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

#if NET || !SYSTEMCLOCK

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

    #region Interface Implementations

    #region IPrimeTestClock Implementation

    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public void SetTime (DateTimeOffset utcTime)
    {
        lock (Gate)
        {
            SetVirtualUtcNowLocked(utcTime);
        }

        RaiseClockEventsAfterVirtualUtcChange(utcTime);
    }
    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public void Advance (TimeSpan duration)
    {
        if (duration < TimeSpan.Zero)
            duration = TimeSpan.Zero;

        DateTimeOffset newNow;
        List<PendingDelay>? toComplete = null;
        List<TimeExpiryEntry>? toCancel = null;
        List<VirtualIntervalTimerBase>? intervalDue = null;
#if NET || !SYSTEMCLOCK
        List<VirtualDayTimeTimerBase>? dayTimeDue = null;
#endif

        lock (Gate)
        {
            AddVirtualTimeLocked(duration);
            newNow = ReadVirtualUtcNowLocked();

            foreach (PendingDelay pendingDelay in PendingDelays)
            {
                if (pendingDelay.DueUtc > newNow)
                {
                    continue;
                }

                toComplete ??= [];
                toComplete.Add(pendingDelay);
            }

            if (toComplete != null)
            {
                foreach (PendingDelay toCompleteDelay in toComplete)
                    PendingDelays.Remove(toCompleteDelay);
            }

            foreach (TimeExpiryEntry expiryEntry in TimeExpiryEntries)
            {
                if (expiryEntry.ExpireUtc > newNow)
                {
                    continue;
                }

                toCancel ??= [];
                toCancel.Add(expiryEntry);
            }

            if (toCancel != null)
            {
                foreach (TimeExpiryEntry timeExpiry in toCancel)
                    TimeExpiryEntries.Remove(timeExpiry);
            }

            foreach (VirtualIntervalTimerBase timerBase in _intervalTimers)
            {
                if (!timerBase.IsDue(newNow))
                {
                    continue;
                }

                intervalDue ??= [];
                intervalDue.Add(timerBase);
            }

#if NET || !SYSTEMCLOCK
            foreach (VirtualDayTimeTimerBase timerBase in _dayTimeTimers)
            {
                if (!timerBase.IsDue(newNow))
                {
                    continue;
                }

                dayTimeDue ??= [];
                dayTimeDue.Add(timerBase);
            }
#endif
        }

        if (toComplete != null)
        {
            foreach (PendingDelay pendingDelay in toComplete)
                pendingDelay.Complete();
        }

        if (toCancel != null)
        {
            foreach (TimeExpiryEntry timeExpiry in toCancel)
                timeExpiry.Cancel();
        }

        while (intervalDue is { Count: > 0 })
        {
            foreach (VirtualIntervalTimerBase timerBase in intervalDue)
                timerBase.RunDueCallback(newNow);

            intervalDue = null;
            lock (Gate)
            {
                foreach (VirtualIntervalTimerBase timerBase in _intervalTimers)
                {
                    if (!timerBase.IsDue(newNow))
                    {
                        continue;
                    }

                    intervalDue ??= [];
                    intervalDue.Add(timerBase);
                }
            }
        }

#if NET || !SYSTEMCLOCK
        while (dayTimeDue is { Count: > 0 })
        {
            foreach (VirtualDayTimeTimerBase timerBase in dayTimeDue)
                timerBase.RunDueCallback(newNow);

            dayTimeDue = null;
            lock (Gate)
            {
                foreach (VirtualDayTimeTimerBase timerBase in _dayTimeTimers)
                {
                    if (!timerBase.IsDue(newNow))
                    {
                        continue;
                    }

                    dayTimeDue ??= [];
                    dayTimeDue.Add(timerBase);
                }
            }
        }
#endif

        RaiseClockEventsAfterVirtualUtcChange(newNow);
    }
    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public void RunFor (TimeSpan duration)
    {
        Advance(duration);
    }
    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public void Start (TimeSpan? rate = null)
    {
        lock (Gate)
        {
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


