// Copyright (c) Kevin Zehrer
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Diagnostics;

#if SYSTEMCLOCK
namespace KZDev.SystemClock.PrimeTime.Testing;
#else
using NodaTime;

namespace KZDev.PrimeTime.Testing;
#endif

//################################################################################
/// <summary>
///   Shared implementation of <see cref="IPrimeTestClock"/> virtual time, delays,
///   cancellation entries, and virtual interval/day-time timers. Stack-specific instant
///   storage and local-time mapping live in partials.
/// </summary>
public sealed partial class PrimeTestClock : PrimeTestTimeBase, IPrimeTestClock
{
    //----------------------------------------------------------------------------
    /// <summary>
    ///   Monotonically assigned registration id for virtual timers.
    /// </summary>
    private static int _nextTimerId;

    /// <summary>
    ///   Background thread used when <see cref="PrimeTestTimeBase.InternalIsRunning"/> is <c>true</c>.
    /// </summary>
    private Thread? _runThread;

    /// <summary>
    ///   Virtual time advanced per real second when running automatically.
    /// </summary>
    private TimeSpan _runRate = TimeSpan.FromSeconds(1);

    /// <summary>
    ///   Minimum amount of virtual time that may elapse per real second when starting the runner
    ///   with an explicit rate (see <see cref="Start(System.TimeSpan?)"/>).
    /// </summary>
    private static readonly TimeSpan MinimumStartRunRate = TimeSpan.FromMilliseconds(100);

    /// <summary>
    ///   Maximum amount of virtual time that may elapse per real second when starting the runner
    ///   with an explicit rate (see <see cref="Start(System.TimeSpan?)"/>).
    /// </summary>
    private static readonly TimeSpan MaximumStartRunRate = TimeSpan.FromHours(1);

    /// <summary>
    ///   Defensive upper bound on the number of forward-march reconciliation attempts
    ///   <see cref="SetTime(System.DateTimeOffset)"/> may perform before declaring the march unable
    ///   to converge on the requested instant.
    /// </summary>
    /// <remarks>
    ///   <para>
    ///     A single attempt is the steady-state case for single-threaded forward
    ///     <see cref="SetTime(System.DateTimeOffset)"/>. Additional attempts are needed only when another
    ///     thread mutates virtual time between the march and the post-march observation under
    ///     <see cref="PrimeTestTimeBase.Gate"/>, leaving the clock short of the requested target so
    ///     marching must be retried.
    ///   </para>
    ///   <para>
    ///     The value is intentionally orders of magnitude above any expected concurrent contention so it
    ///     never trips in normal test runs, while still guaranteeing the loop terminates if a future
    ///     logic regression caused it to fail to make progress. It is not a published product limit and
    ///     may be tuned without affecting observable behavior.
    ///   </para>
    /// </remarks>
    private const int MaximumForwardMarchReconcilePasses = 10_000;

    /// <summary>
    ///   Active virtual interval timer registrations.
    /// </summary>
    private readonly List<VirtualIntervalTimerBase> _intervalTimers = [];

#if NET || !SYSTEMCLOCK
    /// <summary>
    ///   Active virtual day-time timer registrations.
    /// </summary>
    private readonly List<VirtualDayTimeTimerBase> _dayTimeTimers = [];
#endif

    /// <summary>
    ///   Occurs when the clock's current time has changed.
    /// </summary>
    public event EventHandler<ClockTimeChangedEventArgs>? ClockEvents;
    //----------------------------------------------------------------------------
    /// <summary>
    ///   Converts a virtual UTC instant to local-offset representation used by local-time APIs.
    /// </summary>
    /// <param name="utcNowOffset">Virtual instant in UTC offset form.</param>
    /// <returns>Same instant in the clock's local coordinate system.</returns>
    private partial DateTimeOffset ToLocalOffset (DateTimeOffset utcNowOffset);
    //----------------------------------------------------------------------------
    /// <summary>
    ///   Returns the UTC offset for a local wall-clock calendar <see cref="DateTime"/> in the same time zone
    ///   the clock uses for <see cref="LocalNowDateTimeOffset"/>.
    /// </summary>
    /// <param name="localUnspecified">Local date and time with <see cref="DateTime.Kind"/> <see cref="DateTimeKind.Unspecified"/>.</param>
    /// <returns>The offset from UTC at that local wall time.</returns>
    private partial TimeSpan GetLocalWallClockUtcOffset (DateTime localUnspecified);
    //----------------------------------------------------------------------------
    /// <summary>
    ///   Sets the virtual UTC instant while <see cref="PrimeTestTimeBase.Gate"/> is held.
    /// </summary>
    /// <param name="utcNowOffset">The new virtual UTC time.</param>
    private partial void SetVirtualUtcNowLocked (DateTimeOffset utcNowOffset);
    //----------------------------------------------------------------------------
    /// <summary>
    ///   Adds virtual elapsed time while <see cref="PrimeTestTimeBase.Gate"/> is held.
    /// </summary>
    /// <param name="duration">Amount of virtual time to add (maybe clamped by partial implementations).</param>
    private partial void AddVirtualTimeLocked (TimeSpan duration);
    //----------------------------------------------------------------------------
    /// <summary>
    ///   Raises <see cref="ClockEvents"/> after the virtual UTC instant changed.
    /// </summary>
    /// <param name="utcNowDateTimeOffset">The new virtual UTC time to report.</param>
    private partial void RaiseClockEventsAfterVirtualUtcChange (DateTimeOffset utcNowDateTimeOffset);
    //----------------------------------------------------------------------------

    #region Local time-of-day scheduling

    //----------------------------------------------------------------------------
    /// <summary>
    ///   Computes the UTC instant of the next occurrence of <paramref name="targetTimeOfDay"/> on or after
    ///   <paramref name="localNow"/>, using <paramref name="getLocalWallClockUtcOffset"/> to resolve the UTC offset for
    ///   the scheduled local wall-clock instant (including across daylight saving time transitions).
    /// </summary>
    /// <remarks>
    ///   Virtual local day-time timers use <c>DayTimeBclLocalWallTimeScheduling</c> (System Clock) or
    ///   <c>DayTimeNodaLocalWallTimeScheduling</c> (superset assembly) instead of this method so skipped/duplicate policies
    ///   match production.
    /// </remarks>
    /// <param name="localNow">
    ///   Current time in the local coordinate system (same interpretation as <see cref="LocalNowDateTimeOffset"/>).
    /// </param>
    /// <param name="targetTimeOfDay">Time of day since local midnight.</param>
    /// <param name="getLocalWallClockUtcOffset">
    ///   Returns the UTC offset for a local wall-clock <see cref="DateTime"/> with kind
    ///   <see cref="DateTimeKind.Unspecified"/>.
    /// </param>
    /// <returns>The next due instant in UTC.</returns>
    internal static DateTimeOffset ComputeNextLocalTimeOfDayAsUtc (DateTimeOffset localNow, TimeSpan targetTimeOfDay,
        Func<DateTime, TimeSpan> getLocalWallClockUtcOffset)
    {
        // Base the next occurrence on the current local calendar date (local wall-clock midnight).
        DateTime todayMidnight = localNow.Date;
        DateTime nextDt = todayMidnight + targetTimeOfDay;
        // If today's target time-of-day is not strictly in the future, schedule the same clock time on the next day.
        if (nextDt <= localNow.DateTime)
            nextDt = nextDt.AddDays(1);
        // Unspecified wall-clock time is interpreted in the clock's local zone, including when that instant is
        // ambiguous (fall-back) or not otherwise representable as a single wall time (spring-forward gap).
        DateTime nextLocalUnspecified = DateTime.SpecifyKind(nextDt, DateTimeKind.Unspecified);
        // Delegate returns the UTC offset for that wall time. Callers that need option-driven DST
        // resolution use DayTimeBclLocalWallTimeScheduling / DayTimeNodaLocalWallTimeScheduling instead.
        TimeSpan nextLocalOffset = getLocalWallClockUtcOffset(nextLocalUnspecified);
        // Wall clock plus resolved offset is fixed; convert to UTC so the scheduled instant is unambiguous downstream.
        DateTimeOffset nextLocal = new(nextLocalUnspecified, nextLocalOffset);
        return nextLocal.ToUniversalTime();
    }
    //----------------------------------------------------------------------------

    #endregion Local time-of-day scheduling

    #region Private helpers

    //----------------------------------------------------------------------------
    /// <summary>
    ///   Background loop: sleeps one real second, then advances virtual time by <see cref="_runRate"/>.
    /// </summary>
    private void RunLoop ()
    {
        while (true)
        {
            Thread.Sleep(1000);
            TimeSpan toAdvance;
            lock (Gate)
            {
                if (!InternalIsRunning)
                    return;
                toAdvance = _runRate;
            }

            Advance(toAdvance);
        }
    }
    //----------------------------------------------------------------------------
    /// <summary>
    ///   Removes an interval registration from the active list (under <see cref="PrimeTestTimeBase.Gate"/>).
    /// </summary>
    /// <param name="timer">The registration to remove.</param>
    private void RemoveIntervalTimer (VirtualIntervalTimerBase timer)
    {
        lock (Gate)
        {
            _intervalTimers.Remove(timer);
        }
    }
    //----------------------------------------------------------------------------
    /// <summary>
    ///   Returns whether any interval timer registration on this clock is still active.
    /// </summary>
    /// <returns>
    ///   <c>true</c> if at least one interval timer registration is active (see <see cref="IClockIntervalTimer"/>);
    ///   otherwise <c>false</c>.
    /// </returns>
    /// <remarks>
    ///   The caller must hold <see cref="PrimeTestTimeBase.Gate"/>.
    /// </remarks>
    private bool AnyActiveIntervalTimerLocked ()
    {
        foreach (VirtualIntervalTimerBase timer in _intervalTimers)
        {
            if (timer.IsActive)
            {
                return true;
            }
        }

        return false;
    }
    //----------------------------------------------------------------------------
    /// <summary>
    ///   Throws when changing virtual time strictly backward would violate backward-time rules for the test clock.
    /// </summary>
    /// <param name="targetUtc">The virtual UTC instant being set.</param>
    /// <param name="currentUtc">The current virtual UTC instant before the adjustment.</param>
    /// <remarks>
    ///   The caller must hold <see cref="PrimeTestTimeBase.Gate"/>. No-op when
    ///   <paramref name="targetUtc"/> is on or after <paramref name="currentUtc"/>.
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    ///   Thrown when <paramref name="targetUtc"/> is strictly before <paramref name="currentUtc"/> and either the
    ///   clock is running or any interval timer registration is active.
    /// </exception>
    private void ThrowIfBackwardVirtualTimeChangeDisallowedLocked (DateTimeOffset targetUtc, DateTimeOffset currentUtc)
    {
        if (targetUtc.UtcTicks >= currentUtc.UtcTicks)
        {
            return;
        }

        if (InternalIsRunning)
        {
            throw new InvalidOperationException(
                "Cannot move the test clock's virtual time backward while it is running.");
        }

        if (AnyActiveIntervalTimerLocked())
        {
            throw new InvalidOperationException(
                "Cannot move the test clock's virtual time backward while an interval timer registration is active.");
        }
    }
    //----------------------------------------------------------------------------

#if NET || !SYSTEMCLOCK

    //----------------------------------------------------------------------------
    /// <summary>
    ///   Recomputes next-due instants for all virtual day-time timer registrations after a permitted backward clock
    ///   jump.
    /// </summary>
    /// <remarks>
    ///   The caller must hold <see cref="PrimeTestTimeBase.Gate"/>.
    /// </remarks>
    private void RecomputeDayTimeTimersAfterPermittedBackwardJumpLocked ()
    {
        DateTimeOffset virtualNowUtc = ReadVirtualUtcNowLocked();
        foreach (VirtualDayTimeTimerBase timer in _dayTimeTimers)
        {
            timer.RecomputeNextDueUtcAfterPermittedBackwardJump(virtualNowUtc);
        }
    }
    //----------------------------------------------------------------------------
    /// <summary>
    ///   Removes a day-time registration from the active list (under <see cref="PrimeTestTimeBase.Gate"/>).
    /// </summary>
    /// <param name="timer">The registration to remove.</param>
    private void RemoveDayTimeTimer (VirtualDayTimeTimerBase timer)
    {
        lock (Gate)
        {
            _dayTimeTimers.Remove(timer);
        }
    }
    //----------------------------------------------------------------------------
    /// <summary>
    ///   Registers a local time-of-day timer and adds it to <see cref="_dayTimeTimers"/>.
    /// </summary>
    /// <param name="targetTimeOfDay">Time of day since local midnight.</param>
    /// <param name="kind">Callback shape.</param>
    /// <param name="callback">User callback.</param>
    /// <param name="state">Optional state.</param>
    /// <param name="options">Day-time options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The new registration.</returns>
    private IClockDayTimeTimer RegisterTimeOfDayLocal (TimeSpan targetTimeOfDay,
        TimerCallbackKind kind, Delegate callback, object? state,
        DayTimeTimerOptions? options, CancellationToken cancellationToken)
    {
        VirtualDayTimeTimerBase t = new VirtualDayTimeTimer(this, isLocal: true,
            targetTimeOfDay, kind, callback, state, options, cancellationToken);
        lock (Gate)
        {
            _dayTimeTimers.Add(t);
        }
        return t;
    }
    //----------------------------------------------------------------------------
    /// <summary>
    ///   Registers a UTC time-of-day timer and adds it to <see cref="_dayTimeTimers"/>.
    /// </summary>
    /// <param name="targetTimeOfDay">Time of day since UTC midnight.</param>
    /// <param name="kind">Callback shape.</param>
    /// <param name="callback">User callback.</param>
    /// <param name="state">Optional state.</param>
    /// <param name="options">Day-time options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The new registration.</returns>
    private IClockDayTimeTimer RegisterTimeOfDayUtc (TimeSpan targetTimeOfDay,
        TimerCallbackKind kind, Delegate callback, object? state,
        DayTimeTimerOptions? options, CancellationToken cancellationToken)
    {
        VirtualDayTimeTimerBase t = new VirtualDayTimeTimer(this, isLocal: false,
            targetTimeOfDay, kind, callback, state, options, cancellationToken);
        lock (Gate)
        {
            _dayTimeTimers.Add(t);
        }
        return t;
    }
    //----------------------------------------------------------------------------

#endif

    #endregion Private helpers

    #region Virtual-time march

    //----------------------------------------------------------------------------
    /// <summary>
    ///   Computes the next virtual UTC instant to stop at when marching from <paramref name="nowUtc"/> toward
    ///   <paramref name="targetUtc"/>: the minimum of <paramref name="targetUtc"/> and every pending delay, time
    ///   expiry, interval, or day-time deadline strictly after <paramref name="nowUtc"/> and on or before
    ///   <paramref name="targetUtc"/>.
    /// </summary>
    /// <param name="nowUtc">Current virtual UTC instant (caller holds <see cref="PrimeTestTimeBase.Gate"/>).</param>
    /// <param name="targetUtc">Inclusive march horizon in UTC.</param>
    /// <returns>The next instant to assign to the virtual clock.</returns>
    private DateTimeOffset ComputeNextMarchInstantUtcLocked (DateTimeOffset nowUtc, DateTimeOffset targetUtc)
    {
        DateTimeOffset? bestUtc = null;
        foreach (PendingDelay pendingDelay in PendingDelays)
        {
            if (pendingDelay.DueUtc <= nowUtc)
            {
                continue;
            }

            if (pendingDelay.DueUtc > targetUtc)
            {
                continue;
            }

            bestUtc = bestUtc is null || pendingDelay.DueUtc < bestUtc ? pendingDelay.DueUtc : bestUtc;
        }

        foreach (TimeExpiryEntry expiryEntry in TimeExpiryEntries)
        {
            if (expiryEntry.ExpireUtc <= nowUtc)
            {
                continue;
            }

            if (expiryEntry.ExpireUtc > targetUtc)
            {
                continue;
            }

            bestUtc = bestUtc is null || expiryEntry.ExpireUtc < bestUtc ? expiryEntry.ExpireUtc : bestUtc;
        }

        foreach (VirtualIntervalTimerBase intervalTimer in _intervalTimers)
        {
            intervalTimer.ConsiderEarliestDueUtcStrictlyAfterForMarch(ref bestUtc, nowUtc, targetUtc);
        }

#if NET || !SYSTEMCLOCK
        foreach (VirtualDayTimeTimerBase dayTimeTimer in _dayTimeTimers)
        {
            dayTimeTimer.ConsiderEarliestDueUtcStrictlyAfterForMarch(ref bestUtc, nowUtc, targetUtc);
        }
#endif

        return bestUtc ?? targetUtc;
    }
    //----------------------------------------------------------------------------
    /// <summary>
    ///   Dispatches all delay completions, time expiries, and timer callbacks that are due at the current virtual UTC
    ///   instant, without advancing virtual time.
    /// </summary>
    /// <returns>
    ///   <c>true</c> if any work was dispatched; otherwise <c>false</c>.
    /// </returns>
    private bool TryDispatchAllDueAtCurrentVirtualUtc ()
    {
        DateTimeOffset instantUtc;
        lock (Gate)
        {
            instantUtc = ReadVirtualUtcNowLocked();
        }

        List<PendingDelay>? toComplete = null;
        List<TimeExpiryEntry>? toCancel = null;
        List<VirtualIntervalTimerBase>? intervalDue = null;
#if NET || !SYSTEMCLOCK
        List<VirtualDayTimeTimerBase>? dayTimeDue = null;
#endif

        lock (Gate)
        {
            foreach (PendingDelay pendingDelay in PendingDelays)
            {
                if (pendingDelay.DueUtc > instantUtc)
                {
                    continue;
                }

                toComplete ??= [];
                toComplete.Add(pendingDelay);
            }

            if (toComplete != null)
            {
                foreach (PendingDelay toCompleteDelay in toComplete)
                {
                    PendingDelays.Remove(toCompleteDelay);
                }
            }

            foreach (TimeExpiryEntry expiryEntry in TimeExpiryEntries)
            {
                if (expiryEntry.ExpireUtc > instantUtc)
                {
                    continue;
                }

                toCancel ??= [];
                toCancel.Add(expiryEntry);
            }

            if (toCancel != null)
            {
                foreach (TimeExpiryEntry timeExpiry in toCancel)
                {
                    TimeExpiryEntries.Remove(timeExpiry);
                }
            }

            foreach (VirtualIntervalTimerBase timerBase in _intervalTimers)
            {
                if (!timerBase.IsDue(instantUtc))
                {
                    continue;
                }

                intervalDue ??= [];
                intervalDue.Add(timerBase);
            }

#if NET || !SYSTEMCLOCK
            foreach (VirtualDayTimeTimerBase timerBase in _dayTimeTimers)
            {
                if (!timerBase.IsDue(instantUtc))
                {
                    continue;
                }

                dayTimeDue ??= [];
                dayTimeDue.Add(timerBase);
            }
#endif
        }

        bool didWork = toComplete != null || toCancel != null || intervalDue != null;
#if NET || !SYSTEMCLOCK
        didWork = didWork || dayTimeDue != null;
#endif

        if (toComplete != null)
        {
            foreach (PendingDelay pendingDelay in toComplete)
            {
                pendingDelay.Complete();
            }
        }

        if (toCancel != null)
        {
            foreach (TimeExpiryEntry timeExpiry in toCancel)
            {
                timeExpiry.Cancel();
            }
        }

        while (intervalDue is { Count: > 0 })
        {
            foreach (VirtualIntervalTimerBase timerBase in intervalDue)
            {
                timerBase.RunDueCallback(instantUtc);
            }

            intervalDue = null;
            lock (Gate)
            {
                foreach (VirtualIntervalTimerBase timerBase in _intervalTimers)
                {
                    if (!timerBase.IsDue(instantUtc))
                    {
                        continue;
                    }

                    intervalDue ??= [];
                    intervalDue.Add(timerBase);
                }
            }

            didWork = true;
        }

#if NET || !SYSTEMCLOCK
        while (dayTimeDue is { Count: > 0 })
        {
            foreach (VirtualDayTimeTimerBase timerBase in dayTimeDue)
            {
                timerBase.RunDueCallback(instantUtc);
            }

            dayTimeDue = null;
            lock (Gate)
            {
                foreach (VirtualDayTimeTimerBase timerBase in _dayTimeTimers)
                {
                    if (!timerBase.IsDue(instantUtc))
                    {
                        continue;
                    }

                    dayTimeDue ??= [];
                    dayTimeDue.Add(timerBase);
                }
            }

            didWork = true;
        }
#endif

        return didWork;
    }
    //----------------------------------------------------------------------------
    /// <summary>
    ///   Marches virtual UTC forward to <paramref name="targetUtc"/> along pending deadlines, dispatching work at each
    ///   distinct instant and raising <see cref="ClockEvents"/> once per marched instant where virtual time changes.
    /// </summary>
    /// <param name="targetUtc">Virtual UTC instant to reach.</param>
    private void MarchVirtualUtcForwardToTargetRaisingClockEvents (DateTimeOffset targetUtc)
    {
        while (true)
        {
            while (TryDispatchAllDueAtCurrentVirtualUtc())
            {
            }

            DateTimeOffset stepUtc;
            lock (Gate)
            {
                DateTimeOffset nowUtc = ReadVirtualUtcNowLocked();
                if (nowUtc >= targetUtc)
                {
                    return;
                }

                stepUtc = ComputeNextMarchInstantUtcLocked(nowUtc, targetUtc);
                SetVirtualUtcNowLocked(stepUtc);
            }

            _ = TryDispatchAllDueAtCurrentVirtualUtc();
            RaiseClockEventsAfterVirtualUtcChange(stepUtc);
        }
    }
    //----------------------------------------------------------------------------

    #endregion Virtual-time march

    #region Interface Implementations

    #region IPrimeTestClock Implementation

    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public void SetTime (DateTimeOffset utcTime)
    {
        bool marchToTarget;
        lock (Gate)
        {
            DateTimeOffset nowUtc = ReadVirtualUtcNowLocked();
            if (utcTime.UtcTicks <= nowUtc.UtcTicks)
            {
                if (utcTime.UtcTicks < nowUtc.UtcTicks)
                {
                    ThrowIfBackwardVirtualTimeChangeDisallowedLocked(utcTime, nowUtc);
                }

                SetVirtualUtcNowLocked(utcTime);
#if NET || !SYSTEMCLOCK
                if (utcTime.UtcTicks < nowUtc.UtcTicks)
                {
                    RecomputeDayTimeTimersAfterPermittedBackwardJumpLocked();
                }
#endif
                marchToTarget = false;
            }
            else
            {
                marchToTarget = true;
            }
        }

        if (!marchToTarget)
        {
            RaiseClockEventsAfterVirtualUtcChange(utcTime);
            return;
        }

        for (int attemptNumber = 1; attemptNumber <= MaximumForwardMarchReconcilePasses; attemptNumber++)
        {
            MarchVirtualUtcForwardToTargetRaisingClockEvents(utcTime);

            lock (Gate)
            {
                DateTimeOffset afterMarchUtc = ReadVirtualUtcNowLocked();
                if (afterMarchUtc.UtcTicks == utcTime.UtcTicks)
                {
                    return;
                }

                if (afterMarchUtc.UtcTicks > utcTime.UtcTicks)
                {
                    ThrowIfBackwardVirtualTimeChangeDisallowedLocked(utcTime, afterMarchUtc);
                    SetVirtualUtcNowLocked(utcTime);
#if NET || !SYSTEMCLOCK
                    RecomputeDayTimeTimersAfterPermittedBackwardJumpLocked();
#endif
                    RaiseClockEventsAfterVirtualUtcChange(utcTime);
                    return;
                }
            }
        }

        DateTimeOffset currentAfterFinalPass;
        lock (Gate)
        {
            currentAfterFinalPass = ReadVirtualUtcNowLocked();
        }

        string diagnosticMessage =
            $"Forward virtual-time march did not reach the requested instant after {MaximumForwardMarchReconcilePasses} attempt(s). "
            + $"Target UTC: {utcTime:o}; virtual UTC after final attempt: {currentAfterFinalPass:o}. "
            + "This is typically caused by extreme concurrent contention (another thread mutated virtual time between the march and the "
            + "post-march observation under the clock gate on every attempt, leaving the clock short of the target each time) or by a logic "
            + "regression in the forward-march path that prevents virtual time from progressing toward the target. "
            + "To diagnose, check whether other threads are concurrently advancing or setting the clock during this call, and verify that "
            + "virtual UTC is making forward progress toward the target after each march iteration.";
        throw new InvalidOperationException(diagnosticMessage);
    }
    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public void Advance (TimeSpan duration)
    {
        if (duration < TimeSpan.Zero)
        {
            duration = TimeSpan.Zero;
        }

        DateTimeOffset targetUtc;
        lock (Gate)
        {
            targetUtc = ReadVirtualUtcNowLocked() + duration;
        }

        MarchVirtualUtcForwardToTargetRaisingClockEvents(targetUtc);

        if (duration == TimeSpan.Zero)
        {
            DateTimeOffset utcNow;
            lock (Gate)
            {
                utcNow = ReadVirtualUtcNowLocked();
            }

            RaiseClockEventsAfterVirtualUtcChange(utcNow);
        }
    }
    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public void RunFor (TimeSpan duration)
    {
        Advance(duration);
    }
    //----------------------------------------------------------------------------
    /// <summary>
    ///   Throws if <paramref name="rate"/> is outside the inclusive range allowed for
    ///   <see cref="Start(System.TimeSpan?)"/>.
    /// </summary>
    /// <param name="rate">
    ///   Virtual time that should elapse per one real second.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    ///   Thrown when <paramref name="rate"/> is less than 100 milliseconds or greater than 1 hour.
    /// </exception>
    private static void ThrowIfStartRunRateOutOfRange (TimeSpan rate)
    {
        if (rate < MinimumStartRunRate || rate > MaximumStartRunRate)
        {
            throw new ArgumentOutOfRangeException(nameof(rate),
                rate,
                "Virtual time per real second must be between 100 milliseconds and 1 hour, inclusive.");
        }
    }
    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public void Start (TimeSpan? rate = null)
    {
        lock (Gate)
        {
            if (rate is { } explicitRate)
                ThrowIfStartRunRateOutOfRange(explicitRate);
            if (InternalIsRunning)
                return;
            InternalIsRunning = true;
            _runRate = rate ?? TimeSpan.FromSeconds(1);

            _runThread = new Thread(RunLoop)
            {
                IsBackground = true
            };
            _runThread.Start();
        }
    }
    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public bool Stop ()
    {
        Thread? runningThread;

        lock (Gate)
        {
            if (!InternalIsRunning)
                return false;
            InternalIsRunning = false;
            runningThread = _runThread;
            _runThread = null;
        }
        runningThread?.Join(TimeSpan.FromSeconds(5));
        return true;
    }
    //----------------------------------------------------------------------------

    #endregion IPrimeTestClock Implementation

#if SYSTEMCLOCK
    #region IPrimeClock Implementation — Now

    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public DateTimeOffset LocalNowDateTimeOffset
    {
        get
        {
            lock (Gate)
                return ToLocalOffset(ReadVirtualUtcNowLocked());
        }
    }
    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public DateTimeOffset UtcNowDateTimeOffset
    {
        get
        {
            lock (Gate)
                return ReadVirtualUtcNowLocked();
        }
    }
    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public DateTime LocalNowDateTime
    {
        get
        {
            lock (Gate)
                return ToLocalOffset(ReadVirtualUtcNowLocked()).DateTime;
        }
    }
    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public DateTime UtcNowDateTime
    {
        get
        {
            lock (Gate)
                return ReadVirtualUtcNowLocked().UtcDateTime;
        }
    }
    //----------------------------------------------------------------------------

#if NET
    //----------------------------------------------------------------------------
    /// <inheritdoc />
    /// <remarks>
    ///   <para>
    ///     Only supported in .NET 8+. Not supported in NetStandard 2.0.
    ///   </para>
    /// </remarks>
    public TimeOnly LocalNowTimeOnly
    {
        get
        {
            lock (Gate)
                return TimeOnly.FromDateTime(ToLocalOffset(ReadVirtualUtcNowLocked()).DateTime);
        }
    }
    //----------------------------------------------------------------------------
    /// <inheritdoc />
    /// <remarks>
    ///   <para>
    ///     Only supported in .NET 8+. Not supported in NetStandard 2.0.
    ///   </para>
    /// </remarks>
    public TimeOnly UtcNowTimeOnly
    {
        get
        {
            lock (Gate)
                return TimeOnly.FromDateTime(ReadVirtualUtcNowLocked().UtcDateTime);
        }
    }
    //----------------------------------------------------------------------------
    /// <inheritdoc />
    /// <remarks>
    ///   <para>
    ///     Only supported in .NET 8+. Not supported in NetStandard 2.0.
    ///   </para>
    /// </remarks>
    public DateOnly LocalNowDateOnly
    {
        get
        {
            lock (Gate)
                return DateOnly.FromDateTime(ToLocalOffset(ReadVirtualUtcNowLocked()).DateTime);
        }
    }
    //----------------------------------------------------------------------------
    /// <inheritdoc />
    /// <remarks>
    ///   <para>
    ///     Only supported in .NET 8+. Not supported in NetStandard 2.0.
    ///   </para>
    /// </remarks>
    public DateOnly UtcNowDateOnly
    {
        get
        {
            lock (Gate)
                return DateOnly.FromDateTime(ReadVirtualUtcNowLocked().UtcDateTime);
        }
    }
    //----------------------------------------------------------------------------
#endif

    #endregion IPrimeClock Implementation — Now
#endif

    #region IPrimeClock Implementation — Interval timers

    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public IClockIntervalTimer RegisterTimer (TimeSpan callbackTime,
        TimeSpan repeatInterval,
        Action<ClockTimerCallbackContext> callback, CancellationToken cancellationToken,
        object? state = null, IntervalTimerOptions? timerOptions = null)
    {
        VirtualIntervalTimerBase intervalTimer = new VirtualIntervalTimer(this,
            callbackTime, repeatInterval, TimerCallbackKind.ContextAction,
            callback, state, timerOptions, cancellationToken);
        lock (Gate)
            _intervalTimers.Add(intervalTimer);
        return intervalTimer;
    }
    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public IClockIntervalTimer RegisterAsyncTimer (TimeSpan callbackTime,
        TimeSpan repeatInterval,
        Func<ClockTimerCallbackContext, CancellationToken, ValueTask> callback,
        CancellationToken cancellationToken,
        object? state = null, IntervalTimerOptions? timerOptions = null)
    {
        VirtualIntervalTimerBase intervalTimer = new VirtualIntervalTimer(this,
            callbackTime, repeatInterval, TimerCallbackKind.ContextAsync,
            callback, state, timerOptions, cancellationToken);
        lock (Gate)
            _intervalTimers.Add(intervalTimer);
        return intervalTimer;
    }
    //----------------------------------------------------------------------------

    #endregion IPrimeClock Implementation — Interval timers

#if NET
    #region IPrimeClock Implementation — Day-time timers

    //----------------------------------------------------------------------------
    /// <inheritdoc />
    /// <remarks>
    ///   <para>
    ///     Only supported in .NET 8+. Not supported in NetStandard 2.0.
    ///   </para>
    /// </remarks>
    public IClockDayTimeTimer RegisterTimeOfDay (LocalTimeOfDay timeOfDay,
        Action<ClockTimerCallbackContext> callback, CancellationToken cancellationToken,
        object? state = null, DayTimeTimerOptions? timerOptions = null) =>
        RegisterTimeOfDayLocal(timeOfDay.Value.ToTimeSpan(), TimerCallbackKind.ContextAction, callback, state, timerOptions, cancellationToken);
    //----------------------------------------------------------------------------
    /// <inheritdoc />
    /// <remarks>
    ///   <para>
    ///     Only supported in .NET 8+. Not supported in NetStandard 2.0.
    ///   </para>
    /// </remarks>
    public IClockDayTimeTimer RegisterAsyncTimeOfDay (LocalTimeOfDay timeOfDay,
        Func<ClockTimerCallbackContext, CancellationToken, ValueTask> callback,
        CancellationToken cancellationToken, object? state = null,
        DayTimeTimerOptions? timerOptions = null) =>
        RegisterTimeOfDayLocal(timeOfDay.Value.ToTimeSpan(), TimerCallbackKind.ContextAsync, callback, state, timerOptions, cancellationToken);
    //----------------------------------------------------------------------------
    /// <inheritdoc />
    /// <remarks>
    ///   <para>
    ///     Only supported in .NET 8+. Not supported in NetStandard 2.0.
    ///   </para>
    /// </remarks>
    public IClockDayTimeTimer RegisterTimeOfDay (UtcTimeOfDay timeOfDay,
        Action<ClockTimerCallbackContext> callback,
        CancellationToken cancellationToken, object? state = null,
        DayTimeTimerOptions? timerOptions = null) =>
        RegisterTimeOfDayUtc(timeOfDay.Value.ToTimeSpan(), TimerCallbackKind.ContextAction, callback, state, timerOptions, cancellationToken);
    //----------------------------------------------------------------------------
    /// <inheritdoc />
    /// <remarks>
    ///   <para>
    ///     Only supported in .NET 8+. Not supported in NetStandard 2.0.
    ///   </para>
    /// </remarks>
    public IClockDayTimeTimer RegisterAsyncTimeOfDay (UtcTimeOfDay timeOfDay,
        Func<ClockTimerCallbackContext, CancellationToken, ValueTask> callback,
        CancellationToken cancellationToken, object? state = null,
        DayTimeTimerOptions? timerOptions = null) =>
        RegisterTimeOfDayUtc(timeOfDay.Value.ToTimeSpan(), TimerCallbackKind.ContextAsync, callback, state, timerOptions, cancellationToken);
    //----------------------------------------------------------------------------

    #endregion IPrimeClock Implementation — Day-time timers
#endif
    #endregion Interface Implementations
}
//################################################################################


