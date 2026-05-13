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
public sealed partial class PrimeTestClock
{
#if NET || !SYSTEMCLOCK
    #region Nested types — Virtual day-time timer

    //============================================================================
    /// <summary>
    ///   Base type for virtual time-of-day timers driven by <see cref="PrimeTestClock"/> time.
    /// </summary>
    private abstract partial class VirtualDayTimeTimerBase : IClockDayTimeTimer
    {
        //------------------------------------------------------------------------
        /// <summary>
        ///   One calendar day as a <see cref="TimeSpan"/> for arithmetic in partial implementations.
        /// </summary>
        private static readonly TimeSpan OneDay = TimeSpan.FromDays(1);
        //------------------------------------------------------------------------

        //------------------------------------------------------------------------
        /// <summary>
        ///   Gets the owning test clock.
        /// </summary>
        protected PrimeTestClock Clock { [DebuggerStepThrough] get; }
        //------------------------------------------------------------------------

        //------------------------------------------------------------------------
        /// <summary>
        ///   Gets whether this registration uses local time-of-day versus UTC time-of-day.
        /// </summary>
        protected bool IsLocal { [DebuggerStepThrough] get; }
        //------------------------------------------------------------------------

        //------------------------------------------------------------------------
        /// <summary>
        ///   Gets the callback delegate shape for this registration.
        /// </summary>
        protected TimerCallbackKind CallbackKind { [DebuggerStepThrough] get; }
        //------------------------------------------------------------------------

        //------------------------------------------------------------------------
        /// <summary>
        ///   Gets the user callback delegate.
        /// </summary>
        protected Delegate Callback { [DebuggerStepThrough] get; }
        //------------------------------------------------------------------------

        //------------------------------------------------------------------------
        /// <summary>
        ///   Gets optional state passed to context-based callbacks.
        /// </summary>
        protected object? CallbackState { [DebuggerStepThrough] get; }
        //------------------------------------------------------------------------

        //------------------------------------------------------------------------
        /// <summary>
        ///   Gets the cancellation token associated with this registration.
        /// </summary>
        protected CancellationToken CancellationToken { [DebuggerStepThrough] get; }
        //------------------------------------------------------------------------

#if NET10_OR_GREATER
        /// <summary>
        ///   Synchronizes access to this registration's mutable fields.
        /// </summary>
        protected Lock Gate { [DebuggerStepThrough] get; } = new();
#else
        //------------------------------------------------------------------------
        /// <summary>
        ///   Synchronizes access to this registration's mutable fields.
        /// </summary>
        protected object Gate { [DebuggerStepThrough] get; } = new();
        //------------------------------------------------------------------------
#endif

        //------------------------------------------------------------------------
        /// <summary>
        ///   Gets or sets the next scheduled callback instant in UTC.
        /// </summary>
        protected DateTimeOffset? NextDueUtc { [DebuggerStepThrough] get; [DebuggerStepThrough] set; }
        //------------------------------------------------------------------------

        //------------------------------------------------------------------------
        /// <summary>
        ///   The instant of the last callback.
        /// </summary>
        /// <remarks>
        ///   <para>
        ///     Because the offset depends on <see cref="IsLocal"/> and on the time zone and DST
        ///     rules in effect at the instant of the callback, values from different timers, or
        ///     from the same timer across offset changes (such as daylight saving time
        ///     transitions), MUST NOT be naively compared or ordered based solely on the
        ///     <see cref="DateTimeOffset"/> value. In particular:
        ///   </para>
        ///   <list type="bullet">
        ///     <item>
        ///       <description>
        ///         Do not assume that two <see cref="LastCallbackAt"/> values with different
        ///         <see cref="DateTimeOffset.Offset"/> values are different instants; they may
        ///         represent the same UTC instant with different offsets.
        ///       </description>
        ///     </item>
        ///     <item>
        ///       <description>
        ///         Do not compare <see cref="LastCallbackAt"/> values from local and UTC
        ///         timers (or from different time zones) without first normalizing to a common
        ///         time basis (for example, via <see cref="DateTimeOffset.UtcDateTime"/> or
        ///         <see cref="DateTimeOffset.UtcTicks"/>).
        ///       </description>
        ///     </item>
        ///   </list>
        ///   <para>
        ///     This property is primarily intended for diagnostics and for reasoning about a
        ///     single registration in isolation. Code that needs to compare instants across
        ///     timers or across time zones should perform such comparisons in UTC.
        ///   </para>
        ///   <para>
        ///     For <c>IsLocal == true</c> day-time registrations, this stores the same offset
        ///     representation as <see cref="PrimeTestClock.LocalNowDateTimeOffset"/> (that is, the local
        ///     offset that was in effect at the time of the callback). For UTC day-time
        ///     registrations, this is stored with a UTC offset so it aligns with the "now" used
        ///     in <see cref="ElapsedTime"/>.
        ///   </para>
        ///   <para>
        ///     This dual representation is deliberate: it preserves the developer-facing view
        ///     of when the timer fired in the same coordinate system in which the timer was
        ///     registered (local clock versus UTC clock), while still representing a single
        ///     underlying instant in time.
        ///   </para>
        /// </remarks>
        protected DateTimeOffset? LastCallbackAt { [DebuggerStepThrough] get; [DebuggerStepThrough] set; }
        //------------------------------------------------------------------------

        //------------------------------------------------------------------------
        /// <summary>
        ///   Gets or sets the logical timer state.
        /// </summary>
        protected TimerState State { [DebuggerStepThrough] get; [DebuggerStepThrough] set; } = TimerState.Active;
        //------------------------------------------------------------------------

        //------------------------------------------------------------------------
        /// <summary>
        ///   Gets or sets whether day-time callbacks are enabled.
        /// </summary>
        protected bool EnabledDayTime { [DebuggerStepThrough] get; [DebuggerStepThrough] set; } = true;
        //------------------------------------------------------------------------

        //------------------------------------------------------------------------
        /// <summary>
        ///   Gets or sets whether this registration has been disposed.
        /// </summary>
        protected bool Disposed { [DebuggerStepThrough] get; [DebuggerStepThrough] set; }
        //------------------------------------------------------------------------

        //------------------------------------------------------------------------
        /// <summary>
        ///   Gets or sets whether external cancellation was requested.
        /// </summary>
        protected bool CancelRequested { [DebuggerStepThrough] get; [DebuggerStepThrough] set; }
        //------------------------------------------------------------------------

        //------------------------------------------------------------------------
        /// <summary>
        ///   Gets or sets how many callbacks are currently in flight.
        /// </summary>
        protected int CallbacksRunning { [DebuggerStepThrough] get; [DebuggerStepThrough] set; }
        //------------------------------------------------------------------------

        //------------------------------------------------------------------------
        /// <summary>
        ///   Time of day since local or UTC midnight (depending on <see cref="IsLocal"/>).
        /// </summary>
        protected TimeSpan TargetTimeOfDay { [DebuggerStepThrough] get; [DebuggerStepThrough] set; }
        //------------------------------------------------------------------------

        //------------------------------------------------------------------------
        /// <summary>
        ///   Concurrent invocation policy copied from registration options.
        /// </summary>
        private readonly ConcurrentTriggerProcessing _concurrentTriggerProcessing;

        /// <summary>
        ///   Skipped-time policy from registration options (same resolution as
        ///   <see cref="ClockDayTimeTimerRegistration"/>).
        /// </summary>
        private readonly SkippedTimeBehavior _skippedTimeBehavior;

        /// <summary>
        ///   Duplicate-trigger policy from registration options (same resolution as
        ///   <see cref="ClockDayTimeTimerRegistration"/>).
        /// </summary>
        private readonly DuplicateTimeBehavior _duplicateTimeBehavior;
        //------------------------------------------------------------------------

        //------------------------------------------------------------------------
        /// <summary>
        ///   Initializes base state for a virtual day-time timer registration.
        /// </summary>
        /// <param name="clock">The owning test clock.</param>
        /// <param name="isLocal"><c>true</c> for local time-of-day; <c>false</c> for UTC.</param>
        /// <param name="targetTimeOfDay">Time of day since midnight in the chosen basis.</param>
        /// <param name="callbackKind">Callback delegate shape.</param>
        /// <param name="callback">The callback delegate.</param>
        /// <param name="callbackState">Optional callback state.</param>
        /// <param name="options">Day-time options, or <c>null</c> for defaults.</param>
        /// <param name="cancellationToken">Token that cancels this registration.</param>
        protected VirtualDayTimeTimerBase (PrimeTestClock clock,
            bool isLocal,
            TimeSpan targetTimeOfDay,
            TimerCallbackKind callbackKind,
            Delegate callback,
            object? callbackState,
            DayTimeTimerOptions? options,
            CancellationToken cancellationToken)
        {
            Clock = clock;
            IsLocal = isLocal;
            TargetTimeOfDay = targetTimeOfDay;
            CallbackKind = callbackKind;
            Callback = callback;
            CallbackState = callbackState;
            CancellationToken = cancellationToken;
            DayTimeTimerOptions resolvedOptions = options ?? new DayTimeTimerOptions();
            _concurrentTriggerProcessing = resolvedOptions.ConcurrentTriggerProcessing;
            _skippedTimeBehavior = resolvedOptions.SkippedTimeBehavior;
            _duplicateTimeBehavior = resolvedOptions.DuplicateTimeBehavior;
            Id = Interlocked.Increment(ref _nextTimerId);
            RegisteredTime = Clock.UtcNowDateTimeOffset;
            NextDueUtc = ComputeNextDue(clock.UtcNowDateTimeOffset);

            if (!cancellationToken.CanBeCanceled)
            {
                return;
            }

            cancellationToken.Register(OnCancelRequested);
            if (cancellationToken.IsCancellationRequested)
            {
                CancelRequested = true;
                State = TimerState.Cancelled;
                EnabledDayTime = false;
            }
        }
        //------------------------------------------------------------------------

        //------------------------------------------------------------------------
        /// <inheritdoc />
        public int Id { [DebuggerStepThrough] get; }
        //------------------------------------------------------------------------

        //------------------------------------------------------------------------
        /// <inheritdoc />
        public DateTimeOffset RegisteredTime { [DebuggerStepThrough] get; }
        //------------------------------------------------------------------------

        //------------------------------------------------------------------------
        /// <inheritdoc />
        public bool IsTimeOfDay => true;
        //------------------------------------------------------------------------
        /// <inheritdoc />
        public bool IsLocalTimeRepresentation => IsLocal;
        //------------------------------------------------------------------------
        /// <inheritdoc />
        public bool IsRepeating => true;
        //------------------------------------------------------------------------
        /// <inheritdoc />
        public bool IsCancelled => State == TimerState.Cancelled;
        //------------------------------------------------------------------------
        /// <inheritdoc />
        public bool IsActive
        {
            get
            {
                // Capture local state value
                TimerState state = State;
                return state != TimerState.Cancelled &&
                       state != TimerState.Disposed;
            }
        }
        //------------------------------------------------------------------------
        /// <inheritdoc />
        public bool CallbacksProcessing
        {
            get
            {
                lock (Gate)
                {
                    TimerState state = State;
                    return state != TimerState.Cancelled &&
                           state != TimerState.Disposed &&
                           CallbacksRunning > 0;
                }
            }
        }
        //------------------------------------------------------------------------
        /// <inheritdoc />
        public ConcurrentTriggerProcessing ConcurrentTriggerProcessing { [DebuggerStepThrough] get => _concurrentTriggerProcessing; }
        //------------------------------------------------------------------------
        /// <inheritdoc />
        public SkippedTimeBehavior SkippedTimeBehavior { [DebuggerStepThrough] get => _skippedTimeBehavior; }
        //------------------------------------------------------------------------
        /// <inheritdoc />
        public DuplicateTimeBehavior DuplicateTimeBehavior { [DebuggerStepThrough] get => _duplicateTimeBehavior; }
        //------------------------------------------------------------------------
        /// <inheritdoc />
        public bool Enabled
        {
            get => EnabledDayTime && !IsCancelled && State != TimerState.Disposed;
            set
            {
                lock (Gate)
                {
                    if (Disposed || State == TimerState.Cancelled)
                        return;
                    if (value)
                    {
                        EnabledDayTime = true;
                        State = TimerState.Active;
                        NextDueUtc = ComputeNextDue(Clock.UtcNowDateTimeOffset);
                    }
                    else
                    {
                        EnabledDayTime = false;
                        State = TimerState.Disabled;
                    }
                }
            }
        }
        //------------------------------------------------------------------------
        /// <inheritdoc />
        TimerState IClockTimer.State => State;
        //------------------------------------------------------------------------
        /// <summary>
        ///   Returns a <see cref="DateTimeOffset"/> for <paramref name="virtualUtcInstant"/> in this registration's
        ///   coordinate system: local-offset when <see cref="IsLocal"/> (via <see cref="PrimeTestClock.ToLocalOffset"/>),
        ///   otherwise UTC-offset unchanged—so <see cref="ElapsedTime"/> and <see cref="LastCallbackAt"/> compare like
        ///   with like.
        /// </summary>
        /// <param name="virtualUtcInstant">The virtual instant (typically <see cref="PrimeTestClock.UtcNowDateTimeOffset"/>).</param>
        /// <returns>
        ///   The same instant expressed in the offset form that matches local versus UTC day-time registration.
        /// </returns>
        protected DateTimeOffset GetOffsetRepresentationForTimer (DateTimeOffset virtualUtcInstant) =>
            IsLocal ? Clock.ToLocalOffset(virtualUtcInstant) : virtualUtcInstant;
        //------------------------------------------------------------------------
        /// <inheritdoc />
        public long ElapsedTime
        {
            get
            {
                lock (Gate)
                {
                    if (LastCallbackAt is not { } last)
                        return -1;
                    if (CallbacksRunning > 0)
                        return 0;
                    DateTimeOffset now = GetOffsetRepresentationForTimer(Clock.UtcNowDateTimeOffset);
                    if (last >= now)
                        return 0;
                    return (long)(now - last).TotalMilliseconds;
                }
            }
        }
        //------------------------------------------------------------------------
        /// <inheritdoc />
        public long TimeUntilNextCallback
        {
            get
            {
                lock (Gate)
                {
                    if (NextDueUtc is not { } next)
                        return -1;
                    // NextDueUtc is stored as a UTC instant; use virtual UTC now for the same representation.
                    DateTimeOffset now = Clock.UtcNowDateTimeOffset;
                    if (next <= now)
                        return 0;
                    return (long)(next - now).TotalMilliseconds;
                }
            }
        }
        //------------------------------------------------------------------------
        /// <summary>
        ///   Returns whether this day-time registration is due at the given virtual UTC instant.
        /// </summary>
        /// <param name="now">Current virtual UTC time from the test clock.</param>
        /// <returns>
        ///   <c>true</c> if a callback should run at <paramref name="now"/>; otherwise <c>false</c>.
        /// </returns>
        public bool IsDue (DateTimeOffset now)
        {
            lock (Gate)
            {
                if (Disposed || CancelRequested || State == TimerState.Cancelled || !EnabledDayTime)
                    return false;
                if (NextDueUtc is not { } next)
                    return false;
                return next <= now;
            }
        }
        //------------------------------------------------------------------------
        /// <summary>
        ///   Updates <paramref name="bestUtc"/> with this registration&apos;s <see cref="NextDueUtc"/> when it lies in
        ///   <c>(<paramref name="nowUtc"/>, <paramref name="targetUtc"/>]</c>.
        /// </summary>
        /// <remarks>
        ///   <para>
        ///     The caller must already hold <see cref="PrimeTestTimeBase.Gate"/> on the owning <see cref="PrimeTestClock"/>.
        ///     This method then acquires this registration&apos;s per-timer <c>Gate</c> (a different synchronization object
        ///     from the clock gate) while reading <see cref="NextDueUtc"/>.
        ///   </para>
        /// </remarks>
        /// <param name="bestUtc">The best (minimum) candidate instant discovered so far, or <c>null</c> if none.</param>
        /// <param name="nowUtc">Current virtual UTC instant.</param>
        /// <param name="targetUtc">Inclusive upper bound for the next march instant.</param>
        internal void ConsiderEarliestDueUtcStrictlyAfterForMarch(ref DateTimeOffset? bestUtc, DateTimeOffset nowUtc,
            DateTimeOffset targetUtc)
        {
            lock (Gate)
            {
                if (Disposed || CancelRequested || State == TimerState.Cancelled || !EnabledDayTime)
                {
                    return;
                }

                if (NextDueUtc is not { } nextDueUtc)
                {
                    return;
                }

                if (nextDueUtc <= nowUtc)
                {
                    return;
                }

                if (nextDueUtc > targetUtc)
                {
                    return;
                }

                bestUtc = bestUtc is null || nextDueUtc < bestUtc ? nextDueUtc : bestUtc;
            }
        }
        //------------------------------------------------------------------------
        /// <summary>
        ///   Runs the day-time callback when due at <paramref name="now"/>.
        /// </summary>
        /// <param name="now">Virtual UTC instant from <see cref="PrimeTestClock.Advance(System.TimeSpan)"/>.</param>
        public abstract void RunDueCallback (DateTimeOffset now);
        //------------------------------------------------------------------------
        /// <summary>
        ///   Recomputes <see cref="NextDueUtc"/> after a permitted backward adjustment of the test clock&apos;s virtual
        ///   instant.
        /// </summary>
        /// <param name="virtualNowUtc">The clock&apos;s virtual UTC instant after the adjustment.</param>
        /// <remarks>
        ///   The caller must hold the clock <see cref="PrimeTestTimeBase.Gate"/>; this method acquires this
        ///   registration&apos;s <see cref="Gate"/>.
        /// </remarks>
        internal void RecomputeNextDueUtcAfterPermittedBackwardJump (DateTimeOffset virtualNowUtc)
        {
            lock (Gate)
            {
                if (Disposed || CancelRequested || State == TimerState.Cancelled || !EnabledDayTime)
                {
                    return;
                }

                NextDueUtc = ComputeNextDue(virtualNowUtc);
            }
        }
        //------------------------------------------------------------------------
        /// <summary>
        ///   Computes the next UTC instant when <see cref="TargetTimeOfDay"/> should fire on or after the clock&apos;s
        ///   current instant, using the same local wall-time policies as production
        ///   (<c>DayTimeBclLocalWallTimeScheduling</c> on the System Clock stack and <c>DayTimeNodaLocalWallTimeScheduling</c> in the
        ///   superset assembly).
        /// </summary>
        /// <param name="now">Current virtual UTC instant (used for UTC day-time; local scheduling uses the clock&apos;s local &quot;now&quot;).</param>
        /// <returns>The next scheduled fire instant in UTC, or <c>null</c> if not applicable.</returns>
        /// <remarks>
        ///   <para>
        ///     For <see cref="IsLocal"/> on the System Clock stack, the schedule zone is <c>IPrimeClock.LocalScheduleTimeZone</c>
        ///     (exposed on the System Clock build of <see cref="PrimeTestClock"/>), which is not injectable on
        ///     <see cref="PrimeTestClock"/>; deterministic DST edge tests should use the Noda <see cref="PrimeTestClock"/>
        ///     constructor that accepts a <c>NodaTime.DateTimeZone</c>.
        ///   </para>
        /// </remarks>
        protected DateTimeOffset? ComputeNextDue (DateTimeOffset now)
        {
            if (IsLocal)
            {
#if SYSTEMCLOCK
                TimeSpan delay = DayTimeBclLocalWallTimeScheduling.GetDelayUntilNextLocalDayTime(
                    Clock.LocalNowDateTimeOffset,
                    Clock.LocalScheduleTimeZone,
                    TimeOnly.FromTimeSpan(TargetTimeOfDay),
                    _skippedTimeBehavior,
                    _duplicateTimeBehavior);
                if (delay <= TimeSpan.Zero)
                {
                    delay = TimeSpan.FromDays(1);
                }

                DateTimeOffset scheduleBasis = Clock.LocalNowDateTimeOffset;
                return new DateTimeOffset((scheduleBasis + delay).UtcDateTime, TimeSpan.Zero);
#else
                LocalTime targetLocal = LocalTime.FromTicksSinceMidnight(TargetTimeOfDay.Ticks);
                Duration delayNoda = DayTimeNodaLocalWallTimeScheduling.GetDelayUntilNextLocalDayTime(
                    Clock.NowInstant,
                    Clock.TimeZone,
                    targetLocal,
                    _skippedTimeBehavior,
                    _duplicateTimeBehavior);
                if (delayNoda <= Duration.Zero)
                {
                    delayNoda = Duration.FromDays(1);
                }

                Instant fireInstant = Clock.NowInstant + delayNoda;
                return new DateTimeOffset(fireInstant.ToDateTimeUtc(), TimeSpan.Zero);
#endif
            }
            else
            {
                DateTime utcDate = now.UtcDateTime;
                DateTime utcMidnight = utcDate.Date;
                DateTime nextDt = utcMidnight + TargetTimeOfDay;
                if (nextDt <= utcDate)
                    nextDt = nextDt.AddDays(1);
                return new DateTimeOffset(DateTime.SpecifyKind(nextDt, DateTimeKind.Utc), TimeSpan.Zero);
            }
        }
        //------------------------------------------------------------------------
        /// <summary>
        ///   Handles cancellation: marks the registration cancelled and removes it from the clock.
        /// </summary>
        protected void OnCancelRequested ()
        {
            lock (Gate)
            {
                if (Disposed || State == TimerState.Cancelled)
                    return;
                CancelRequested = true;
                State = TimerState.Cancelled;
                EnabledDayTime = false;
            }

            Clock.RemoveDayTimeTimer(this);
        }
        //------------------------------------------------------------------------

#if NET
        /// <inheritdoc />
        public bool Change (LocalTimeOfDay newTimeOfDay)
        {
            if (!IsLocal)
                return false;
            lock (Gate)
            {
                if (Disposed || State == TimerState.Cancelled)
                    return false;
                TargetTimeOfDay = newTimeOfDay.Value.ToTimeSpan();
                if (Enabled)
                    NextDueUtc = ComputeNextDue(Clock.UtcNowDateTimeOffset);
                return true;
            }
        }

        /// <inheritdoc />
        public bool Change (UtcTimeOfDay newTimeOfDay)
        {
            if (IsLocal)
                return false;
            lock (Gate)
            {
                if (Disposed || State == TimerState.Cancelled)
                    return false;
                TargetTimeOfDay = newTimeOfDay.Value.ToTimeSpan();
                if (Enabled)
                    NextDueUtc = ComputeNextDue(Clock.UtcNowDateTimeOffset);
                return true;
            }
        }
#endif

        //------------------------------------------------------------------------
        /// <inheritdoc />
        public void Cancel ()
        {
            lock (Gate)
            {
                if (State == TimerState.Cancelled || Disposed)
                    return;
                CancelRequested = true;
                State = TimerState.Cancelled;
                Enabled = false;
            }

            Clock.RemoveDayTimeTimer(this);
        }
        //------------------------------------------------------------------------

        //------------------------------------------------------------------------
        /// <inheritdoc />
        public bool Stop ()
        {
            lock (Gate)
            {
                if (!EnabledDayTime || State == TimerState.Cancelled || Disposed)
                    return false;
                EnabledDayTime = false;
                State = TimerState.Disabled;
                return true;
            }
        }
        //------------------------------------------------------------------------

        //------------------------------------------------------------------------
        /// <inheritdoc />
        public bool Start ()
        {
            lock (Gate)
            {
                if (Disposed || State == TimerState.Cancelled)
                    return false;
                if (State != TimerState.Completed && State != TimerState.Disabled)
                    return false;
                EnabledDayTime = true;
                State = TimerState.Active;
                NextDueUtc = ComputeNextDue(Clock.UtcNowDateTimeOffset);
                return true;
            }
        }
        //------------------------------------------------------------------------

        //------------------------------------------------------------------------
        /// <inheritdoc />
        public void Dispose ()
        {
            lock (Gate)
            {
                if (Disposed)
                    return;
                Disposed = true;
                State = TimerState.Disposed;
                EnabledDayTime = false;
            }

            Clock.RemoveDayTimeTimer(this);
        }
        //------------------------------------------------------------------------

        #region IAsyncDisposable Implementation

        //------------------------------------------------------------------------
        /// <inheritdoc />
        public ValueTask DisposeAsync ()
        {
            Dispose();
#if NET
            return ValueTask.CompletedTask;
#else
            return new ValueTask();
#endif
        }
        //------------------------------------------------------------------------

        #endregion IAsyncDisposable Implementation
    }
    //============================================================================

    //============================================================================
    /// <summary>
    ///   Virtual day-time timer that invokes the user callback when the scheduled time-of-day is reached.
    /// </summary>
    private sealed partial class VirtualDayTimeTimer : VirtualDayTimeTimerBase
    {
        //------------------------------------------------------------------------
        /// <summary>
        ///   Initializes a new instance of the <see cref="VirtualDayTimeTimer"/> class.
        /// </summary>
        /// <param name="clock">The owning test clock.</param>
        /// <param name="isLocal"><c>true</c> for local time-of-day; <c>false</c> for UTC.</param>
        /// <param name="targetTimeOfDay">Time of day since midnight in the chosen basis.</param>
        /// <param name="callbackKind">Callback delegate shape.</param>
        /// <param name="callback">The callback delegate.</param>
        /// <param name="callbackState">Optional callback state.</param>
        /// <param name="options">Day-time options, or <c>null</c> for defaults.</param>
        /// <param name="cancellationToken">Token that cancels this registration.</param>
        public VirtualDayTimeTimer (PrimeTestClock clock,
            bool isLocal,
            TimeSpan targetTimeOfDay,
            TimerCallbackKind callbackKind,
            Delegate callback,
            object? callbackState,
            DayTimeTimerOptions? options,
            CancellationToken cancellationToken)
            : base(clock, isLocal, targetTimeOfDay, callbackKind, callback, callbackState, options, cancellationToken)
        {
        }
        //------------------------------------------------------------------------

        //------------------------------------------------------------------------
        /// <inheritdoc />
        public override void RunDueCallback (DateTimeOffset now)
        {
            lock (Gate)
            {
                if (Disposed || CancelRequested || State == TimerState.Cancelled || !EnabledDayTime)
                    return;
                if (NextDueUtc is not { } next || next > now)
                    return;
                LastCallbackAt = GetOffsetRepresentationForTimer(now);
                NextDueUtc = ComputeNextDue(now);
                State = TimerState.RepeatProcessingCallback;
                CallbacksRunning++;
            }

            try
            {
                RunCallback();
            }
            finally
            {
                lock (Gate)
                {
                    CallbacksRunning--;
                    State = TimerState.RepeatCycle;
                }
            }
        }
        //------------------------------------------------------------------------

        //------------------------------------------------------------------------
        /// <summary>
        ///   Invokes the registered synchronous or async-blocking callback.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        ///   <see cref="VirtualDayTimeTimerBase.CallbackKind"/> is not supported.
        /// </exception>
        private void RunCallback ()
        {
            switch (CallbackKind)
            {
                case TimerCallbackKind.SimpleAction:
                    ((Action)Callback)();
                    break;
                case TimerCallbackKind.ContextAction:
                    ((Action<ClockTimerCallbackContext>)Callback)(new ClockTimerCallbackContext(this, CallbackState));
                    break;
                case TimerCallbackKind.ContextActionWithToken:
                    ((Action<ClockTimerCallbackContext, CancellationToken>)Callback)(new ClockTimerCallbackContext(this, CallbackState),
                        CancellationToken);
                    break;
                case TimerCallbackKind.SimpleAsync:
                    ((Func<CancellationToken, ValueTask>)Callback)(CancellationToken).AsTask().GetAwaiter().GetResult();
                    break;
                case TimerCallbackKind.ContextAsync:
                    ((Func<ClockTimerCallbackContext, CancellationToken, ValueTask>)Callback)(new ClockTimerCallbackContext(this, CallbackState),
                        CancellationToken)
                        .AsTask().GetAwaiter().GetResult();
                    break;
                default:
                    throw new InvalidOperationException($"Unsupported callback kind: {CallbackKind}");
            }
        }
        //------------------------------------------------------------------------
    }
    //============================================================================

    #endregion Nested types — Virtual day-time timer
#endif
}
//################################################################################


