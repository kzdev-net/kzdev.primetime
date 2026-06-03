// Copyright (c) Kevin Zehrer
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Globalization;

#if SYSTEMCLOCK
namespace KZDev.SystemClock.PrimeTime.Testing;
#else
using NodaTime;

namespace KZDev.PrimeTime.Testing;
#endif

//################################################################################
/// <summary>
///   Virtual test clock: event marching on <see cref="Advance(System.TimeSpan)"/> and forward
///   <see cref="SetTime(System.DateTimeOffset)"/>, optional deadline-driven automatic advancement from
///   <see cref="Start(System.TimeSpan?)"/>, and persist-on-read "now" projection while running.
/// </summary>
/// <remarks>
///   <para>
///     See <see cref="IPrimeTestClock"/> for the public contract (run-rate limits, backward-time rules, runner waits,
///     15 ms burst rule, actual-wait catch-up, <see cref="ClockEvents"/> cadence, and day-time <c>NextDueUtc</c>
///     behavior). Stack-specific partials supply BCL or NodaTime instant storage and local zone mapping.
///   </para>
/// </remarks>
public sealed partial class PrimeTestClock : PrimeTestTimeBase, IPrimeTestClock
{
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
    ///   Minimum real time the runner uses as a sleep threshold: shorter intended waits are handled by bursting
    ///   virtual steps instead of sleeping.
    /// </summary>
    /// <remarks>
    ///   <para>
    ///     Fifteen milliseconds is the documented contract for the test clock runner (see product specification:
    ///     do not request a real wait shorter than this; process virtual deadlines in order until the next sleep would
    ///     be at least this long, or work is idle). Real waits shorter than typical thread and timer scheduling
    ///     granularity are noisy and often oversleep, which would fight the separate &quot;actual elapsed&quot;
    ///     catch-up path; skipping those sleeps keeps behavior predictable and avoids a tight sleep spin loop.
    ///   </para>
    /// </remarks>
    private static readonly TimeSpan MinimumRunnerRealWait = TimeSpan.FromMilliseconds(15);

    /// <summary>
    ///   Virtual elapsed time without a substantive event before the runner raises a heartbeat <see cref="ClockEvents"/>.
    /// </summary>
    private static readonly TimeSpan VirtualMinuteHeartbeatInterval = TimeSpan.FromMinutes(1);

    /// <summary>
    ///   Sentinel upper bound passed to timer march helpers when scanning for the next runner deadline with no finite
    ///   cap.
    /// </summary>
    /// <remarks>
    ///   <para>
    ///     This value is not used as a virtual instant to advance the clock to; it is only supplied as
    ///     <c>targetUtc</c> to
    ///     <see cref="VirtualIntervalTimerBase.ConsiderEarliestDueUtcStrictlyAfterForMarch"/> and the parallel
    ///     day-time helper (same signature in <c>PrimeTestClock.VirtualDayTimeTimer</c> when that feature is compiled)
    ///     so every finite <c>NextDueUtc</c> lies on or before the bound. <see cref="DateTimeOffset.MaxValue"/> is used
    ///     because it is unambiguous and avoids threading <see langword="null"/> through those helpers for this single
    ///     call site.
    ///   </para>
    /// </remarks>
    private static readonly DateTimeOffset RunnerDeadlineHorizonUtc = DateTimeOffset.MaxValue;

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
    ///   Maximum real time <see cref="Stop"/> waits for the automatic runner thread to exit.
    /// </summary>
    private static readonly TimeSpan StopJoinTimeout = TimeSpan.FromSeconds(5);

    /// <summary>
    ///   Maximum real time <see cref="Start(System.TimeSpan?)"/> waits for an overlapping <see cref="Stop"/> to finish
    ///   joining the runner (<see cref="StopJoinTimeout"/> plus one second of scheduling slack).
    /// </summary>
    private static readonly TimeSpan MaximumWaitForOverlappingRunnerStop = StopJoinTimeout + TimeSpan.FromSeconds(1);

    //----------------------------------------------------------------------------
    /// <summary>
    ///   Monotonically assigned registration id for virtual timers.
    /// </summary>
    private static int _nextTimerId;

    /// <summary>
    ///   Signaled when <see cref="_runnerStopInProgress"/> is cleared after <see cref="Stop"/> finishes join handling.
    ///   Used instead of <see cref="Monitor.Wait(object)"/> so waiters do not nest a second lock with
    ///   <see cref="PrimeTestTimeBase.Gate"/>.
    /// </summary>
    private readonly ManualResetEventSlim _runnerStopCompletedEvent = new(initialState: true);

    /// <summary>
    ///   Test-only: signaled after <see cref="Stop"/> sets <see cref="_runnerStopInProgress"/> and releases
    ///   <see cref="PrimeTestTimeBase.Gate"/> before joining the runner thread.
    /// </summary>
    internal ManualResetEventSlim TestRunnerStopJoinPhaseEntered { [DebuggerStepThrough] get; } = new(initialState: false);

    /// <summary>
    ///   Real elapsed time since the last committed virtual instant while <see cref="PrimeTestTimeBase.InternalIsRunning"/>
    ///   is <c>true</c>, shared with observer projection and deadline-driven runner wake budgeting.
    ///   Monotonic real-time elapsed since the committed virtual instant was established while the automatic runner is
    ///   active.
    /// </summary>
    /// <remarks>
    ///   <para>
    ///     <see cref="Stopwatch"/> is not thread-safe for un-synchronized concurrent use. Every read or write of this
    ///     instance (including <see cref="Stopwatch.Elapsed"/>, <see cref="Stopwatch.Restart"/>, and
    ///     <see cref="Stopwatch.Reset"/>) occurs only while the caller holds <see cref="PrimeTestTimeBase.Gate"/>, so
    ///     only one thread touches it at a time.
    ///   </para>
    /// </remarks>
    private readonly Stopwatch _runAnchorStopwatch = new();

    /// <summary>
    ///   Cooperative wake for a future deadline-driven runner wait when scheduling or the committed anchor changes.
    /// </summary>
    private readonly ManualResetEventSlim _runnerWakeEvent = new(initialState: false);

    /// <summary>
    ///   Counts nested calls from virtual-time march callbacks into observable "now" members within the same
    ///   logical asynchronous execution flow on this clock so persist-on-read does not recurse while
    ///   a virtual-time march is in progress.
    /// </summary>
    /// <remarks>
    ///   <para>
    ///     Each clock has its own <see cref="AsyncLocal{T}"/> so multiple <see cref="PrimeTestClock"/> instances in the
    ///     same test or execution context do not share a persistence depth. Values still flow with
    ///     <see cref="ExecutionContext"/> so continuations and typical <see cref="Task.Run(Action)"/> work observe the
    ///     same depth as the outer observation even when they run on a different OS thread. Work that explicitly starts
    ///     a thread without flowing context (for example <see cref="Thread.Start()"/>) does not inherit the depth.
    ///   </para>
    /// </remarks>
    private readonly AsyncLocal<int> _observationUtcPersistenceFrameDepth = new();

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
    ///   Background thread used when <see cref="PrimeTestTimeBase.InternalIsRunning"/> is <c>true</c>.
    /// </summary>
    private Thread? _runThread;

    /// <summary>
    ///   <c>true</c> while <see cref="Stop"/> has cleared <see cref="PrimeTestTimeBase.InternalIsRunning"/> but has
    ///   not finished joining the runner and raising <see cref="PrimeTestClockEventType.ClockStopped"/>.
    /// </summary>
    private bool _runnerStopInProgress;

    /// <summary>
    ///   <c>true</c> after <see cref="Stop"/> failed to join the automatic runner within the allowed time. While set,
    ///   <see cref="Start(System.TimeSpan?)"/> fails fast so a second runner cannot be started while the prior thread
    ///   may still be executing.
    /// </summary>
    private bool _runnerStopJoinFailed;

    /// <summary>
    ///   Virtual time advanced per real second when running automatically.
    /// </summary>
    private TimeSpan _runRate = TimeSpan.FromSeconds(1);

    /// <summary>
    ///   <c>true</c> while a <see cref="RunFor(System.TimeSpan, System.TimeSpan)"/> bounded run is active.
    /// </summary>
    private bool _runForBounded;

    /// <summary>
    ///   Virtual UTC instant at which an active bounded <see cref="RunFor(System.TimeSpan, System.TimeSpan)"/> run
    ///   commits and stops; set only while <see cref="_runForBounded"/> is <c>true</c>.
    /// </summary>
    private DateTimeOffset? _runForStopUtc;

    /// <summary>
    ///   Monotonically assigned when a bounded <see cref="RunFor(System.TimeSpan, System.TimeSpan)"/> run starts;
    ///   identifies the active bounded run when completing at a stop horizon after a virtual-time march.
    /// </summary>
    private ulong _runForBoundedGeneration;

    /// <summary>
    ///   Virtual UTC instant from which the runner measures the next virtual-minute <see cref="ClockEvents"/> heartbeat.
    /// </summary>
    private DateTimeOffset _runnerHeartbeatWindowStartUtc;

    /// <summary>
    ///   Occurs when the clock publishes a discriminated lifecycle or virtual-time event.
    /// </summary>
    /// <remarks>
    ///   Subscriber exceptions propagate to the caller that raised the event. Raise paths capture this delegate once,
    ///   skip payload allocation when the snapshot is <see langword="null"/>, then invoke the snapshot. Multicast
    ///   invocation stops at the first throwing handler; later handlers
    ///   are not called. Raises from the automatic runner run on the background runner thread, so an unhandled
    ///   subscriber exception can abort virtual-time work in progress or terminate the runner while
    ///   <see cref="IPrimeTestTime.IsRunning"/> is still <c>true</c>.
    /// </remarks>
    public event PrimeTestClockEventHandler? ClockEvents;
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
    ///   Raises <see cref="PrimeTestClockEventType.NewTime"/> on <see cref="ClockEvents"/> after the virtual UTC instant changed.
    /// </summary>
    /// <param name="utcNowDateTimeOffset">The new virtual UTC time to report.</param>
    private partial void RaiseNewTimeEvent (DateTimeOffset utcNowDateTimeOffset);
    //----------------------------------------------------------------------------
    /// <summary>
    ///   Raises <see cref="PrimeTestClockEventType.ClockStarted"/> on <see cref="ClockEvents"/>.
    /// </summary>
    /// <param name="startUtc">Committed virtual UTC when the runner started.</param>
    /// <param name="runRateTimeSpan">Active runner rate for the new run.</param>
    private partial void RaiseClockStartedEvent (DateTimeOffset startUtc, TimeSpan runRateTimeSpan);
    //----------------------------------------------------------------------------
    /// <summary>
    ///   Raises <see cref="PrimeTestClockEventType.ClockStopped"/> on <see cref="ClockEvents"/>.
    /// </summary>
    /// <param name="finalUtc">Final committed virtual UTC after the runner stopped.</param>
    /// <param name="runRateTimeSpan">Runner rate that was active before stop, if any.</param>
    private partial void RaiseClockStoppedEvent (DateTimeOffset finalUtc, TimeSpan? runRateTimeSpan);
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

    #region Runner loop

    //----------------------------------------------------------------------------
    /// <summary>
    ///   Background loop: waits until the next virtual deadline (or real timeout), then marches virtual time using
    ///   measured real elapsed time on <see cref="_runAnchorStopwatch"/>.
    /// </summary>
    private void RunLoop ()
    {
        while (true)
        {
            if (!TryPrepareNextRunnerSleep(out DateTimeOffset nextDeadlineUtc, out TimeSpan intendedRealWait))
            {
                return;
            }

            if (intendedRealWait <= TimeSpan.Zero)
            {
                MarchVirtualUtcForwardToTargetRaisingClockEventsFromRunnerLoop(nextDeadlineUtc);

                if (TryCompleteBoundedRunFromRunnerLoop())
                {
                    return;
                }

                lock (Gate)
                {
                    if (!InternalIsRunning)
                    {
                        return;
                    }

                    ResetRunnerHeartbeatWindowLocked(ReadVirtualUtcNowLocked());
                }

                continue;
            }

            bool rescheduleImmediately;
            lock (Gate)
            {
                if (!InternalIsRunning)
                {
                    return;
                }

                // Scheduling signals Set under Gate; observe and clear the latch under the same lock so a wake cannot
                // arrive between IsSet and Reset and be lost. If a wake is already latched, skip sleeping and recompute.
                rescheduleImmediately = _runnerWakeEvent.IsSet;
                _runnerWakeEvent.Reset();
            }

            if (rescheduleImmediately)
            {
                continue;
            }

            int waitMilliseconds = GetRunnerWaitMilliseconds(intendedRealWait);
            _ = _runnerWakeEvent.Wait(waitMilliseconds);

            DateTimeOffset marchTargetUtc;
            lock (Gate)
            {
                if (!InternalIsRunning)
                {
                    return;
                }

                TimeSpan actualRealElapsed = _runAnchorStopwatch.Elapsed;
                DateTimeOffset nowUtc = ReadVirtualUtcNowLocked();
                TimeSpan virtualBudget = ScaleRealElapsedToVirtualTime(actualRealElapsed, _runRate);
                marchTargetUtc = nowUtc + virtualBudget;
                marchTargetUtc = CapMarchTargetUtcAtBoundedRunStopHorizonLocked(marchTargetUtc);
            }

            MarchVirtualUtcForwardToTargetRaisingClockEventsFromRunnerLoop(marchTargetUtc);

            if (TryCompleteBoundedRunFromRunnerLoop())
            {
                return;
            }

            lock (Gate)
            {
                if (!InternalIsRunning)
                {
                    return;
                }

                ResetRunnerHeartbeatWindowLocked(ReadVirtualUtcNowLocked());
            }
        }
    }
    //----------------------------------------------------------------------------
    /// <summary>
    ///   Computes the next virtual deadline and intended real wait; when that wait is below
    ///   <see cref="MinimumRunnerRealWait"/>, marches virtual time in-process until the next slice would sleep at least
    ///   that long (or work is due with zero virtual delta to the deadline).
    /// </summary>
    /// <param name="nextDeadlineUtc">Next virtual UTC instant the runner is targeting for the slice.</param>
    /// <param name="intendedRealWait">Real time to wait for that virtual delta at <see cref="_runRate"/>.</param>
    /// <returns>
    ///   <see langword="false"/> when the automatic runner has stopped; otherwise <see langword="true"/> with outputs
    ///   describing the next slice.
    /// </returns>
    private bool TryPrepareNextRunnerSleep (out DateTimeOffset nextDeadlineUtc, out TimeSpan intendedRealWait)
    {
        while (true)
        {
            lock (Gate)
            {
                if (!InternalIsRunning)
                {
                    nextDeadlineUtc = default;
                    intendedRealWait = TimeSpan.Zero;
                    return false;
                }

                DateTimeOffset nowUtc = ReadVirtualUtcNowLocked();
                nextDeadlineUtc = GetEarliestRunnerDeadlineUtcLocked(nowUtc);
                TimeSpan virtualDelta = nextDeadlineUtc - nowUtc;
                intendedRealWait = virtualDelta <= TimeSpan.Zero ? TimeSpan.Zero : ScaleVirtualDeltaToRealTime(virtualDelta, _runRate);
            }

            if (intendedRealWait >= MinimumRunnerRealWait)
            {
                return true;
            }

            MarchVirtualUtcForwardToTargetRaisingClockEventsFromRunnerLoop(nextDeadlineUtc);

            if (TryCompleteBoundedRunFromRunnerLoop())
            {
                nextDeadlineUtc = default;
                intendedRealWait = TimeSpan.Zero;
                return false;
            }

            lock (Gate)
            {
                if (!InternalIsRunning)
                {
                    nextDeadlineUtc = default;
                    intendedRealWait = TimeSpan.Zero;
                    return false;
                }

                ResetRunnerHeartbeatWindowLocked(ReadVirtualUtcNowLocked());
            }

            if (intendedRealWait <= TimeSpan.Zero)
            {
                return true;
            }

            // Sub-minimum real wait: another burst iteration may follow; yield so a pathological schedule cannot spin.
            Thread.Yield();
        }
    }
    //----------------------------------------------------------------------------
    /// <summary>
    ///   Returns the earliest virtual UTC deadline the runner should reach next: substantive work or the virtual-minute
    ///   heartbeat, whichever is sooner.
    /// </summary>
    /// <param name="nowUtc">Current committed virtual UTC instant.</param>
    /// <returns>The next deadline strictly after <paramref name="nowUtc"/>, or <paramref name="nowUtc"/> when due now.</returns>
    /// <remarks>
    ///   The caller must hold <see cref="PrimeTestTimeBase.Gate"/>.
    /// </remarks>
    private DateTimeOffset GetEarliestRunnerDeadlineUtcLocked (DateTimeOffset nowUtc)
    {
        DateTimeOffset? bestUtc = null;
        foreach (PendingDelay pendingDelay in PendingDelays)
        {
            if (pendingDelay.DueUtc <= nowUtc)
            {
                return nowUtc;
            }

            bestUtc = bestUtc is null || pendingDelay.DueUtc < bestUtc ? pendingDelay.DueUtc : bestUtc;
        }

        foreach (TimeExpiryEntry expiryEntry in TimeExpiryEntries)
        {
            if (expiryEntry.ExpireUtc <= nowUtc)
            {
                return nowUtc;
            }

            bestUtc = bestUtc is null || expiryEntry.ExpireUtc < bestUtc ? expiryEntry.ExpireUtc : bestUtc;
        }

        foreach (VirtualIntervalTimerBase intervalTimer in _intervalTimers)
        {
            intervalTimer.ConsiderEarliestDueUtcStrictlyAfterForMarch(ref bestUtc, nowUtc, RunnerDeadlineHorizonUtc);
        }

#if NET || !SYSTEMCLOCK
        foreach (VirtualDayTimeTimerBase dayTimeTimer in _dayTimeTimers)
        {
            dayTimeTimer.ConsiderEarliestDueUtcStrictlyAfterForMarch(ref bestUtc, nowUtc, RunnerDeadlineHorizonUtc);
        }
#endif

        DateTimeOffset heartbeatDeadlineUtc = _runnerHeartbeatWindowStartUtc + VirtualMinuteHeartbeatInterval;
        if (heartbeatDeadlineUtc <= nowUtc)
        {
            return nowUtc;
        }

        DateTimeOffset earliestUtc = bestUtc is null || heartbeatDeadlineUtc < bestUtc ? heartbeatDeadlineUtc : bestUtc.Value;
        if (!_runForBounded || _runForStopUtc is not { } runForStopUtc)
        {
            return earliestUtc;
        }

        if (runForStopUtc <= nowUtc)
        {
            return nowUtc;
        }

        if (runForStopUtc < earliestUtc)
        {
            earliestUtc = runForStopUtc;
        }

        return earliestUtc;
    }
    //----------------------------------------------------------------------------
    /// <summary>
    ///   Restarts the virtual-minute heartbeat window from <paramref name="instantUtc"/>.
    /// </summary>
    /// <param name="instantUtc">Virtual UTC instant that last drove virtual time or substantive work.</param>
    /// <remarks>
    ///   The caller must hold <see cref="PrimeTestTimeBase.Gate"/>.
    /// </remarks>
    private void ResetRunnerHeartbeatWindowLocked (DateTimeOffset instantUtc)
    {
        _runnerHeartbeatWindowStartUtc = instantUtc;
    }
    //----------------------------------------------------------------------------
    /// <summary>
    ///   Converts a runner real-wait duration to whole milliseconds for <see cref="ManualResetEventSlim.Wait(int)"/>.
    /// </summary>
    /// <param name="intendedRealWait">Intended real elapsed time before the next wake.</param>
    /// <returns>Non-negative wait time in milliseconds, capped at <see cref="int.MaxValue"/>.</returns>
    private static int GetRunnerWaitMilliseconds (TimeSpan intendedRealWait)
    {
        if (intendedRealWait <= TimeSpan.Zero)
        {
            return 0;
        }

        double totalMilliseconds = intendedRealWait.TotalMilliseconds;
        if (totalMilliseconds >= int.MaxValue)
        {
            return int.MaxValue;
        }

        return (int)Math.Ceiling(totalMilliseconds);
    }
    //----------------------------------------------------------------------------
    /// <summary>
    ///   Notifies the automatic runner that scheduling changed while <see cref="PrimeTestTimeBase.Gate"/> is held.
    /// </summary>
    protected override void OnSchedulingMutatedWhileGateHeld ()
    {
        SignalRunnerWakeIfRunningLocked();
    }
    //----------------------------------------------------------------------------

    #endregion Runner loop

    #region Run anchor and persist-on-read

    //----------------------------------------------------------------------------
    /// <summary>
    ///   Maps a virtual-time interval to the corresponding real elapsed time at the configured
    ///   virtual-time-per-real-second rate.
    /// </summary>
    /// <param name="virtualDelta">Virtual elapsed time until the next deadline.</param>
    /// <param name="virtualTimePerRealSecond">Virtual time that elapses per one real second.</param>
    /// <returns>Real time to wait before that virtual interval elapses at the run rate.</returns>
    private static TimeSpan ScaleVirtualDeltaToRealTime (TimeSpan virtualDelta, TimeSpan virtualTimePerRealSecond)
    {
        long virtualTicks = virtualDelta.Ticks;
        if (virtualTicks == 0)
        {
            return TimeSpan.Zero;
        }

        long rateTicks = virtualTimePerRealSecond.Ticks;
        long scaledTicks;
        // Intermediate product virtualTicks * TicksPerSecond must fit in long; otherwise use decimal to avoid
        // overflow before dividing by rateTicks (very large virtual deltas and/or extremely small run-rate ticks).
        if (TicksProductFitsInInt64(virtualTicks, TimeSpan.TicksPerSecond))
        {
            long product = virtualTicks * TimeSpan.TicksPerSecond;
            scaledTicks = product / rateTicks;
        }
        else
        {
            decimal scaledDecimal = (decimal)virtualTicks * TimeSpan.TicksPerSecond / rateTicks;
            scaledTicks = (long)scaledDecimal;
        }

        ThrowIfScaledTicksOutsideTimeSpanRange(scaledTicks);
        return TimeSpan.FromTicks(scaledTicks);
    }
    //----------------------------------------------------------------------------
    /// <summary>
    ///   Maps a real-time elapsed interval to virtual elapsed using the configured virtual-time-per-real-second rate.
    /// </summary>
    /// <param name="realElapsed">Elapsed real time on the anchor stopwatch.</param>
    /// <param name="virtualTimePerRealSecond">Virtual time that elapses per one real second.</param>
    /// <returns>Corresponding virtual elapsed time.</returns>
    /// <remarks>
    ///   <para>
    ///     Uses <see cref="long"/> arithmetic when the intermediate product
    ///     <c><paramref name="realElapsed"/>.Ticks * <paramref name="virtualTimePerRealSecond"/>.Ticks</c> fits in a
    ///     <see cref="long"/>; otherwise uses <see cref="decimal"/> for that product so it does not overflow before
    ///     dividing by <see cref="TimeSpan.TicksPerSecond"/>. Truncation toward zero when converting the scaled ticks to
    ///     <see cref="long"/> matches the prior floating-point cast behavior for non-extreme durations.
    ///   </para>
    /// </remarks>
    internal static TimeSpan ScaleRealElapsedToVirtualTime (TimeSpan realElapsed, TimeSpan virtualTimePerRealSecond)
    {
        long realTicks = realElapsed.Ticks;
        long rateTicks = virtualTimePerRealSecond.Ticks;
        long scaledTicks;
        // Intermediate product realTicks * rateTicks must fit in long; otherwise use decimal to avoid overflow
        // before dividing by TicksPerSecond (extreme elapsed × rate combinations).
        if (TicksProductFitsInInt64(realTicks, rateTicks))
        {
            long product = realTicks * rateTicks;
            scaledTicks = product / TimeSpan.TicksPerSecond;
        }
        else
        {
            decimal scaledDecimal = (decimal)realTicks * rateTicks / TimeSpan.TicksPerSecond;
            scaledTicks = (long)scaledDecimal;
        }

        ThrowIfScaledTicksOutsideTimeSpanRange(scaledTicks);
        return TimeSpan.FromTicks(scaledTicks);
    }
    //----------------------------------------------------------------------------
    /// <summary>
    ///   Throws <see cref="OverflowException"/> when scaled tick counts are outside the range representable as a
    ///   <see cref="TimeSpan"/>.
    /// </summary>
    /// <param name="scaledTicks">Virtual elapsed ticks after scaling real elapsed by the run rate.</param>
    private static void ThrowIfScaledTicksOutsideTimeSpanRange (long scaledTicks)
    {
        if (scaledTicks > TimeSpan.MaxValue.Ticks || scaledTicks < TimeSpan.MinValue.Ticks)
        {
            throw new OverflowException(
                "Scaled virtual elapsed time exceeds the representable TimeSpan range.");
        }
    }
    //----------------------------------------------------------------------------
    /// <summary>
    ///   Returns whether <c>x * y</c> is representable as a <see cref="long"/> without overflowing signed
    ///   multiplication.
    /// </summary>
    /// <param name="x">First factor (typically real elapsed ticks).</param>
    /// <param name="y">Second factor (typically virtual-time-per-real-second ticks).</param>
    /// <returns>
    ///   <see langword="true"/> if the product fits in <see cref="long"/>; otherwise <see langword="false"/>.
    /// </returns>
    private static bool TicksProductFitsInInt64 (long x, long y)
    {
        if (x == 0 || y == 0)
        {
            return true;
        }

        if (x == long.MinValue)
        {
            return y == 1;
        }

        if (y == long.MinValue)
        {
            return x == 1;
        }

        long absX = Math.Abs(x);
        long absY = Math.Abs(y);
        return absX <= long.MaxValue / absY;
    }
    //----------------------------------------------------------------------------
    /// <summary>
    ///   Computes linearly projected virtual UTC from the last committed instant and anchor stopwatch while the
    ///   runner is active. The caller must hold <see cref="PrimeTestTimeBase.Gate"/>.
    /// </summary>
    /// <returns>Projected virtual UTC.</returns>
    private DateTimeOffset ComputeProjectedVirtualUtcLocked ()
    {
        if (!InternalIsRunning)
        {
            return ReadVirtualUtcNowLocked();
        }

        DateTimeOffset committedUtc = ReadVirtualUtcNowLocked();
        TimeSpan realElapsed = _runAnchorStopwatch.Elapsed;
        TimeSpan virtualElapsed = ScaleRealElapsedToVirtualTime(realElapsed, _runRate);
        return committedUtc + virtualElapsed;
    }
    //----------------------------------------------------------------------------
    /// <summary>
    ///   Signals the runner wake handle when the automatic runner is active. The caller must hold
    ///   <see cref="PrimeTestTimeBase.Gate"/>.
    /// </summary>
    private void SignalRunnerWakeIfRunningLocked ()
    {
        if (InternalIsRunning)
        {
            _runnerWakeEvent.Set();
        }
    }
    //----------------------------------------------------------------------------
    /// <summary>
    ///   Persists a new committed virtual UTC instant while the runner is active and restarts the monotonic anchor so
    ///   projection and a future deadline-driven runner share the same timeline.
    /// </summary>
    /// <param name="utcNowOffset">The new committed virtual UTC instant.</param>
    /// <remarks>
    ///   The caller must hold <see cref="PrimeTestTimeBase.Gate"/>.
    /// </remarks>
    private void CommitVirtualUtcInstantLocked (DateTimeOffset utcNowOffset)
    {
        SetVirtualUtcNowLocked(utcNowOffset);
        if (!InternalIsRunning)
        {
            return;
        }

        _runAnchorStopwatch.Restart();
        SignalRunnerWakeIfRunningLocked();
    }
    //----------------------------------------------------------------------------
    /// <summary>
    ///   Returns the virtual UTC instant for observable clock APIs, applying persist-on-read while the automatic runner
    ///   is active.
    /// </summary>
    /// <returns>The virtual UTC instant observers should use.</returns>
    /// <remarks>
    ///   <para>
    ///     For the outer persist-on-read path, <see cref="_observationUtcPersistenceFrameDepth"/> is incremented under
    ///     <see cref="PrimeTestTimeBase.Gate"/> in the same critical section as the
    ///     <see cref="PrimeTestTimeBase.InternalIsRunning"/> and nested-depth checks, and decremented under the same lock
    ///     in <c>finally</c> so no other thread can begin a new gated observation pass between unlock and depth teardown.
    ///   </para>
    /// </remarks>
    private DateTimeOffset GetObservationVirtualUtcDateTimeOffset ()
    {
        lock (Gate)
        {
            if (!InternalIsRunning)
            {
                return ReadVirtualUtcNowLocked();
            }

            if (_observationUtcPersistenceFrameDepth.Value > 0)
            {
                return ComputeProjectedVirtualUtcLocked();
            }

            _observationUtcPersistenceFrameDepth.Value++;
        }

        try
        {
            DateTimeOffset targetUtc;
            lock (Gate)
            {
                if (!InternalIsRunning)
                {
                    return ReadVirtualUtcNowLocked();
                }

                targetUtc = ComputeProjectedVirtualUtcLocked();
            }

            MarchVirtualUtcForwardToTargetRaisingClockEventsFromExternalCaller(targetUtc);

            lock (Gate)
            {
                if (!InternalIsRunning)
                {
                    return ReadVirtualUtcNowLocked();
                }

                _runAnchorStopwatch.Restart();
                SignalRunnerWakeIfRunningLocked();
                return ReadVirtualUtcNowLocked();
            }
        }
        finally
        {
            lock (Gate)
            {
                _observationUtcPersistenceFrameDepth.Value--;
            }
        }
    }
    //----------------------------------------------------------------------------

    #endregion Run anchor and persist-on-read

    /// <summary>
    ///   Removes an interval registration from the active list (under <see cref="PrimeTestTimeBase.Gate"/>).
    /// </summary>
    /// <param name="timer">The registration to remove.</param>
    private void RemoveIntervalTimer (VirtualIntervalTimerBase timer)
    {
        lock (Gate)
        {
            if (_intervalTimers.Remove(timer))
            {
                OnSchedulingMutatedWhileGateHeld();
            }
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
            if (_dayTimeTimers.Remove(timer))
            {
                OnSchedulingMutatedWhileGateHeld();
            }
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
            OnSchedulingMutatedWhileGateHeld();
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
            OnSchedulingMutatedWhileGateHeld();
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

        if (!didWork)
        {
            return didWork;
        }

        lock (Gate)
        {
            if (InternalIsRunning)
            {
                ResetRunnerHeartbeatWindowLocked(ReadVirtualUtcNowLocked());
            }
        }

        return didWork;
    }
    //----------------------------------------------------------------------------
    /// <summary>
    ///   Marches virtual UTC forward to <paramref name="targetUtc"/> from the automatic runner loop, completing an
    ///   active bounded <see cref="RunFor(System.TimeSpan, System.TimeSpan)"/> at the stop horizon without joining the
    ///   runner thread.
    /// </summary>
    /// <param name="targetUtc">Virtual UTC instant to reach.</param>
    private void MarchVirtualUtcForwardToTargetRaisingClockEventsFromRunnerLoop (DateTimeOffset targetUtc) =>
        MarchVirtualUtcForwardToTargetRaisingClockEventsBody(targetUtc, calledFromRunnerLoop: true);
    //----------------------------------------------------------------------------
    /// <summary>
    ///   Marches virtual UTC forward to <paramref name="targetUtc"/> from an external caller (for example
    ///   <see cref="Advance(TimeSpan)"/> or <see cref="SetTime(DateTimeOffset)"/>), joining the runner thread when a
    ///   bounded <see cref="RunFor(System.TimeSpan, System.TimeSpan)"/> completes at the stop horizon.
    /// </summary>
    /// <param name="targetUtc">Virtual UTC instant to reach.</param>
    private void MarchVirtualUtcForwardToTargetRaisingClockEventsFromExternalCaller (DateTimeOffset targetUtc) =>
        MarchVirtualUtcForwardToTargetRaisingClockEventsBody(targetUtc, calledFromRunnerLoop: false);
    //----------------------------------------------------------------------------
    /// <summary>
    ///   Marches virtual UTC forward to <paramref name="targetUtc"/> along pending deadlines, dispatching work at each
    ///   distinct instant and raising <see cref="ClockEvents"/> once per marched instant where virtual time changes.
    /// </summary>
    /// <param name="targetUtc">Virtual UTC instant to reach.</param>
    /// <param name="calledFromRunnerLoop">
    ///   <see langword="true"/> when invoked from <see cref="RunLoop"/> on the runner thread (bounded completion must not
    ///   self-join).
    /// </param>
    private void MarchVirtualUtcForwardToTargetRaisingClockEventsBody (DateTimeOffset targetUtc, bool calledFromRunnerLoop)
    {
        // When targetUtc reaches or passes an active RunFor stop horizon, march in two phases: first to the horizon
        // (complete the bounded run and raise ClockStopped), then continue from committed virtual now to targetUtc.
        while (true)
        {
            DateTimeOffset marchTargetUtc;
            bool crossedBoundedStopHorizon;
            ulong boundedRunGeneration = 0;
            DateTimeOffset boundedStopHorizonUtc = default;
            lock (Gate)
            {
                if (ReadVirtualUtcNowLocked() >= targetUtc)
                {
                    return;
                }

                marchTargetUtc = CapMarchTargetUtcAtBoundedRunStopHorizonLocked(targetUtc);
                crossedBoundedStopHorizon = _runForBounded
                    && _runForStopUtc is { } stopUtc
                    && targetUtc >= stopUtc
                    && marchTargetUtc == stopUtc;
                if (crossedBoundedStopHorizon)
                {
                    boundedRunGeneration = _runForBoundedGeneration;
                    boundedStopHorizonUtc = marchTargetUtc;
                }
            }

            MarchVirtualUtcForwardToTargetRaisingClockEventsCore(marchTargetUtc);

            if (!crossedBoundedStopHorizon)
            {
                return;
            }

            if (!CompleteBoundedRunFor(
                    calledFromRunnerLoop,
                    expectedBoundedRunGeneration: boundedRunGeneration,
                    expectedStopHorizonUtc: boundedStopHorizonUtc))
            {
                continue;
            }
        }
    }
    //----------------------------------------------------------------------------
    /// <summary>
    ///   Marches virtual UTC forward to <paramref name="targetUtc"/> without bounded-run horizon handling.
    /// </summary>
    /// <param name="targetUtc">Virtual UTC instant to reach.</param>
    private void MarchVirtualUtcForwardToTargetRaisingClockEventsCore (DateTimeOffset targetUtc)
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
                CommitVirtualUtcInstantLocked(stepUtc);
            }

            _ = TryDispatchAllDueAtCurrentVirtualUtc();
            RaiseNewTimeEvent(stepUtc);
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
            ThrowIfVirtualTimeMutationBlockedDuringRunnerStopLocked();
            DateTimeOffset nowUtc = ReadVirtualUtcNowLocked();
            // Is the clock being advanced, or is it already at or beyond the target time?
            // In either case, we can set directly to the target and raise events once.
            if (utcTime.UtcTicks <= nowUtc.UtcTicks)
            {
                // Is the clock being moved backward, and if so, would that violate backward-time rules?
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
            RaiseNewTimeEvent(utcTime);
            return;
        }

        for (int attemptNumber = 1; attemptNumber <= MaximumForwardMarchReconcilePasses; attemptNumber++)
        {
            MarchVirtualUtcForwardToTargetRaisingClockEventsFromExternalCaller(utcTime);

            lock (Gate)
            {
                DateTimeOffset afterMarchUtc = ReadVirtualUtcNowLocked();
                if (afterMarchUtc.UtcTicks == utcTime.UtcTicks)
                {
                    return;
                }

                if (afterMarchUtc.UtcTicks <= utcTime.UtcTicks)
                {
                    continue;
                }

                ThrowIfBackwardVirtualTimeChangeDisallowedLocked(utcTime, afterMarchUtc);
                SetVirtualUtcNowLocked(utcTime);
#if NET || !SYSTEMCLOCK
                RecomputeDayTimeTimersAfterPermittedBackwardJumpLocked();
#endif
                RaiseNewTimeEvent(utcTime);
                return;
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
            ThrowIfVirtualTimeMutationBlockedDuringRunnerStopLocked();
            if (InternalIsRunning)
            {
                ResetRunnerHeartbeatWindowLocked(ReadVirtualUtcNowLocked());
            }

            targetUtc = ReadVirtualUtcNowLocked() + duration;
        }

        MarchVirtualUtcForwardToTargetRaisingClockEventsFromExternalCaller(targetUtc);

        if (duration != TimeSpan.Zero)
        {
            return;
        }

        DateTimeOffset utcNow;
        lock (Gate)
        {
            utcNow = ReadVirtualUtcNowLocked();
        }

        RaiseNewTimeEvent(utcNow);
    }
    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public bool RunFor (TimeSpan duration) =>
        RunFor(duration, TimeSpan.FromSeconds(1));
    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public bool RunFor (TimeSpan duration, TimeSpan perSecondRate)
    {
        if (duration < TimeSpan.Zero)
        {
            duration = TimeSpan.Zero;
        }

        ThrowIfStartRunRateOutOfRange(perSecondRate);

        DateTimeOffset startUtc;
        TimeSpan runRateTimeSpan = perSecondRate;
        bool zeroDuration;
        while (true)
        {
            lock (Gate)
            {
                ThrowIfRunnerStopJoinFailedLocked();
                if (InternalIsRunning)
                {
                    return false;
                }

                if (_runnerStopInProgress)
                {
                    // Release Gate and wait for Stop to finish before retrying.
                }
                else
                {
                    startUtc = ReadVirtualUtcNowLocked();
                    _runForBoundedGeneration++;
                    _runForStopUtc = startUtc + duration;
                    _runForBounded = true;
                    _runRate = perSecondRate;
                    zeroDuration = duration == TimeSpan.Zero;
                    InternalIsRunning = true;
                    _runnerHeartbeatWindowStartUtc = startUtc;
                    _runAnchorStopwatch.Restart();
                    break;
                }
            }

            WaitForOverlappingRunnerStopCompletion();
        }

        RaiseClockStartedEvent(startUtc, runRateTimeSpan);

        if (zeroDuration)
        {
            ulong boundedRunGeneration;
            DateTimeOffset stopHorizonUtc;
            lock (Gate)
            {
                boundedRunGeneration = _runForBoundedGeneration;
                stopHorizonUtc = _runForStopUtc ?? ReadVirtualUtcNowLocked();
            }

            CompleteBoundedRunFor(
                calledFromRunnerLoop: false,
                expectedBoundedRunGeneration: boundedRunGeneration,
                expectedStopHorizonUtc: stopHorizonUtc);
        }
        else
        {
            lock (Gate)
            {
                try
                {
                    _runThread = new Thread(RunLoop)
                    {
                        IsBackground = true
                    };
                    _runThread.Start();
                }
                catch
                {
                    InternalIsRunning = false;
                    _runForBounded = false;
                    _runForStopUtc = null;
                    _runThread = null;
                    _runAnchorStopwatch.Reset();
                    _runnerHeartbeatWindowStartUtc = ReadVirtualUtcNowLocked();
                    throw;
                }
            }
        }

        return true;
    }
    //----------------------------------------------------------------------------
    /// <summary>
    ///   When the caller holds <see cref="PrimeTestTimeBase.Gate"/>, caps a runner march target at the bounded
    ///   <see cref="RunFor(System.TimeSpan, System.TimeSpan)"/> stop horizon when one is active.
    /// </summary>
    /// <param name="marchTargetUtc">Proposed virtual UTC march target.</param>
    /// <returns>
    ///   <paramref name="marchTargetUtc"/>, or the bounded stop horizon when it is sooner.
    /// </returns>
    private DateTimeOffset CapMarchTargetUtcAtBoundedRunStopHorizonLocked (DateTimeOffset marchTargetUtc)
    {
        if (_runForBounded && _runForStopUtc is { } stopUtc && marchTargetUtc > stopUtc)
        {
            return stopUtc;
        }

        return marchTargetUtc;
    }
    //----------------------------------------------------------------------------
    /// <summary>
    ///   When virtual time has reached the bounded <see cref="RunFor(System.TimeSpan, System.TimeSpan)"/> stop
    ///   horizon on the runner thread, commits at that horizon, stops the runner, and raises
    ///   <see cref="PrimeTestClockEventType.ClockStopped"/>.
    /// </summary>
    /// <returns>
    ///   <see langword="true"/> when the bounded run was completed and the runner loop should exit.
    /// </returns>
    private bool TryCompleteBoundedRunFromRunnerLoop ()
    {
        ulong boundedRunGeneration;
        DateTimeOffset stopHorizonUtc;
        lock (Gate)
        {
            if (!InternalIsRunning || !_runForBounded || _runForStopUtc is not { } stopUtc)
            {
                return false;
            }

            if (ReadVirtualUtcNowLocked() < stopUtc)
            {
                return false;
            }

            boundedRunGeneration = _runForBoundedGeneration;
            stopHorizonUtc = stopUtc;
        }

        return CompleteBoundedRunFor(
            calledFromRunnerLoop: true,
            expectedBoundedRunGeneration: boundedRunGeneration,
            expectedStopHorizonUtc: stopHorizonUtc);
    }
    //----------------------------------------------------------------------------
    /// <summary>
    ///   Ends an active bounded <see cref="RunFor(System.TimeSpan, System.TimeSpan)"/> run at
    ///   <see cref="_runForStopUtc"/>, commits virtual time, optionally joins the runner thread, and raises
    ///   <see cref="PrimeTestClockEventType.ClockStopped"/>.
    /// </summary>
    /// <param name="calledFromRunnerLoop">
    ///   <see langword="true"/> when invoked from <see cref="RunLoop"/> on the runner thread.
    ///   Thread identity is also validated to prevent self-join if this hint is wrong.
    /// </param>
    /// <param name="expectedBoundedRunGeneration">
    ///   When set, completion proceeds only when <see cref="_runForBoundedGeneration"/> still matches this value.
    /// </param>
    /// <param name="expectedStopHorizonUtc">
    ///   When <paramref name="expectedBoundedRunGeneration"/> is set, completion proceeds only when
    ///   <see cref="_runForStopUtc"/> still equals this horizon and virtual time has reached it.
    /// </param>
    /// <returns>
    ///   <see langword="true"/> when the bounded run was completed; otherwise <see langword="false"/>.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    ///   Thrown when the automatic runner thread could not be joined within <see cref="StopJoinTimeout"/>.
    /// </exception>
    private bool CompleteBoundedRunFor (bool calledFromRunnerLoop, ulong? expectedBoundedRunGeneration = null,
        DateTimeOffset? expectedStopHorizonUtc = null)
    {
        Thread? runningThread;
        bool shouldJoinRunnerThread;
        TimeSpan? runRateTimeSpan;
        DateTimeOffset finalUtc;

        lock (Gate)
        {
            if (!_runForBounded)
            {
                return false;
            }

            if (expectedBoundedRunGeneration is ulong expectedGeneration)
            {
                if (expectedStopHorizonUtc is not DateTimeOffset expectedStopHorizon
                    || _runForBoundedGeneration != expectedGeneration
                    || _runForStopUtc is not { } stopUtc
                    || stopUtc != expectedStopHorizon
                    || ReadVirtualUtcNowLocked() < expectedStopHorizon)
                {
                    return false;
                }
            }

            runRateTimeSpan = _runRate;
            finalUtc = _runForStopUtc ?? ReadVirtualUtcNowLocked();
            InternalIsRunning = false;
            _runForBounded = false;
            _runForStopUtc = null;
            _runAnchorStopwatch.Reset();
            _runnerWakeEvent.Set();
            runningThread = _runThread;
            shouldJoinRunnerThread = !calledFromRunnerLoop
                && runningThread is not null
                && !ReferenceEquals(Thread.CurrentThread, runningThread);
            _runThread = null;
        }

        if (shouldJoinRunnerThread && runningThread is not null && !runningThread.Join(StopJoinTimeout))
        {
            lock (Gate)
            {
                _runnerStopJoinFailed = true;
            }

            InvalidOperationException joinTimeoutException = new(
                "Timed out stopping the automatic runner after "
                + StopJoinTimeout.TotalSeconds.ToString("g0", CultureInfo.InvariantCulture)
                + " seconds.");
            joinTimeoutException.Data["BlockedOperations"] =
                "Start(), SetTime, Advance, and RunFor";
            joinTimeoutException.Data["LikelyCause"] =
                "The runner thread may still be executing and is typically blocked inside a ClockEvents subscriber "
                + "or virtual-time dispatch callback.";
            throw joinTimeoutException;
        }

        lock (Gate)
        {
            CommitVirtualUtcInstantLocked(finalUtc);
            finalUtc = ReadVirtualUtcNowLocked();
        }

        RaiseClockStoppedEvent(finalUtc, runRateTimeSpan);
        return true;
    }
    //----------------------------------------------------------------------------
    /// <summary>
    ///   Throws if <paramref name="perSecondRate"/> is outside the inclusive range allowed for
    ///   <see cref="Start(System.TimeSpan?)"/>.
    /// </summary>
    /// <param name="perSecondRate">
    ///   Virtual time that should elapse per one real second.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    ///   Thrown when <paramref name="perSecondRate"/> is less than 100 milliseconds or greater than 1 hour.
    /// </exception>
    private static void ThrowIfStartRunRateOutOfRange (TimeSpan perSecondRate)
    {
        if (perSecondRate < MinimumStartRunRate || perSecondRate > MaximumStartRunRate)
        {
            throw new ArgumentOutOfRangeException(nameof(perSecondRate),
                perSecondRate,
                "Virtual time per real second must be between 100 milliseconds and 1 hour, inclusive.");
        }
    }
    //----------------------------------------------------------------------------
    /// <summary>
    ///   Throws when a prior <see cref="Stop"/> could not join the automatic runner. The caller must hold
    ///   <see cref="PrimeTestTimeBase.Gate"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    ///   Thrown when <see cref="_runnerStopJoinFailed"/> is set.
    /// </exception>
    private void ThrowIfRunnerStopJoinFailedLocked ()
    {
        if (_runnerStopJoinFailed)
        {
            throw new InvalidOperationException(
                "A previous Stop() returned false because the automatic runner did not join within "
                + StopJoinTimeout.TotalSeconds.ToString("g0", CultureInfo.InvariantCulture)
                + " seconds, and the runner thread may still be executing. Start(), SetTime, Advance, and RunFor are "
                + "blocked until that thread exits or this PrimeTestClock instance is discarded. This typically "
                + "indicates the runner loop is blocked "
                + "inside a ClockEvents subscriber or virtual-time dispatch callback.");
        }
    }
    //----------------------------------------------------------------------------
    /// <summary>
    ///   Throws when virtual time must not change while <see cref="Stop"/> is joining the runner or a prior
    ///   <see cref="Stop"/> could not join the runner. The caller must hold <see cref="PrimeTestTimeBase.Gate"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    ///   Thrown when <see cref="_runnerStopInProgress"/> or <see cref="_runnerStopJoinFailed"/> is set.
    /// </exception>
    private void ThrowIfVirtualTimeMutationBlockedDuringRunnerStopLocked ()
    {
        ThrowIfRunnerStopJoinFailedLocked();
        if (_runnerStopInProgress)
        {
            throw new InvalidOperationException(
                "Virtual time cannot be changed while Stop() is joining the automatic runner. Wait for Stop() to "
                + "complete before calling SetTime, Advance, or RunFor.");
        }
    }
    //----------------------------------------------------------------------------
    /// <summary>
    ///   Blocks until an overlapping <see cref="Stop"/> clears <see cref="_runnerStopInProgress"/>, or throws when the
    ///   wait exceeds <see cref="MaximumWaitForOverlappingRunnerStop"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    ///   Thrown when <see cref="_runnerStopJoinFailed"/> is set, or when <see cref="_runnerStopInProgress"/> remains
    ///   set longer than <see cref="MaximumWaitForOverlappingRunnerStop"/>.
    /// </exception>
    private void WaitForOverlappingRunnerStopCompletion ()
    {
        Stopwatch waitStopwatch = Stopwatch.StartNew();
        while (true)
        {
            lock (Gate)
            {
                ThrowIfRunnerStopJoinFailedLocked();
                if (!_runnerStopInProgress)
                {
                    return;
                }
            }

            TimeSpan remaining = MaximumWaitForOverlappingRunnerStop - waitStopwatch.Elapsed;
            if (remaining <= TimeSpan.Zero)
            {
                throw new InvalidOperationException(
                    "Start waited for an overlapping Stop to finish joining the automatic runner, "
                    + "but Stop did not complete within "
                    + MaximumWaitForOverlappingRunnerStop.TotalSeconds.ToString("g0", CultureInfo.InvariantCulture)
                    + " seconds ("
                    + StopJoinTimeout.TotalSeconds.ToString("g0", CultureInfo.InvariantCulture)
                    + " second join timeout plus scheduling slack). This typically indicates the runner loop "
                    + "is blocked inside a ClockEvents subscriber or virtual-time dispatch callback.");
            }

            _runnerStopCompletedEvent.Wait(remaining);
        }
    }
    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public void Start (TimeSpan? perSecondRate = null)
    {
        DateTimeOffset startUtc;
        TimeSpan runRateTimeSpan;
        while (true)
        {
            lock (Gate)
            {
                if (perSecondRate is { } explicitPerSecondRate)
                    ThrowIfStartRunRateOutOfRange(explicitPerSecondRate);
                ThrowIfRunnerStopJoinFailedLocked();
                if (InternalIsRunning)
                    return;
                if (_runnerStopInProgress)
                {
                    // Release Gate and wait for Stop to finish before retrying.
                }
                else
                {
                    _runForBounded = false;
                    _runForStopUtc = null;
                    InternalIsRunning = true;
                    _runRate = perSecondRate ?? TimeSpan.FromSeconds(1);
                    runRateTimeSpan = _runRate;
                    startUtc = ReadVirtualUtcNowLocked();
                    _runnerHeartbeatWindowStartUtc = startUtc;
                    _runAnchorStopwatch.Restart();

                    try
                    {
                        _runThread = new Thread(RunLoop)
                        {
                            IsBackground = true
                        };
                        _runThread.Start();
                    }
                    catch
                    {
                        InternalIsRunning = false;
                        _runThread = null;
                        _runAnchorStopwatch.Reset();
                        _runnerHeartbeatWindowStartUtc = ReadVirtualUtcNowLocked();
                        throw;
                    }

                    break;
                }
            }

            WaitForOverlappingRunnerStopCompletion();
        }

        RaiseClockStartedEvent(startUtc, runRateTimeSpan);
    }
    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public bool Stop ()
    {
        Thread? runningThread;
        TimeSpan? runRateTimeSpan;
        DateTimeOffset finalUtc;

        lock (Gate)
        {
            if (!InternalIsRunning)
                return false;
            runRateTimeSpan = _runRate;
            finalUtc = ComputeProjectedVirtualUtcLocked();
            CommitVirtualUtcInstantLocked(finalUtc);
            finalUtc = ReadVirtualUtcNowLocked();
            InternalIsRunning = false;
            _runForBounded = false;
            _runForStopUtc = null;
            _runnerStopInProgress = true;
            _runnerStopCompletedEvent.Reset();
            _runAnchorStopwatch.Reset();
            _runnerWakeEvent.Set();
            runningThread = _runThread;
            _runThread = null;
        }

        TestRunnerStopJoinPhaseEntered.Set();

        try
        {
            if (runningThread is not null && !runningThread.Join(StopJoinTimeout))
            {
                lock (Gate)
                {
                    _runnerStopJoinFailed = true;
                }

                return false;
            }

            lock (Gate)
            {
                CommitVirtualUtcInstantLocked(finalUtc);
                finalUtc = ReadVirtualUtcNowLocked();
            }

            RaiseClockStoppedEvent(finalUtc, runRateTimeSpan);
            return true;
        }
        finally
        {
            lock (Gate)
            {
                _runnerStopInProgress = false;
            }

            _runnerStopCompletedEvent.Set();
            TestRunnerStopJoinPhaseEntered.Reset();
        }
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
            DateTimeOffset utcObservation = GetObservationVirtualUtcDateTimeOffset();
            return ToLocalOffset(utcObservation);
        }
    }
    //----------------------------------------------------------------------------
    /// <inheritdoc />
    /// <remarks>
    ///   While <see cref="IPrimeTestTime.IsRunning"/> is <c>true</c>, returns projected virtual UTC from the committed
    ///   instant and run anchor, marching through due work up to that instant (persist-on-read) before returning.
    ///   While stopped, returns the persisted instant only.
    /// </remarks>
    public DateTimeOffset UtcNowDateTimeOffset
    {
        get => GetObservationVirtualUtcDateTimeOffset();
    }
    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public DateTime LocalNowDateTime
    {
        get
        {
            DateTimeOffset utcObservation = GetObservationVirtualUtcDateTimeOffset();
            return ToLocalOffset(utcObservation).DateTime;
        }
    }
    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public DateTime UtcNowDateTime
    {
        get
        {
            DateTimeOffset utcObservation = GetObservationVirtualUtcDateTimeOffset();
            return utcObservation.UtcDateTime;
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
            DateTimeOffset utcObservation = GetObservationVirtualUtcDateTimeOffset();
            return TimeOnly.FromDateTime(ToLocalOffset(utcObservation).DateTime);
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
            DateTimeOffset utcObservation = GetObservationVirtualUtcDateTimeOffset();
            return TimeOnly.FromDateTime(utcObservation.UtcDateTime);
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
            DateTimeOffset utcObservation = GetObservationVirtualUtcDateTimeOffset();
            return DateOnly.FromDateTime(ToLocalOffset(utcObservation).DateTime);
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
            DateTimeOffset utcObservation = GetObservationVirtualUtcDateTimeOffset();
            return DateOnly.FromDateTime(utcObservation.UtcDateTime);
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
        {
            _intervalTimers.Add(intervalTimer);
            OnSchedulingMutatedWhileGateHeld();
        }
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
        {
            _intervalTimers.Add(intervalTimer);
            OnSchedulingMutatedWhileGateHeld();
        }
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


