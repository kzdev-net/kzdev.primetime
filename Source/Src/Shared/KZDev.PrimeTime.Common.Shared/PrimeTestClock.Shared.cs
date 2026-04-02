// Copyright (c) Kevin Zehrer. All rights reserved.
// This file is part of the PrimeTime project.

using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

#if SYSTEMCLOCK
namespace KZDev.SystemClock.PrimeTime;
#else
using NodaTime;

namespace KZDev.PrimeTime;
#endif

/// <summary>
///   Shared implementation of <see cref="IPrimeTestClock"/> virtual time, delays,
///   cancellation entries, and virtual interval/day-time timers. Stack-specific instant
///   storage and local-time mapping live in partials.
/// </summary>
public sealed partial class PrimeTestClock : IPrimeTestClock
{
    private partial DateTimeOffset ToLocalOffset (DateTimeOffset utcNowOffset);

    /// <summary>
    ///   Returns the UTC offset for a local wall-clock calendar <see cref="DateTime"/> in the same time zone
    ///   the clock uses for <see cref="LocalNowOffset"/>.
    /// </summary>
    /// <param name="localUnspecified">Local date and time with <see cref="DateTime.Kind"/> <see cref="DateTimeKind.Unspecified"/>.</param>
    /// <returns>The offset from UTC at that local wall time.</returns>
    private partial TimeSpan GetLocalWallClockUtcOffset (DateTime localUnspecified);

    private partial void SetVirtualUtcNowLocked (DateTimeOffset utcNowOffset);

    private partial DateTimeOffset ReadVirtualUtcNowLocked ();

    private partial void AddVirtualTimeLocked (TimeSpan duration);

    private partial void RaiseClockEventsAfterVirtualUtcChange (DateTimeOffset utcNowOffset);

    #region Local time-of-day scheduling

    /// <summary>
    ///   Computes the UTC instant of the next occurrence of <paramref name="targetTimeOfDay"/> on or after
    ///   <paramref name="localNow"/>, using <paramref name="getLocalWallClockUtcOffset"/> to resolve the UTC offset for
    ///   the scheduled local wall-clock instant (including across daylight saving time transitions).
    /// </summary>
    /// <param name="localNow">
    ///   Current time in the local coordinate system (same interpretation as <see cref="LocalNowOffset"/>).
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
        // Delegate returns the UTC offset for that wall time: for ambiguous times it applies the zone's resolution
        // rules; for skipped times it maps to a defined instant (for example, per BCL or Noda lenient mapping).
        TimeSpan nextLocalOffset = getLocalWallClockUtcOffset(nextLocalUnspecified);
        // Wall clock plus resolved offset is fixed; convert to UTC so the scheduled instant is unambiguous downstream.
        DateTimeOffset nextLocal = new(nextLocalUnspecified, nextLocalOffset);
        return nextLocal.ToUniversalTime();
    }

    #endregion Local time-of-day scheduling

    #region Nested types — Pending delay and time expiry

    private sealed class PendingDelay
    {
        public DateTimeOffset DueUtc { [DebuggerStepThrough] get; }
        public TaskCompletionSource<bool> TaskCompletionSource { [DebuggerStepThrough] get; }

        public PendingDelay (DateTimeOffset dueUtc, TaskCompletionSource<bool> taskCompletionSource)
        {
            DueUtc = dueUtc;
            TaskCompletionSource = taskCompletionSource;
        }

        public void Complete ()
        {
            TaskCompletionSource.TrySetResult(true);
        }
    }

    private sealed class TimeExpiryEntry
    {
        public DateTimeOffset ExpireUtc { [DebuggerStepThrough] get; }
        private readonly TimeCancellationTokenSource _wrapper;

        public TimeExpiryEntry (DateTimeOffset expireUtc, TimeCancellationTokenSource wrapper,
            CancellationTokenSource? timeCts = null)
        {
            ExpireUtc = expireUtc;
            _wrapper = wrapper;
        }

        public void Cancel ()
        {
            try
            {
                _wrapper.Cancel();
            }
            catch (ObjectDisposedException)
            {
                // Caller may have disposed already.
            }
        }
    }

    #endregion Nested types — Pending delay and time expiry

    #region Nested types — Virtual interval timer

    private abstract partial class VirtualIntervalTimerBase : IClockIntervalTimer
    {
        protected PrimeTestClock Clock { [DebuggerStepThrough] get; }
        protected IntervalTimerCallbackKind CallbackKind { [DebuggerStepThrough] get; }
        protected Delegate Callback { [DebuggerStepThrough] get; }
        protected object? CallbackState { [DebuggerStepThrough] get; }
        protected CancellationToken CancellationToken { [DebuggerStepThrough] get; }
#if NET10_OR_GREATER
        protected Lock Gate { [DebuggerStepThrough] get; } = new();
#else
        protected object Gate { [DebuggerStepThrough] get; } = new();
#endif
        protected DateTimeOffset? NextDueUtc { [DebuggerStepThrough] get; [DebuggerStepThrough] set; }
        protected DateTimeOffset? LastCallbackUtc { [DebuggerStepThrough] get; [DebuggerStepThrough] set; }
        protected TimerState State { [DebuggerStepThrough] get; [DebuggerStepThrough] set; } = TimerState.Active;
        protected bool IntervalTimerEnabled { [DebuggerStepThrough] get; [DebuggerStepThrough] private set; } = true;
        protected bool Disposed { [DebuggerStepThrough] get; [DebuggerStepThrough] set; }
        protected bool CancelRequested { [DebuggerStepThrough] get; [DebuggerStepThrough] set; }
        protected int CallbacksRunning { [DebuggerStepThrough] get; [DebuggerStepThrough] set; }
        protected TimeSpan InitialCallbackTime { [DebuggerStepThrough] get; [DebuggerStepThrough] set; }
        protected TimeSpan RepeatInterval { [DebuggerStepThrough] get; [DebuggerStepThrough] set; }
        private readonly bool _isLocalTimeRepresentation;
        private readonly bool _resetAfterCallback;

        protected VirtualIntervalTimerBase (PrimeTestClock clock,
            TimeSpan initialCallbackTime,
            TimeSpan repeatInterval,
            IntervalTimerCallbackKind callbackKind,
            Delegate callback,
            object? callbackState,
            IntervalTimerOptions? options,
            CancellationToken cancellationToken)
        {
            Clock = clock;
            InitialCallbackTime = initialCallbackTime;
            RepeatInterval = repeatInterval;
            CallbackKind = callbackKind;
            Callback = callback;
            CallbackState = callbackState;
            CancellationToken = cancellationToken;
            _isLocalTimeRepresentation = options?.LocalTimeRepresentation == true;
            _resetAfterCallback = options?.ResetIntervalAfterCallback ?? false;
            Id = Interlocked.Increment(ref _nextTimerId);
            DateTimeOffset now = clock.UtcNowOffset;
            RegisteredTime = _isLocalTimeRepresentation ? clock.LocalNowOffset : now;
            NextDueUtc = now + initialCallbackTime;

            if (cancellationToken.CanBeCanceled)
            {
                cancellationToken.Register(OnCancelRequested);
                if (cancellationToken.IsCancellationRequested)
                {
                    CancelRequested = true;
                    State = TimerState.Cancelled;
                    IntervalTimerEnabled = false;
                }
            }
        }

        public int Id { [DebuggerStepThrough] get; }
        public DateTimeOffset RegisteredTime { [DebuggerStepThrough] get; }
        public bool IsTimeOfDay => false;
        public bool IsRepeating => RepeatInterval != Timeout.InfiniteTimeSpan && RepeatInterval > TimeSpan.Zero;
        public bool IsResetAfterCallback => _resetAfterCallback && IsRepeating;
        public bool IsCancelled => State == TimerState.Cancelled;
        public bool IsLocalTimeRepresentation => _isLocalTimeRepresentation;
        public bool IsActive =>
            State != TimerState.Cancelled && State != TimerState.Disposed && IntervalTimerEnabled;
        public bool CallbacksProcessing => CallbacksRunning > 0;

        public bool Enabled
        {
            get => IntervalTimerEnabled && !IsCancelled && State != TimerState.Disposed;
            set
            {
                lock (Gate)
                {
                    if (Disposed || State == TimerState.Cancelled)
                        return;
                    if (value)
                        Start();
                    else
                        Stop();
                }
            }
        }

        TimerState IClockTimer.State => State;

        public long ElapsedTime
        {
            get
            {
                lock (Gate)
                {
                    if (LastCallbackUtc is not { } last)
                        return -1;
                    if (CallbacksRunning > 0)
                        return 0;
                    DateTimeOffset now = Clock.UtcNowOffset;
                    if (last >= now)
                        return 0;
                    return (long)(now - last).TotalMilliseconds;
                }
            }
        }

        public long TimeUntilNextCallback
        {
            get
            {
                lock (Gate)
                {
                    if (!IsRepeating && LastCallbackUtc.HasValue)
                        return -1;
                    if (NextDueUtc is not { } next)
                        return -1;
                    DateTimeOffset now = Clock.UtcNowOffset;
                    if (next <= now)
                        return 0;
                    if (CallbacksRunning > 0 && IsResetAfterCallback)
                        return (long)RepeatInterval.TotalMilliseconds;
                    return (long)(next - now).TotalMilliseconds;
                }
            }
        }

        public bool IsDue (DateTimeOffset now)
        {
            lock (Gate)
            {
                if (Disposed || CancelRequested || State == TimerState.Cancelled || !IntervalTimerEnabled)
                    return false;
                if (NextDueUtc is not { } next)
                    return false;
                return next <= now;
            }
        }

        public abstract void RunDueCallback (DateTimeOffset now);

        protected void OnCancelRequested ()
        {
            lock (Gate)
            {
                if (Disposed || State == TimerState.Cancelled)
                    return;
                CancelRequested = true;
                State = TimerState.Cancelled;
                IntervalTimerEnabled = false;
            }

            Clock.RemoveIntervalTimer(this);
        }

        public bool Change (TimeSpan interval) =>
            Change(interval, IsRepeating ? interval : Timeout.InfiniteTimeSpan);

        public bool Change (TimeSpan nextInterval, TimeSpan repeatInterval)
        {
            lock (Gate)
            {
                if (Disposed || State == TimerState.Cancelled)
                    return false;
                if (!IsRepeating && repeatInterval != Timeout.InfiniteTimeSpan && repeatInterval > TimeSpan.Zero)
                    throw new InvalidOperationException("Cannot change a non-repeating timer to a repeating timer.");
                InitialCallbackTime = nextInterval;
                RepeatInterval = repeatInterval;
                if (State == TimerState.Completed)
                    State = TimerState.Active;
                if (!IntervalTimerEnabled)
                    return true;
                NextDueUtc = Clock.UtcNowOffset + nextInterval;
                return true;
            }
        }

        public void Cancel ()
        {
            lock (Gate)
            {
                if (State == TimerState.Cancelled || Disposed)
                    return;
                CancelRequested = true;
                State = TimerState.Cancelled;
                IntervalTimerEnabled = false;
            }

            Clock.RemoveIntervalTimer(this);
        }

        public bool Stop ()
        {
            lock (Gate)
            {
                if (!IntervalTimerEnabled || State == TimerState.Cancelled || Disposed)
                    return false;
                IntervalTimerEnabled = false;
                State = TimerState.Disabled;
                return true;
            }
        }

        public bool Start ()
        {
            lock (Gate)
            {
                if (Disposed || State == TimerState.Cancelled)
                    return false;
                if (State != TimerState.Completed && State != TimerState.Disabled)
                    return false;
                IntervalTimerEnabled = true;
                State = TimerState.Active;
                NextDueUtc = Clock.UtcNowOffset + InitialCallbackTime;
                return true;
            }
        }

        public void Dispose ()
        {
            lock (Gate)
            {
                if (Disposed)
                    return;
                Disposed = true;
                State = TimerState.Disposed;
                IntervalTimerEnabled = false;
            }

            Clock.RemoveIntervalTimer(this);
        }
    }

    private sealed partial class VirtualIntervalTimer : VirtualIntervalTimerBase
    {
        public VirtualIntervalTimer (PrimeTestClock clock,
            TimeSpan initialCallbackTime,
            TimeSpan repeatInterval,
            IntervalTimerCallbackKind callbackKind,
            Delegate callback,
            object? callbackState,
            IntervalTimerOptions? options,
            CancellationToken cancellationToken)
            : base(clock, initialCallbackTime, repeatInterval, callbackKind, callback, callbackState, options, cancellationToken)
        {
        }

        public override void RunDueCallback (DateTimeOffset now)
        {
            bool resetAfter;
            bool isRepeating;
            DateTimeOffset firedAt;

            lock (Gate)
            {
                if (Disposed || CancelRequested || State == TimerState.Cancelled || !IntervalTimerEnabled)
                    return;
                if (NextDueUtc is not { } next || next > now)
                    return;
                firedAt = next;
                NextDueUtc = null;
                LastCallbackUtc = firedAt;
                isRepeating = IsRepeating;
                resetAfter = IsResetAfterCallback;
                State = isRepeating && !resetAfter ? TimerState.RepeatProcessingCallback : TimerState.ProcessingCallback;
                CallbacksRunning++;
            }

            try
            {
                RunCallback(resetAfter, isRepeating, firedAt);
            }
            finally
            {
                lock (Gate)
                    CallbacksRunning--;
            }
        }

        private void RunCallback (bool resetAfter, bool isRepeating, DateTimeOffset now)
        {
            void InvokeSync (Action run)
            {
                run();
            }

            switch (CallbackKind)
            {
                case IntervalTimerCallbackKind.SimpleAction:
                    InvokeSync(() => ((Action)Callback)());
                    break;
                case IntervalTimerCallbackKind.ContextAction:
                    InvokeSync(() => ((Action<ClockTimerCallbackContext>)Callback)(new ClockTimerCallbackContext(this, CallbackState)));
                    break;
                case IntervalTimerCallbackKind.ContextActionWithToken:
                    InvokeSync(() => ((Action<ClockTimerCallbackContext, CancellationToken>)Callback)(new ClockTimerCallbackContext(this, CallbackState),
                        CancellationToken));
                    break;
                case IntervalTimerCallbackKind.SimpleAsync:
                    RunAsyncAndScheduleAfter(() => ((Func<CancellationToken, ValueTask>)Callback)(CancellationToken),
                        resetAfter,
                        isRepeating,
                        now);
                    return;
                case IntervalTimerCallbackKind.ContextAsync:
                    RunAsyncAndScheduleAfter(() => ((Func<ClockTimerCallbackContext, CancellationToken, ValueTask>)Callback)(new ClockTimerCallbackContext(this, CallbackState),
                            CancellationToken),
                        resetAfter,
                        isRepeating,
                        now);
                    return;
                default:
                    throw new InvalidOperationException($"Unsupported callback kind: {CallbackKind}");
            }

            OnSyncCallbackCompleted(resetAfter, isRepeating, now);
        }

        private void RunAsyncAndScheduleAfter (Func<ValueTask> run, bool resetAfter, bool isRepeating, DateTimeOffset now)
        {
            ValueTask vt = run();
            if (vt.IsCompletedSuccessfully)
            {
                OnAsyncCallbackCompleted(resetAfter, isRepeating, now);
                return;
            }

            vt.AsTask().ContinueWith((_, state) =>
                {
                    (VirtualIntervalTimer reg, bool ra, bool rep, DateTimeOffset n) =
                        ((VirtualIntervalTimer, bool, bool, DateTimeOffset))state!;
                    reg.OnAsyncCallbackCompleted(ra, rep, n);
                },
                (this, resetAfter, isRepeating, now),
                CancellationToken.None,
                TaskContinuationOptions.None,
                TaskScheduler.Default);
        }

        private void OnSyncCallbackCompleted (bool resetAfter, bool isRepeating, DateTimeOffset now)
        {
            lock (Gate)
            {
                if (Disposed || CancelRequested || State == TimerState.Cancelled)
                    return;
                if (!isRepeating)
                {
                    State = TimerState.Completed;
                    return;
                }

                State = TimerState.RepeatCycle;
                NextDueUtc = now + RepeatInterval;
            }
        }

        private void OnAsyncCallbackCompleted (bool resetAfter, bool isRepeating, DateTimeOffset now)
        {
            lock (Gate)
            {
                if (Disposed || CancelRequested || State == TimerState.Cancelled)
                    return;
                if (!isRepeating)
                {
                    State = TimerState.Completed;
                    return;
                }

                State = TimerState.RepeatCycle;
                NextDueUtc = now + RepeatInterval;
            }
        }
    }

    #endregion Nested types — Virtual interval timer

#if NET || !SYSTEMCLOCK
    #region Nested types — Virtual day-time timer

    private abstract partial class VirtualDayTimeTimerBase : IClockDayTimeTimer
    {
        protected PrimeTestClock Clock { [DebuggerStepThrough] get; }
        protected bool IsLocal { [DebuggerStepThrough] get; }
        protected IntervalTimerCallbackKind CallbackKind { [DebuggerStepThrough] get; }
        protected Delegate Callback { [DebuggerStepThrough] get; }
        protected object? CallbackState { [DebuggerStepThrough] get; }
        protected CancellationToken CancellationToken { [DebuggerStepThrough] get; }
#if NET10_OR_GREATER
        protected Lock Gate { [DebuggerStepThrough] get; } = new();
#else
        protected object Gate { [DebuggerStepThrough] get; } = new();
#endif
        protected DateTimeOffset? NextDueUtc { [DebuggerStepThrough] get; [DebuggerStepThrough] set; }
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
        ///     representation as <see cref="PrimeTestClock.LocalNowOffset"/> (that is, the local
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
        protected TimerState State { [DebuggerStepThrough] get; [DebuggerStepThrough] set; } = TimerState.Active;
        protected bool EnabledDayTime { [DebuggerStepThrough] get; [DebuggerStepThrough] set; } = true;
        protected bool Disposed { [DebuggerStepThrough] get; [DebuggerStepThrough] set; }
        protected bool CancelRequested { [DebuggerStepThrough] get; [DebuggerStepThrough] set; }
        protected int CallbacksRunning { [DebuggerStepThrough] get; [DebuggerStepThrough] set; }
        /// <summary>
        ///   Time of day since local or UTC midnight (depending on <see cref="IsLocal"/>).
        /// </summary>
        protected TimeSpan TargetTimeOfDay { [DebuggerStepThrough] get; [DebuggerStepThrough] set; }
        private readonly ConcurrentTriggerProcessing _concurrentTriggerProcessing;
        private readonly SkippedTimeBehavior _skippedTimeBehavior;
        private readonly DuplicateTimeBehavior _duplicateTimeBehavior;

        protected VirtualDayTimeTimerBase (PrimeTestClock clock,
            bool isLocal,
            TimeSpan targetTimeOfDay,
            IntervalTimerCallbackKind callbackKind,
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
            _concurrentTriggerProcessing = options?.ConcurrentTriggerProcessing ?? ConcurrentTriggerProcessing.RunSequentially;
            _skippedTimeBehavior = options?.SkippedTimeBehavior ?? SkippedTimeBehavior.RunAfter;
            _duplicateTimeBehavior = options?.DuplicateTimeBehavior ?? DuplicateTimeBehavior.RunFirst;
            Id = Interlocked.Increment(ref _nextTimerId);
            RegisteredTime = Clock.UtcNowOffset;
            NextDueUtc = ComputeNextDue(clock.UtcNowOffset);

            if (cancellationToken.CanBeCanceled)
            {
                cancellationToken.Register(OnCancelRequested);
                if (cancellationToken.IsCancellationRequested)
                {
                    CancelRequested = true;
                    State = TimerState.Cancelled;
                    EnabledDayTime = false;
                }
            }
        }

        private static readonly TimeSpan OneDay = TimeSpan.FromDays(1);

        public int Id { [DebuggerStepThrough] get; }
        public DateTimeOffset RegisteredTime { [DebuggerStepThrough] get; }
        public bool IsTimeOfDay => true;
        public bool IsResetAfterCallback => false;
        public bool IsLocalTimeRepresentation => IsLocal;
        public bool IsRepeating => true;
        public bool IsCancelled => State == TimerState.Cancelled;
        public bool IsActive =>
            State != TimerState.Cancelled && State != TimerState.Disposed && EnabledDayTime;
        public bool CallbacksProcessing => CallbacksRunning > 0;
        public ConcurrentTriggerProcessing ConcurrentTriggerProcessing { [DebuggerStepThrough] get => _concurrentTriggerProcessing; }
        public SkippedTimeBehavior SkippedTimeBehavior { [DebuggerStepThrough] get => _skippedTimeBehavior; }
        public DuplicateTimeBehavior DuplicateTimeBehavior { [DebuggerStepThrough] get => _duplicateTimeBehavior; }

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
                        NextDueUtc = ComputeNextDue(Clock.UtcNowOffset);
                    }
                    else
                    {
                        EnabledDayTime = false;
                        State = TimerState.Disabled;
                    }
                }
            }
        }

        TimerState IClockTimer.State => State;

        /// <summary>
        ///   Returns a <see cref="DateTimeOffset"/> for <paramref name="virtualUtcInstant"/> in this registration's
        ///   coordinate system: local-offset when <see cref="IsLocal"/> (via <see cref="PrimeTestClock.ToLocalOffset"/>),
        ///   otherwise UTC-offset unchanged—so <see cref="ElapsedTime"/> and <see cref="LastCallbackAt"/> compare like
        ///   with like.
        /// </summary>
        /// <param name="virtualUtcInstant">The virtual instant (typically <see cref="PrimeTestClock.UtcNowOffset"/>).</param>
        /// <returns>
        ///   The same instant expressed in the offset form that matches local versus UTC day-time registration.
        /// </returns>
        protected DateTimeOffset GetOffsetRepresentationForTimer (DateTimeOffset virtualUtcInstant) =>
            IsLocal ? Clock.ToLocalOffset(virtualUtcInstant) : virtualUtcInstant;

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
                    DateTimeOffset now = GetOffsetRepresentationForTimer(Clock.UtcNowOffset);
                    if (last >= now)
                        return 0;
                    return (long)(now - last).TotalMilliseconds;
                }
            }
        }

        public long TimeUntilNextCallback
        {
            get
            {
                lock (Gate)
                {
                    if (NextDueUtc is not { } next)
                        return -1;
                    // NextDueUtc is stored as a UTC instant; use virtual UTC now for the same representation.
                    DateTimeOffset now = Clock.UtcNowOffset;
                    if (next <= now)
                        return 0;
                    return (long)(next - now).TotalMilliseconds;
                }
            }
        }

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

        public abstract void RunDueCallback (DateTimeOffset now);

        protected DateTimeOffset? ComputeNextDue (DateTimeOffset now)
        {
            if (IsLocal)
            {
                return ComputeNextLocalTimeOfDayAsUtc(Clock.LocalNowOffset, TargetTimeOfDay,
                    dt => Clock.GetLocalWallClockUtcOffset(dt));
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

#if NET
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
                    NextDueUtc = ComputeNextDue(Clock.UtcNowOffset);
                return true;
            }
        }

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
                    NextDueUtc = ComputeNextDue(Clock.UtcNowOffset);
                return true;
            }
        }
#endif

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
                NextDueUtc = ComputeNextDue(Clock.UtcNowOffset);
                return true;
            }
        }

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
    }

    private sealed partial class VirtualDayTimeTimer : VirtualDayTimeTimerBase
    {
        public VirtualDayTimeTimer (PrimeTestClock clock,
            bool isLocal,
            TimeSpan targetTimeOfDay,
            IntervalTimerCallbackKind callbackKind,
            Delegate callback,
            object? callbackState,
            DayTimeTimerOptions? options,
            CancellationToken cancellationToken)
            : base(clock, isLocal, targetTimeOfDay, callbackKind, callback, callbackState, options, cancellationToken)
        {
        }

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

        private void RunCallback ()
        {
            switch (CallbackKind)
            {
                case IntervalTimerCallbackKind.SimpleAction:
                    ((Action)Callback)();
                    break;
                case IntervalTimerCallbackKind.ContextAction:
                    ((Action<ClockTimerCallbackContext>)Callback)(new ClockTimerCallbackContext(this, CallbackState));
                    break;
                case IntervalTimerCallbackKind.ContextActionWithToken:
                    ((Action<ClockTimerCallbackContext, CancellationToken>)Callback)(new ClockTimerCallbackContext(this, CallbackState),
                        CancellationToken);
                    break;
                case IntervalTimerCallbackKind.SimpleAsync:
                    ((Func<CancellationToken, ValueTask>)Callback)(CancellationToken).AsTask().GetAwaiter().GetResult();
                    break;
                case IntervalTimerCallbackKind.ContextAsync:
                    ((Func<ClockTimerCallbackContext, CancellationToken, ValueTask>)Callback)(new ClockTimerCallbackContext(this, CallbackState),
                        CancellationToken)
                        .AsTask().GetAwaiter().GetResult();
                    break;
                default:
                    throw new InvalidOperationException($"Unsupported callback kind: {CallbackKind}");
            }
        }
    }

    #endregion Nested types — Virtual day-time timer
#endif

#if !SYSTEMCLOCK
    #region Nested types — Noda virtual interval extensions

    /// <summary>
    ///   NodaTime <see cref="Duration"/> and <see cref="Instant"/> surface for shared virtual interval timers.
    /// </summary>
    private abstract partial class VirtualIntervalTimerBase
    {
        private static readonly Duration NoRepeatSentinel = Duration.FromTimeSpan(Timeout.InfiniteTimeSpan);

        /// <inheritdoc />
        public Instant RegisteredInstant => Instant.FromDateTimeOffset(RegisteredTime);

        /// <inheritdoc />
        public bool Change (Duration interval) =>
            Change(interval.ToTimeSpan(), IsRepeating ? interval.ToTimeSpan() : Timeout.InfiniteTimeSpan);

        /// <inheritdoc />
        public bool Change (Duration nextInterval, Duration repeatInterval)
        {
            TimeSpan repeatTs = repeatInterval == NoRepeatSentinel ? Timeout.InfiniteTimeSpan : repeatInterval.ToTimeSpan();
            return Change(nextInterval.ToTimeSpan(), repeatTs);
        }
    }

    #endregion Nested types — Noda virtual interval extensions
#endif

    private static int _nextTimerId;
#if NET10_OR_GREATER
    private readonly Lock _gate = new();
#else
    private readonly object _gate = new();
#endif
    private bool _isRunning;
    private Thread? _runThread;
    private TimeSpan _runRate = TimeSpan.FromSeconds(1);
    private readonly List<PendingDelay> _pendingDelays = [];
    private readonly List<TimeExpiryEntry> _timeExpiryEntries = [];
    private readonly List<VirtualIntervalTimerBase> _intervalTimers = [];
#if NET || !SYSTEMCLOCK
    private readonly List<VirtualDayTimeTimerBase> _dayTimeTimers = [];
#endif


    /// <summary>
    ///   Occurs when the clock's current time has changed.
    /// </summary>
    public event EventHandler<ClockTimeChangedEventArgs>? ClockEvents;

    #region Private helpers

    private void RunLoop ()
    {
        while (true)
        {
            Thread.Sleep(1000);
            TimeSpan toAdvance;
            lock (_gate)
            {
                if (!_isRunning)
                    return;
                toAdvance = _runRate;
            }

            Advance(toAdvance);
        }
    }

    private void RemoveIntervalTimer (VirtualIntervalTimerBase timer)
    {
        lock (_gate)
            _intervalTimers.Remove(timer);
    }

#if NET || !SYSTEMCLOCK
    private void RemoveDayTimeTimer (VirtualDayTimeTimerBase timer)
    {
        lock (_gate)
            _dayTimeTimers.Remove(timer);
    }

    private IClockDayTimeTimer RegisterTimeOfDayLocal (TimeSpan targetTimeOfDay,
        IntervalTimerCallbackKind kind,
        Delegate callback,
        object? state,
        DayTimeTimerOptions? options,
        CancellationToken cancellationToken)
    {
        VirtualDayTimeTimerBase t = new VirtualDayTimeTimer(this,
            isLocal: true,
            targetTimeOfDay,
            kind,
            callback,
            state,
            options,
            cancellationToken);
        lock (_gate)
            _dayTimeTimers.Add(t);
        return t;
    }

    private IClockDayTimeTimer RegisterTimeOfDayUtc (TimeSpan targetTimeOfDay,
        IntervalTimerCallbackKind kind,
        Delegate callback,
        object? state,
        DayTimeTimerOptions? options,
        CancellationToken cancellationToken)
    {
        VirtualDayTimeTimerBase t = new VirtualDayTimeTimer(this,
            isLocal: false,
            targetTimeOfDay,
            kind,
            callback,
            state,
            options,
            cancellationToken);
        lock (_gate)
            _dayTimeTimers.Add(t);
        return t;
    }

#endif

    #endregion Private helpers

    #region Interface Implementations

    #region IPrimeTestClock Implementation

    /// <inheritdoc />
    public void SetTime (DateTimeOffset utcTime)
    {
        lock (_gate)
        {
            SetVirtualUtcNowLocked(utcTime);
        }

        RaiseClockEventsAfterVirtualUtcChange(utcTime);
    }

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

        lock (_gate)
        {
            AddVirtualTimeLocked(duration);
            newNow = ReadVirtualUtcNowLocked();

            foreach (PendingDelay pd in _pendingDelays)
            {
                if (pd.DueUtc <= newNow)
                {
                    toComplete ??= [];
                    toComplete.Add(pd);
                }
            }

            if (toComplete != null)
            {
                foreach (PendingDelay pd in toComplete)
                    _pendingDelays.Remove(pd);
            }

            foreach (TimeExpiryEntry tee in _timeExpiryEntries)
            {
                if (tee.ExpireUtc <= newNow)
                {
                    toCancel ??= [];
                    toCancel.Add(tee);
                }
            }

            if (toCancel != null)
            {
                foreach (TimeExpiryEntry tee in toCancel)
                    _timeExpiryEntries.Remove(tee);
            }

            foreach (VirtualIntervalTimerBase t in _intervalTimers)
            {
                if (t.IsDue(newNow))
                {
                    intervalDue ??= [];
                    intervalDue.Add(t);
                }
            }

#if NET || !SYSTEMCLOCK
            foreach (VirtualDayTimeTimerBase t in _dayTimeTimers)
            {
                if (t.IsDue(newNow))
                {
                    dayTimeDue ??= [];
                    dayTimeDue.Add(t);
                }
            }
#endif
        }

        if (toComplete != null)
        {
            foreach (PendingDelay pd in toComplete)
                pd.Complete();
        }

        if (toCancel != null)
        {
            foreach (TimeExpiryEntry tee in toCancel)
                tee.Cancel();
        }

        while (intervalDue is { Count: > 0 })
        {
            foreach (VirtualIntervalTimerBase t in intervalDue)
                t.RunDueCallback(newNow);

            intervalDue = null;
            lock (_gate)
            {
                foreach (VirtualIntervalTimerBase t in _intervalTimers)
                {
                    if (t.IsDue(newNow))
                    {
                        intervalDue ??= [];
                        intervalDue.Add(t);
                    }
                }
            }
        }

#if NET || !SYSTEMCLOCK
        while (dayTimeDue is { Count: > 0 })
        {
            foreach (VirtualDayTimeTimerBase t in dayTimeDue)
                t.RunDueCallback(newNow);

            dayTimeDue = null;
            lock (_gate)
            {
                foreach (VirtualDayTimeTimerBase t in _dayTimeTimers)
                {
                    if (t.IsDue(newNow))
                    {
                        dayTimeDue ??= [];
                        dayTimeDue.Add(t);
                    }
                }
            }
        }
#endif

        RaiseClockEventsAfterVirtualUtcChange(newNow);
    }

    /// <inheritdoc />
    public void RunFor (TimeSpan duration)
    {
        Advance(duration);
    }

    /// <inheritdoc />
    public void Start (TimeSpan? rate = null)
    {
        lock (_gate)
        {
            if (_isRunning)
                return;
            _isRunning = true;
            _runRate = rate ?? TimeSpan.FromSeconds(1);
        }

        _runThread = new Thread(RunLoop)
        {
            IsBackground = true
        };
        _runThread.Start();
    }

    /// <inheritdoc />
    public bool Stop ()
    {
        lock (_gate)
        {
            if (!_isRunning)
                return false;
            _isRunning = false;
        }

        _runThread?.Join(TimeSpan.FromSeconds(5));
        _runThread = null;
        return true;
    }

    #endregion IPrimeTestClock Implementation

    #region IPrimeTestTime Implementation

    /// <inheritdoc />
    public bool IsRunning
    {
        get
        {
            lock (_gate)
                return _isRunning;
        }
    }

    #endregion IPrimeTestTime Implementation

#if SYSTEMCLOCK
    #region IPrimeClock Implementation — Now

    /// <inheritdoc />
    public DateTimeOffset LocalNowOffset
    {
        get
        {
            lock (_gate)
                return ToLocalOffset(ReadVirtualUtcNowLocked());
        }
    }

    /// <inheritdoc />
    public DateTimeOffset UtcNowOffset
    {
        get
        {
            lock (_gate)
                return ReadVirtualUtcNowLocked();
        }
    }

    /// <inheritdoc />
    public DateTime LocalNowDateTime
    {
        get
        {
            lock (_gate)
                return ToLocalOffset(ReadVirtualUtcNowLocked()).DateTime;
        }
    }

    /// <inheritdoc />
    public DateTime UtcNowDateTime
    {
        get
        {
            lock (_gate)
                return ReadVirtualUtcNowLocked().UtcDateTime;
        }
    }

#if NET
    /// <inheritdoc />
    public TimeOnly LocalNowTimeOnly
    {
        get
        {
            lock (_gate)
                return TimeOnly.FromDateTime(ToLocalOffset(ReadVirtualUtcNowLocked()).DateTime);
        }
    }

    /// <inheritdoc />
    public TimeOnly UtcNowTimeOnly
    {
        get
        {
            lock (_gate)
                return TimeOnly.FromDateTime(ReadVirtualUtcNowLocked().UtcDateTime);
        }
    }

    /// <inheritdoc />
    public DateOnly LocalNowDateOnly
    {
        get
        {
            lock (_gate)
                return DateOnly.FromDateTime(ToLocalOffset(ReadVirtualUtcNowLocked()).DateTime);
        }
    }

    /// <inheritdoc />
    public DateOnly UtcNowDateOnly
    {
        get
        {
            lock (_gate)
                return DateOnly.FromDateTime(ReadVirtualUtcNowLocked().UtcDateTime);
        }
    }
#endif

    #endregion IPrimeClock Implementation — Now
#endif

    #region IPrimeTime Implementation — Delays

    /// <inheritdoc />
    public void Sleep (TimeSpan sleepTime)
    {
        if (sleepTime <= TimeSpan.Zero)
            return;

        TaskCompletionSource<bool> taskCompletionSource = new(TaskCreationOptions.RunContinuationsAsynchronously);

        lock (_gate)
        {
            DateTimeOffset dueUtc = ReadVirtualUtcNowLocked() + sleepTime;
            _pendingDelays.Add(new PendingDelay(dueUtc, taskCompletionSource));
        }

        taskCompletionSource.Task.GetAwaiter().GetResult();
    }

    /// <inheritdoc />
    public void Sleep (int sleepMilliseconds)
    {
        Sleep(TimeSpan.FromMilliseconds(sleepMilliseconds));
    }

    /// <inheritdoc />
    public Task DelayAsync (TimeSpan delayTime)
    {
        return DelayAsync(delayTime, CancellationToken.None);
    }

    /// <inheritdoc />
    public Task DelayAsync (int millisecondsDelay)
    {
        return DelayAsync(millisecondsDelay, CancellationToken.None);
    }

    /// <inheritdoc />
    public Task DelayAsync (TimeSpan delayTime, CancellationToken cancellationToken)
    {
        if (delayTime <= TimeSpan.Zero)
            return Task.CompletedTask;

        TaskCompletionSource<bool> taskCompletionSource = new(TaskCreationOptions.RunContinuationsAsynchronously);

        lock (_gate)
        {
            DateTimeOffset dueUtc = ReadVirtualUtcNowLocked() + delayTime;
            _pendingDelays.Add(new PendingDelay(dueUtc, taskCompletionSource));
        }

        if (cancellationToken.CanBeCanceled)
        {
            CancellationTokenRegistration cancellationRegistration = cancellationToken.Register(() =>
            {
                lock (_gate)
                {
                    if (_pendingDelays.RemoveAll(pendingDelay => pendingDelay.TaskCompletionSource == taskCompletionSource) > 0)
                        taskCompletionSource.TrySetCanceled(cancellationToken);
                }
            });

            _ = taskCompletionSource.Task.ContinueWith((_, state) => ((CancellationTokenRegistration)state!).Dispose(),
                cancellationRegistration,
                CancellationToken.None,
                TaskContinuationOptions.None,
                TaskScheduler.Default);
        }

        return taskCompletionSource.Task;
    }

    /// <inheritdoc />
    public Task DelayAsync (int millisecondsDelay, CancellationToken cancellationToken)
    {
        return DelayAsync(TimeSpan.FromMilliseconds(millisecondsDelay), cancellationToken);
    }

    #endregion IPrimeTime Implementation — Delays

    #region IPrimeTime Implementation — Time cancellation

    /// <inheritdoc />
    public TimeCancellationTokenSource GetTimeCancellationToken (TimeSpan cancelTime)
    {
        CancellationTokenSource cts = new();
        TimeCancellationTokenSource wrapper = new(cts);

        lock (_gate)
        {
            DateTimeOffset expireUtc = ReadVirtualUtcNowLocked() + cancelTime;
            _timeExpiryEntries.Add(new TimeExpiryEntry(expireUtc, wrapper));
        }

        return wrapper;
    }

    /// <inheritdoc />
    public TimeCancellationTokenSource GetTimeCancellationToken (int cancelMilliseconds)
    {
        return GetTimeCancellationToken(TimeSpan.FromMilliseconds(cancelMilliseconds));
    }

    /// <inheritdoc />
    public TimeCancellationTokenSource LinkTimeCancellationToken (TimeSpan cancelTime, CancellationToken cancellationToken)
    {
        CancellationTokenSource timeCts = new();
        CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(timeCts.Token, cancellationToken);
        TimeCancellationTokenSource wrapper = new(linked, [timeCts]);

        lock (_gate)
        {
            DateTimeOffset expireUtc = ReadVirtualUtcNowLocked() + cancelTime;
            _timeExpiryEntries.Add(new TimeExpiryEntry(expireUtc, wrapper, timeCts));
        }

        return wrapper;
    }

    /// <inheritdoc />
    public TimeCancellationTokenSource LinkTimeCancellationToken (int cancelMilliseconds, CancellationToken cancellationToken)
    {
        return LinkTimeCancellationToken(TimeSpan.FromMilliseconds(cancelMilliseconds), cancellationToken);
    }

    /// <inheritdoc />
    public TimeCancellationTokenSource LinkTimeCancellationToken (int cancelMilliseconds,
        CancellationToken token1,
        CancellationToken token2)
    {
        CancellationTokenSource timeCts = new();
        CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(timeCts.Token, token1, token2);
        TimeCancellationTokenSource wrapper = new(linked, [timeCts]);

        lock (_gate)
        {
            DateTimeOffset expireUtc = ReadVirtualUtcNowLocked() + TimeSpan.FromMilliseconds(cancelMilliseconds);
            _timeExpiryEntries.Add(new TimeExpiryEntry(expireUtc, wrapper, timeCts));
        }

        return wrapper;
    }

    /// <inheritdoc />
    public TimeCancellationTokenSource LinkTimeCancellationToken (TimeSpan cancelTime,
        CancellationToken token1,
        CancellationToken token2)
    {
        CancellationTokenSource timeCts = new();
        CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(timeCts.Token, token1, token2);
        TimeCancellationTokenSource wrapper = new(linked, [timeCts]);

        lock (_gate)
        {
            DateTimeOffset expireUtc = ReadVirtualUtcNowLocked() + cancelTime;
            _timeExpiryEntries.Add(new TimeExpiryEntry(expireUtc, wrapper, timeCts));
        }

        return wrapper;
    }

    /// <inheritdoc />
    public TimeCancellationTokenSource LinkTimeCancellationToken (TimeSpan cancelTime,
        params CancellationToken[] cancellationTokens)
    {
        CancellationTokenSource timeCts = new();
        CancellationToken[] all = new CancellationToken[cancellationTokens.Length + 1];
        all[0] = timeCts.Token;
        Array.Copy(cancellationTokens, 0, all, 1, cancellationTokens.Length);
        CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(all);
        TimeCancellationTokenSource wrapper = new(linked, [timeCts]);

        lock (_gate)
        {
            DateTimeOffset expireUtc = ReadVirtualUtcNowLocked() + cancelTime;
            _timeExpiryEntries.Add(new TimeExpiryEntry(expireUtc, wrapper, timeCts));
        }

        return wrapper;
    }

    /// <inheritdoc />
    public TimeCancellationTokenSource LinkTimeCancellationToken (int cancelMilliseconds,
        params CancellationToken[] cancellationTokens)
    {
        return LinkTimeCancellationToken(TimeSpan.FromMilliseconds(cancelMilliseconds), cancellationTokens);
    }

    #endregion IPrimeTime Implementation — Time cancellation

    #region IPrimeClock Implementation — Interval timers

    /// <inheritdoc />
    public IClockIntervalTimer RegisterTimer (TimeSpan callbackTime,
        Action callback,
        CancellationToken cancellationToken,
        bool repeat = false,
        IntervalTimerOptions? timerOptions = null)
    {
        VirtualIntervalTimerBase intervalTimer = new VirtualIntervalTimer(this,
            callbackTime,
            repeat ? callbackTime : Timeout.InfiniteTimeSpan,
            IntervalTimerCallbackKind.SimpleAction,
            callback,
            null,
            timerOptions,
            cancellationToken);
        lock (_gate)
            _intervalTimers.Add(intervalTimer);
        return intervalTimer;
    }

    /// <inheritdoc />
    public IClockIntervalTimer RegisterTimer (TimeSpan callbackTime,
        Action<ClockTimerCallbackContext> callback,
        CancellationToken cancellationToken,
        object? state = null,
        bool repeat = false,
        IntervalTimerOptions? timerOptions = null)
    {
        VirtualIntervalTimerBase intervalTimer = new VirtualIntervalTimer(this,
            callbackTime,
            repeat ? callbackTime : Timeout.InfiniteTimeSpan,
            IntervalTimerCallbackKind.ContextAction,
            callback,
            state,
            timerOptions,
            cancellationToken);
        lock (_gate)
            _intervalTimers.Add(intervalTimer);
        return intervalTimer;
    }

    /// <inheritdoc />
    public IClockIntervalTimer RegisterTimer (TimeSpan callbackTime,
        Action<ClockTimerCallbackContext, CancellationToken> callback,
        CancellationToken cancellationToken,
        object? state = null,
        bool repeat = false,
        IntervalTimerOptions? timerOptions = null)
    {
        VirtualIntervalTimerBase intervalTimer = new VirtualIntervalTimer(this,
            callbackTime,
            repeat ? callbackTime : Timeout.InfiniteTimeSpan,
            IntervalTimerCallbackKind.ContextActionWithToken,
            callback,
            state,
            timerOptions,
            cancellationToken);
        lock (_gate)
            _intervalTimers.Add(intervalTimer);
        return intervalTimer;
    }

    /// <inheritdoc />
    public IClockIntervalTimer RegisterAsyncTimer (TimeSpan callbackTime,
        Func<CancellationToken, ValueTask> callback,
        CancellationToken cancellationToken,
        bool repeat = false,
        IntervalTimerOptions? timerOptions = null)
    {
        VirtualIntervalTimerBase intervalTimer = new VirtualIntervalTimer(this,
            callbackTime,
            repeat ? callbackTime : Timeout.InfiniteTimeSpan,
            IntervalTimerCallbackKind.SimpleAsync,
            callback,
            null,
            timerOptions,
            cancellationToken);
        lock (_gate)
            _intervalTimers.Add(intervalTimer);
        return intervalTimer;
    }

    /// <inheritdoc />
    public IClockIntervalTimer RegisterAsyncTimer (TimeSpan callbackTime,
        Func<ClockTimerCallbackContext, CancellationToken, ValueTask> callback,
        CancellationToken cancellationToken,
        object? state = null,
        bool repeat = false,
        IntervalTimerOptions? timerOptions = null)
    {
        VirtualIntervalTimerBase intervalTimer = new VirtualIntervalTimer(this,
            callbackTime,
            repeat ? callbackTime : Timeout.InfiniteTimeSpan,
            IntervalTimerCallbackKind.ContextAsync,
            callback,
            state,
            timerOptions,
            cancellationToken);
        lock (_gate)
            _intervalTimers.Add(intervalTimer);
        return intervalTimer;
    }

    /// <inheritdoc />
    public IClockIntervalTimer RegisterTimer (TimeSpan callbackTime,
        TimeSpan repeatInterval,
        Action callback,
        CancellationToken cancellationToken,
        IntervalTimerOptions? timerOptions = null)
    {
        VirtualIntervalTimerBase intervalTimer = new VirtualIntervalTimer(this,
            callbackTime,
            repeatInterval,
            IntervalTimerCallbackKind.SimpleAction,
            callback,
            null,
            timerOptions,
            cancellationToken);
        lock (_gate)
            _intervalTimers.Add(intervalTimer);
        return intervalTimer;
    }

    /// <inheritdoc />
    public IClockIntervalTimer RegisterTimer (TimeSpan callbackTime,
        TimeSpan repeatInterval,
        Action<ClockTimerCallbackContext> callback,
        CancellationToken cancellationToken,
        object? state = null,
        IntervalTimerOptions? timerOptions = null)
    {
        VirtualIntervalTimerBase intervalTimer = new VirtualIntervalTimer(this,
            callbackTime,
            repeatInterval,
            IntervalTimerCallbackKind.ContextAction,
            callback,
            state,
            timerOptions,
            cancellationToken);
        lock (_gate)
            _intervalTimers.Add(intervalTimer);
        return intervalTimer;
    }

    /// <inheritdoc />
    public IClockIntervalTimer RegisterTimer (TimeSpan callbackTime,
        TimeSpan repeatInterval,
        Action<ClockTimerCallbackContext, CancellationToken> callback,
        CancellationToken cancellationToken,
        object? state = null,
        IntervalTimerOptions? timerOptions = null)
    {
        VirtualIntervalTimerBase intervalTimer = new VirtualIntervalTimer(this,
            callbackTime,
            repeatInterval,
            IntervalTimerCallbackKind.ContextActionWithToken,
            callback,
            state,
            timerOptions,
            cancellationToken);
        lock (_gate)
            _intervalTimers.Add(intervalTimer);
        return intervalTimer;
    }

    /// <inheritdoc />
    public IClockIntervalTimer RegisterAsyncTimer (TimeSpan callbackTime,
        TimeSpan repeatInterval,
        Func<CancellationToken, ValueTask> callback,
        CancellationToken cancellationToken,
        IntervalTimerOptions? timerOptions = null)
    {
        VirtualIntervalTimerBase intervalTimer = new VirtualIntervalTimer(this,
            callbackTime,
            repeatInterval,
            IntervalTimerCallbackKind.SimpleAsync,
            callback,
            null,
            timerOptions,
            cancellationToken);
        lock (_gate)
            _intervalTimers.Add(intervalTimer);
        return intervalTimer;
    }

    /// <inheritdoc />
    public IClockIntervalTimer RegisterAsyncTimer (TimeSpan callbackTime,
        TimeSpan repeatInterval,
        Func<ClockTimerCallbackContext, CancellationToken, ValueTask> callback,
        CancellationToken cancellationToken,
        object? state = null,
        IntervalTimerOptions? timerOptions = null)
    {
        VirtualIntervalTimerBase intervalTimer = new VirtualIntervalTimer(this,
            callbackTime,
            repeatInterval,
            IntervalTimerCallbackKind.ContextAsync,
            callback,
            state,
            timerOptions,
            cancellationToken);
        lock (_gate)
            _intervalTimers.Add(intervalTimer);
        return intervalTimer;
    }

    #endregion IPrimeClock Implementation — Interval timers

#if NET
    #region IPrimeClock Implementation — Day-time timers

    /// <inheritdoc />
    public IClockDayTimeTimer RegisterTimeOfDay (LocalTimeOfDay timeOfDay,
        Action callback,
        CancellationToken cancellationToken,
        DayTimeTimerOptions? timerOptions = null) =>
        RegisterTimeOfDayLocal(timeOfDay.Value.ToTimeSpan(), IntervalTimerCallbackKind.SimpleAction, callback, null, timerOptions, cancellationToken);

    /// <inheritdoc />
    public IClockDayTimeTimer RegisterTimeOfDay (LocalTimeOfDay timeOfDay,
        Action<ClockTimerCallbackContext> callback,
        CancellationToken cancellationToken,
        object? state = null,
        DayTimeTimerOptions? timerOptions = null) =>
        RegisterTimeOfDayLocal(timeOfDay.Value.ToTimeSpan(), IntervalTimerCallbackKind.ContextAction, callback, state, timerOptions, cancellationToken);

    /// <inheritdoc />
    public IClockDayTimeTimer RegisterTimeOfDay (LocalTimeOfDay timeOfDay,
        Action<ClockTimerCallbackContext, CancellationToken> callback,
        CancellationToken cancellationToken,
        object? state = null,
        DayTimeTimerOptions? timerOptions = null) =>
        RegisterTimeOfDayLocal(timeOfDay.Value.ToTimeSpan(), IntervalTimerCallbackKind.ContextActionWithToken, callback, state, timerOptions, cancellationToken);

    /// <inheritdoc />
    public IClockDayTimeTimer RegisterAsyncTimeOfDay (LocalTimeOfDay timeOfDay,
        Func<CancellationToken, ValueTask> callback,
        CancellationToken cancellationToken,
        DayTimeTimerOptions? timerOptions = null) =>
        RegisterTimeOfDayLocal(timeOfDay.Value.ToTimeSpan(), IntervalTimerCallbackKind.SimpleAsync, callback, null, timerOptions, cancellationToken);

    /// <inheritdoc />
    public IClockDayTimeTimer RegisterAsyncTimeOfDay (LocalTimeOfDay timeOfDay,
        Func<ClockTimerCallbackContext, CancellationToken, ValueTask> callback,
        CancellationToken cancellationToken,
        object? state = null,
        DayTimeTimerOptions? timerOptions = null) =>
        RegisterTimeOfDayLocal(timeOfDay.Value.ToTimeSpan(), IntervalTimerCallbackKind.ContextAsync, callback, state, timerOptions, cancellationToken);

    /// <inheritdoc />
    public IClockDayTimeTimer RegisterTimeOfDay (UtcTimeOfDay timeOfDay,
        Action callback,
        CancellationToken cancellationToken,
        DayTimeTimerOptions? timerOptions = null) =>
        RegisterTimeOfDayUtc(timeOfDay.Value.ToTimeSpan(), IntervalTimerCallbackKind.SimpleAction, callback, null, timerOptions, cancellationToken);

    /// <inheritdoc />
    public IClockDayTimeTimer RegisterTimeOfDay (UtcTimeOfDay timeOfDay,
        Action<ClockTimerCallbackContext> callback,
        CancellationToken cancellationToken,
        object? state = null,
        DayTimeTimerOptions? timerOptions = null) =>
        RegisterTimeOfDayUtc(timeOfDay.Value.ToTimeSpan(), IntervalTimerCallbackKind.ContextAction, callback, state, timerOptions, cancellationToken);

    /// <inheritdoc />
    public IClockDayTimeTimer RegisterTimeOfDay (UtcTimeOfDay timeOfDay,
        Action<ClockTimerCallbackContext, CancellationToken> callback,
        CancellationToken cancellationToken,
        object? state = null,
        DayTimeTimerOptions? timerOptions = null) =>
        RegisterTimeOfDayUtc(timeOfDay.Value.ToTimeSpan(), IntervalTimerCallbackKind.ContextActionWithToken, callback, state, timerOptions, cancellationToken);

    /// <inheritdoc />
    public IClockDayTimeTimer RegisterAsyncTimeOfDay (UtcTimeOfDay timeOfDay,
        Func<CancellationToken, ValueTask> callback,
        CancellationToken cancellationToken,
        DayTimeTimerOptions? timerOptions = null) =>
        RegisterTimeOfDayUtc(timeOfDay.Value.ToTimeSpan(), IntervalTimerCallbackKind.SimpleAsync, callback, null, timerOptions, cancellationToken);

    /// <inheritdoc />
    public IClockDayTimeTimer RegisterAsyncTimeOfDay (UtcTimeOfDay timeOfDay,
        Func<ClockTimerCallbackContext, CancellationToken, ValueTask> callback,
        CancellationToken cancellationToken,
        object? state = null,
        DayTimeTimerOptions? timerOptions = null) =>
        RegisterTimeOfDayUtc(timeOfDay.Value.ToTimeSpan(), IntervalTimerCallbackKind.ContextAsync, callback, state, timerOptions, cancellationToken);

    #endregion IPrimeClock Implementation — Day-time timers
#endif
    #endregion Interface Implementations
}


