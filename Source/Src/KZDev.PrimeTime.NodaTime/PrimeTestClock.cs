// Copyright (c) Kevin Zehrer. All rights reserved.
// This file is part of the PrimeTime project.

using System;
using System.Diagnostics;

using NodaTime;
using NodaTime.TimeZones;

namespace KZDev.PrimeTime;

/// <summary>
///   Test clock implementation of <see cref="IPrimeTestClock"/> that maintains virtual
///   time. All "now" values, delays, time-based cancellation, and timers are driven
///   by this virtual time so tests can advance time deterministically.
/// </summary>
public sealed class PrimeTestClock : IPrimeTestClock
{
    private static readonly Duration NoRepeatSentinel = Duration.FromTimeSpan(Timeout.InfiniteTimeSpan);

    #region Nested types — Pending delay and time expiry

    private sealed class PendingDelay (Instant dueInstant,
        TaskCompletionSource<bool> taskCompletionSource)
    {
        public Instant DueInstant { [DebuggerStepThrough] get; } = dueInstant;
        public TaskCompletionSource<bool> TaskCompletionSource { [DebuggerStepThrough] get; } = taskCompletionSource;

        public void Complete ()
        {
            TaskCompletionSource.TrySetResult(true);
        }
    }

    private sealed class TimeExpiryEntry
    {
        public Instant ExpireInstant { [DebuggerStepThrough] get; }
        private readonly TimeCancellationTokenSource _wrapper;

        public TimeExpiryEntry (Instant expireInstant,
            TimeCancellationTokenSource wrapper,
            CancellationTokenSource? timeCts = null)
        {
            ExpireInstant = expireInstant;
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

    private abstract class VirtualIntervalTimerBase : IClockIntervalTimer
    {
        private PrimeTestClock Clock { [DebuggerStepThrough] get; }
        protected IntervalTimerCallbackKind CallbackKind { [DebuggerStepThrough] get; }
        protected Delegate Callback { [DebuggerStepThrough] get; }
        protected object? CallbackState { [DebuggerStepThrough] get; }
        protected CancellationToken CancellationToken { [DebuggerStepThrough] get; }
#if NET10_OR_GREATER
        protected Lock Gate { [DebuggerStepThrough] get; } = new();
#else
        protected object Gate { [DebuggerStepThrough] get; } = new();
#endif
        protected Instant? NextDueInstant { [DebuggerStepThrough] get; [DebuggerStepThrough] set; }
        protected Instant? LastCallbackInstant { [DebuggerStepThrough] get; [DebuggerStepThrough] set; }
        protected TimerState State { [DebuggerStepThrough] get; [DebuggerStepThrough] set; } = TimerState.Active;
        protected bool IntervalTimerEnabled { [DebuggerStepThrough] get; [DebuggerStepThrough] private set; } = true;
        protected bool Disposed { [DebuggerStepThrough] get; [DebuggerStepThrough] private set; }
        protected bool CancelRequested { [DebuggerStepThrough] get; [DebuggerStepThrough] private set; }
        protected int CallbacksRunning { [DebuggerStepThrough] get; [DebuggerStepThrough] set; }
        private Duration InitialCallbackTime;
        protected Duration RepeatInterval { [DebuggerStepThrough] get; [DebuggerStepThrough] private set; }

        protected VirtualIntervalTimerBase (PrimeTestClock clock,
            Duration initialCallbackTime,
            Duration repeatInterval,
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
            IsLocalTimeRepresentation = options?.LocalTimeRepresentation ?? false;
            IsResetAfterCallback = options?.ResetIntervalAfterCallback ?? false;
            Id = Interlocked.Increment(ref _nextTimerId);
            Instant now = Clock.NowInstant;
            RegisteredInstant = now;
            NextDueInstant = now + initialCallbackTime;

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
        public Instant RegisteredInstant { [DebuggerStepThrough] get; }
        public DateTimeOffset RegisteredTime => RegisteredInstant.ToDateTimeOffset();
        public bool IsTimeOfDay => false;
        public bool IsRepeating =>
            RepeatInterval > Duration.Zero && RepeatInterval != NoRepeatSentinel;
        public bool IsResetAfterCallback => field && IsRepeating;
        public bool IsCancelled => State == TimerState.Cancelled;
        public bool IsLocalTimeRepresentation { [DebuggerStepThrough] get; }
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
                    if (LastCallbackInstant is not { } last)
                        return -1;
                    if (CallbacksRunning > 0)
                        return 0;
                    Instant now = Clock.NowInstant;
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
                    if (!IsRepeating && LastCallbackInstant.HasValue)
                        return -1;
                    if (NextDueInstant is not { } next)
                        return -1;
                    Instant now = Clock.NowInstant;
                    if (next <= now)
                        return 0;
                    if (CallbacksRunning > 0 && IsResetAfterCallback)
                        return (long)RepeatInterval.TotalMilliseconds;
                    return (long)(next - now).TotalMilliseconds;
                }
            }
        }

        public bool IsDue (Instant now)
        {
            lock (Gate)
            {
                if (Disposed || CancelRequested || State == TimerState.Cancelled || !IntervalTimerEnabled)
                    return false;
                if (NextDueInstant is not { } next)
                    return false;
                return next <= now;
            }
        }

        public abstract void RunDueCallback (Instant now);

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
            Change(Duration.FromTimeSpan(interval), IsRepeating ? Duration.FromTimeSpan(interval) : NoRepeatSentinel);

        public bool Change (TimeSpan nextInterval, TimeSpan repeatInterval)
        {
            Duration next = Duration.FromTimeSpan(nextInterval);
            Duration repeat = repeatInterval == Timeout.InfiniteTimeSpan ? NoRepeatSentinel : Duration.FromTimeSpan(repeatInterval);
            return Change(next, repeat);
        }

        public bool Change (Duration interval) =>
            Change(interval, IsRepeating ? interval : NoRepeatSentinel);

        public bool Change (Duration nextInterval, Duration repeatInterval)
        {
            lock (Gate)
            {
                if (Disposed || State == TimerState.Cancelled)
                    return false;
                if (!IsRepeating && repeatInterval > Duration.Zero && repeatInterval != NoRepeatSentinel)
                    throw new InvalidOperationException("Cannot change a non-repeating timer to a repeating timer.");
                InitialCallbackTime = nextInterval;
                RepeatInterval = repeatInterval;
                if (State == TimerState.Completed)
                    State = TimerState.Active;
                if (!IntervalTimerEnabled)
                    return true;
                NextDueInstant = Clock.NowInstant + nextInterval;
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
                NextDueInstant = Clock.NowInstant + InitialCallbackTime;
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

    private sealed class VirtualIntervalTimer (PrimeTestClock clock,
        Duration initialCallbackTime,
        Duration repeatInterval,
        IntervalTimerCallbackKind callbackKind,
        Delegate callback,
        object? callbackState,
        IntervalTimerOptions? options,
        CancellationToken cancellationToken)
        : VirtualIntervalTimerBase(clock,
            initialCallbackTime,
            repeatInterval,
            callbackKind,
            callback,
            callbackState,
            options,
            cancellationToken)
    {
        public override void RunDueCallback (Instant now)
        {
            bool resetAfter;
            bool isRepeating;
            Instant firedAt;

            lock (Gate)
            {
                if (Disposed || CancelRequested || State == TimerState.Cancelled || !IntervalTimerEnabled)
                    return;
                if (NextDueInstant is not { } next || next > now)
                    return;
                firedAt = next;
                NextDueInstant = null;
                LastCallbackInstant = firedAt;
                isRepeating = IsRepeating;
                resetAfter = IsResetAfterCallback;
                State = isRepeating && !resetAfter
                    ? TimerState.RepeatProcessingCallback
                    : TimerState.ProcessingCallback;
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

        private void RunCallback (bool resetAfter, bool isRepeating, Instant now)
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

        private void RunAsyncAndScheduleAfter (Func<ValueTask> run,
            bool resetAfter,
            bool isRepeating,
            Instant now)
        {
            ValueTask vt = run();
            if (vt.IsCompletedSuccessfully)
            {
                OnAsyncCallbackCompleted(resetAfter, isRepeating, now);
                return;
            }

            vt.AsTask().ContinueWith((_, state) =>
                {
                    (VirtualIntervalTimer reg, bool ra, bool rep, Instant n) =
                        ((VirtualIntervalTimer, bool, bool, Instant))state!;
                    reg.OnAsyncCallbackCompleted(ra, rep, n);
                },
                (this, resetAfter, isRepeating, now),
                CancellationToken.None,
                TaskContinuationOptions.None,
                TaskScheduler.Default);
        }

        private void OnSyncCallbackCompleted (bool resetAfter, bool isRepeating, Instant now)
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
                NextDueInstant = now + RepeatInterval;
            }
        }

        private void OnAsyncCallbackCompleted (bool resetAfter, bool isRepeating, Instant now)
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
                NextDueInstant = now + RepeatInterval;
            }
        }
    }

    #endregion Nested types — Virtual interval timer

    #region Nested types — Virtual day-time timer

    private abstract class VirtualDayTimeTimerBase : IClockDayTimeTimer
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
        protected Instant? NextDueInstant { [DebuggerStepThrough] get; [DebuggerStepThrough] set; }
        protected TimerState State { [DebuggerStepThrough] get; [DebuggerStepThrough] set; } = TimerState.Active;
        protected bool EnabledDayTime { [DebuggerStepThrough] get; [DebuggerStepThrough] set; } = true;
        protected bool Disposed { [DebuggerStepThrough] get; [DebuggerStepThrough] set; }
        protected bool CancelRequested { [DebuggerStepThrough] get; [DebuggerStepThrough] set; }
        protected int CallbacksRunning { [DebuggerStepThrough] get; [DebuggerStepThrough] set; }
        protected LocalTime TargetTimeOfDay { [DebuggerStepThrough] get; [DebuggerStepThrough] set; }
        protected bool UtcTimeOfDaySchedule { [DebuggerStepThrough] get; }

        protected VirtualDayTimeTimerBase (PrimeTestClock clock,
            LocalTime timeOfDay,
            IntervalTimerCallbackKind callbackKind,
            Delegate callback,
            object? callbackState,
            DayTimeTimerOptions? options,
            CancellationToken cancellationToken,
            bool utcTimeOfDaySchedule = false)
        {
            Clock = clock;
            UtcTimeOfDaySchedule = utcTimeOfDaySchedule;
            TargetTimeOfDay = timeOfDay;
            CallbackKind = callbackKind;
            Callback = callback;
            CallbackState = callbackState;
            CancellationToken = cancellationToken;
            ConcurrentTriggerProcessing =
                options?.ConcurrentTriggerProcessing ?? ConcurrentTriggerProcessing.RunSequentially;
            SkippedTimeBehavior = options?.SkippedTimeBehavior ?? SkippedTimeBehavior.RunAfter;
            DuplicateTimeBehavior = options?.DuplicateTimeBehavior ?? DuplicateTimeBehavior.RunFirst;
            Id = Interlocked.Increment(ref _nextTimerId);
            RegisteredInstant = Clock.NowInstant;
            IsLocalTimeRepresentation = !utcTimeOfDaySchedule;
            NextDueInstant = ComputeNextDue(Clock.NowInstant);

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

        public int Id { [DebuggerStepThrough] get; }
        public Instant RegisteredInstant { [DebuggerStepThrough] get; }
        public DateTimeOffset RegisteredTime => RegisteredInstant.ToDateTimeOffset();
        public bool IsTimeOfDay => true;
        public bool IsLocalTimeRepresentation { [DebuggerStepThrough] get; }
        public bool IsRepeating => true;
        public bool IsCancelled => State == TimerState.Cancelled;
        public bool IsActive =>
            State != TimerState.Cancelled && State != TimerState.Disposed && EnabledDayTime;
        public bool CallbacksProcessing => CallbacksRunning > 0;
        public ConcurrentTriggerProcessing ConcurrentTriggerProcessing { [DebuggerStepThrough] get; }
        public SkippedTimeBehavior SkippedTimeBehavior { [DebuggerStepThrough] get; }
        public DuplicateTimeBehavior DuplicateTimeBehavior { [DebuggerStepThrough] get; }

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
                        NextDueInstant = ComputeNextDue(Clock.NowInstant);
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

        public bool IsDue (Instant now)
        {
            lock (Gate)
            {
                if (Disposed || CancelRequested || State == TimerState.Cancelled || !EnabledDayTime)
                    return false;
                if (NextDueInstant is not { } next)
                    return false;
                return next <= now;
            }
        }

        public abstract void RunDueCallback (Instant now);

        protected Instant? ComputeNextDue (Instant now)
        {
            if (UtcTimeOfDaySchedule)
            {
                ZonedDateTime nowZ = now.InZone(DateTimeZone.Utc);
                LocalDate today = nowZ.Date;
                LocalDateTime nextLdt = today.At(TargetTimeOfDay);
                ZonedDateTime nextZdt = nextLdt.InZoneLeniently(DateTimeZone.Utc);
                if (nextZdt.ToInstant() <= now)
                {
                    nextLdt = today.PlusDays(1).At(TargetTimeOfDay);
                    nextZdt = nextLdt.InZoneLeniently(DateTimeZone.Utc);
                }

                return nextZdt.ToInstant();
            }

            ZonedDateTime nowLocalZ = now.InZone(Clock._zone);
            LocalDate todayLocal = nowLocalZ.Date;
            LocalDateTime nextLocalLdt = todayLocal.At(TargetTimeOfDay);
            ZonedDateTime nextLocalZdt = nextLocalLdt.InZoneLeniently(nowLocalZ.Zone);
            if (nextLocalZdt.ToInstant() <= now)
            {
                nextLocalLdt = todayLocal.PlusDays(1).At(TargetTimeOfDay);
                nextLocalZdt = nextLocalLdt.InZoneLeniently(nowLocalZ.Zone);
            }

            return nextLocalZdt.ToInstant();
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
            if (UtcTimeOfDaySchedule)
            {
                return false;
            }

            TimeOnly t = newTimeOfDay.Value;
            LocalTime localTime = new(t.Hour, t.Minute, t.Second, t.Millisecond);
            return Change(localTime);
        }

        public bool Change (UtcTimeOfDay newTimeOfDay)
        {
            if (!UtcTimeOfDaySchedule)
            {
                return false;
            }

            TimeOnly t = newTimeOfDay.Value;
            LocalTime localTime = new(t.Hour, t.Minute, t.Second, t.Millisecond);
            return Change(localTime);
        }
#endif

        public bool Change (LocalTime timeOfDay)
        {
            lock (Gate)
            {
                if (Disposed || State == TimerState.Cancelled)
                    return false;
                TargetTimeOfDay = timeOfDay;
                if (Enabled)
                    NextDueInstant = ComputeNextDue(Clock.NowInstant);
                return true;
            }
        }

        public bool Change (Duration interval) => false;

        public void Cancel ()
        {
            lock (Gate)
            {
                if (State == TimerState.Cancelled || Disposed)
                    return;
                CancelRequested = true;
                State = TimerState.Cancelled;
                EnabledDayTime = false;
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
                NextDueInstant = ComputeNextDue(Clock.NowInstant);
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

    private sealed class VirtualDayTimeTimer (PrimeTestClock clock,
        LocalTime timeOfDay,
        IntervalTimerCallbackKind callbackKind,
        Delegate callback,
        object? callbackState,
        DayTimeTimerOptions? options,
        CancellationToken cancellationToken,
        bool utcTimeOfDaySchedule = false)
        : VirtualDayTimeTimerBase(clock, timeOfDay, callbackKind, callback, callbackState, options, cancellationToken,
            utcTimeOfDaySchedule)
    {
        public override void RunDueCallback (Instant now)
        {
            lock (Gate)
            {
                if (Disposed || CancelRequested || State == TimerState.Cancelled || !EnabledDayTime)
                    return;
                if (NextDueInstant is not { } next || next > now)
                    return;
                NextDueInstant = ComputeNextDue(now);
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

    private static int _nextTimerId;
#if NET10_OR_GREATER
    private readonly Lock _gate = new();
#else
    private readonly object _gate = new();
#endif
    private Instant _now;
    private readonly DateTimeZone _zone;
    private bool _isRunning;
    private Thread? _runThread;
    private Duration _runRate = Duration.FromSeconds(1);
    private readonly List<PendingDelay> _pendingDelays = [];
    private readonly List<TimeExpiryEntry> _timeExpiryEntries = [];
    private readonly List<VirtualIntervalTimerBase> _intervalTimers = [];
    private readonly List<VirtualDayTimeTimerBase> _dayTimeTimers = [];

    #region Constructors/Finalizers

    /// <summary>
    ///   Initializes a new instance of the <see cref="PrimeTestClock"/> class with
    ///   virtual time set to the current system instant at construction.
    /// </summary>
    public PrimeTestClock ()
    {
        _now = SystemClock.Instance.GetCurrentInstant();
        _zone = GetSystemDefaultTimeZone();
    }

    /// <summary>
    ///   Initializes a new instance of the <see cref="PrimeTestClock"/> class with
    ///   the specified initial instant and system default time zone.
    /// </summary>
    /// <param name="initialInstant">
    ///   The initial virtual instant.
    /// </param>
    public PrimeTestClock (Instant initialInstant)
    {
        _now = initialInstant;
        _zone = GetSystemDefaultTimeZone();
    }

    /// <summary>
    ///   Initializes a new instance of the <see cref="PrimeTestClock"/> class with
    ///   the specified initial instant and time zone.
    /// </summary>
    /// <param name="initialInstant">
    ///   The initial virtual instant.
    /// </param>
    /// <param name="zone">
    ///   The time zone used for local "now" values.
    /// </param>
    public PrimeTestClock (Instant initialInstant, DateTimeZone zone)
    {
        _now = initialInstant;
        _zone = zone ?? throw new ArgumentNullException(nameof(zone));
    }

    #endregion Constructors/Finalizers


    /// <summary>
    ///   Occurs when the clock's current time has changed.
    /// </summary>
    public event EventHandler<ClockTimeChangedEventArgs>? ClockEvents;

    private static DateTimeZone GetSystemDefaultTimeZone ()
    {
        try
        {
            return DateTimeZoneProviders.Bcl.GetSystemDefault();
        }
        catch (DateTimeZoneNotFoundException)
        {
            return BclDateTimeZone.ForSystemDefault();
        }
    }

#if NET
    private static LocalTime TimeOnlyToLocalTime (TimeOnly t) =>
        new(t.Hour, t.Minute, t.Second, t.Millisecond);
#endif

    #region Private helpers

    private void RunLoop ()
    {
        while (true)
        {
            Thread.Sleep(1000);
            Duration toAdvance;
            lock (_gate)
            {
                if (!_isRunning)
                    return;
                toAdvance = _runRate;
            }

            Advance(toAdvance);
        }
    }

    private void RaiseClockEvents (Instant instant)
    {
        ClockEvents?.Invoke(this, new NodaClockTimeChangedEventArgs(instant));
    }

    private void RemoveIntervalTimer (VirtualIntervalTimerBase timer)
    {
        lock (_gate)
            _intervalTimers.Remove(timer);
    }

    private void RemoveDayTimeTimer (VirtualDayTimeTimerBase timer)
    {
        lock (_gate)
            _dayTimeTimers.Remove(timer);
    }

    #endregion Private helpers

    #region Interface Implementations

    #region IPrimeTestClock Implementation

    /// <inheritdoc />
    public void SetInstant (Instant instant)
    {
        lock (_gate)
        {
            _now = instant;
        }

        RaiseClockEvents(instant);
    }

    /// <inheritdoc />
    public void SetTime (DateTimeOffset utcTime)
    {
        SetInstant(Instant.FromDateTimeUtc(utcTime.UtcDateTime));
    }

    /// <inheritdoc />
    public void SetLocalTime (LocalDateTime localDateTime)
    {
        SetInstant(localDateTime.InZoneLeniently(_zone).ToInstant());
    }

    /// <inheritdoc />
    public void Advance (TimeSpan duration)
    {
        Advance(Duration.FromTimeSpan(duration));
    }

    /// <inheritdoc />
    public void Advance (Duration duration)
    {
        if (duration < Duration.Zero)
            duration = Duration.Zero;

        Instant newNow;
        List<PendingDelay>? toComplete = null;
        List<TimeExpiryEntry>? toCancel = null;
        List<VirtualIntervalTimerBase>? intervalDue = null;
        List<VirtualDayTimeTimerBase>? dayTimeDue = null;

        lock (_gate)
        {
            _now += duration;
            newNow = _now;

            foreach (PendingDelay pd in _pendingDelays)
            {
                if (pd.DueInstant <= newNow)
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
                if (tee.ExpireInstant <= newNow)
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

            foreach (VirtualDayTimeTimerBase t in _dayTimeTimers)
            {
                if (t.IsDue(newNow))
                {
                    dayTimeDue ??= [];
                    dayTimeDue.Add(t);
                }
            }
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

        RaiseClockEvents(newNow);
    }

    /// <inheritdoc />
    public void RunFor (TimeSpan duration)
    {
        RunFor(Duration.FromTimeSpan(duration));
    }

    /// <inheritdoc />
    public void RunFor (Duration duration)
    {
        Advance(duration);
    }

    /// <inheritdoc />
    public void Start (TimeSpan? rate = null)
    {
        Start(rate is null ? null : Duration.FromTimeSpan(rate.Value));
    }

    /// <inheritdoc />
    public void Start (Duration? rate = null)
    {
        lock (_gate)
        {
            if (_isRunning)
                return;
            _isRunning = true;
            _runRate = rate ?? Duration.FromSeconds(1);
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

    #region IPrimeClock Implementation — Now

    /// <inheritdoc />
    public Instant NowInstant
    {
        get
        {
            lock (_gate)
                return _now;
        }
    }

    /// <inheritdoc />
    public ZonedDateTime UtcNow
    {
        get
        {
            lock (_gate)
                return _now.InUtc();
        }
    }

    /// <inheritdoc />
    public ZonedDateTime LocalZonedNow
    {
        get
        {
            lock (_gate)
                return _now.InZone(_zone);
        }
    }

    /// <inheritdoc />
    public ZonedDateTime UtcZonedNow => UtcNow;

    /// <inheritdoc />
    public LocalDateTime LocalNow => LocalZonedNow.LocalDateTime;

    /// <inheritdoc />
    public LocalTime LocalNowTime => LocalZonedNow.TimeOfDay;

    /// <inheritdoc />
    public LocalTime UtcNowTime => UtcNow.TimeOfDay;

    /// <inheritdoc />
    public LocalDate LocalNowDate => LocalZonedNow.Date;

    /// <inheritdoc />
    public LocalDate UtcNowDate => UtcNow.Date;

    /// <inheritdoc />
    public DateTimeOffset LocalNowOffset => LocalZonedNow.ToDateTimeOffset();

    /// <inheritdoc />
    public DateTimeOffset UtcNowOffset => UtcNow.ToDateTimeOffset();

    /// <inheritdoc />
    public DateTime LocalNowDateTime => LocalNowOffset.LocalDateTime;

    /// <inheritdoc />
    public DateTime UtcNowDateTime => UtcNowOffset.UtcDateTime;

#if NET
    /// <inheritdoc />
    public TimeOnly LocalNowTimeOnly => TimeOnly.FromDateTime(LocalNowDateTime);

    /// <inheritdoc />
    public TimeOnly UtcNowTimeOnly => TimeOnly.FromDateTime(UtcNowDateTime);

    /// <inheritdoc />
    public DateOnly LocalNowDateOnly => DateOnly.FromDateTime(LocalNowDateTime);

    /// <inheritdoc />
    public DateOnly UtcNowDateOnly => DateOnly.FromDateTime(UtcNowDateTime);
#endif

    #endregion IPrimeClock Implementation — Now

    #region IPrimeTime Implementation — Delays (TimeSpan/int)

    /// <inheritdoc />
    public void Sleep (TimeSpan sleepTime) => Sleep(Duration.FromTimeSpan(sleepTime));

    /// <inheritdoc />
    public void Sleep (int sleepMilliseconds) => Sleep(Duration.FromMilliseconds(sleepMilliseconds));

    /// <inheritdoc />
    public Task DelayAsync (TimeSpan delayTime) => DelayAsync(Duration.FromTimeSpan(delayTime));

    /// <inheritdoc />
    public Task DelayAsync (int millisecondsDelay) =>
        DelayAsync(Duration.FromMilliseconds(millisecondsDelay));

    /// <inheritdoc />
    public Task DelayAsync (TimeSpan delayTime, CancellationToken cancellationToken) =>
        DelayAsync(Duration.FromTimeSpan(delayTime), cancellationToken);

    /// <inheritdoc />
    public Task DelayAsync (int millisecondsDelay, CancellationToken cancellationToken) =>
        DelayAsync(Duration.FromMilliseconds(millisecondsDelay), cancellationToken);

    #endregion IPrimeTime Implementation — Delays (TimeSpan/int)

    #region IPrimeClock Implementation — Delays (Duration)

    /// <inheritdoc />
    public void Sleep (Duration duration)
    {
        if (duration <= Duration.Zero)
            return;

        TaskCompletionSource<bool> taskCompletionSource = new(TaskCreationOptions.RunContinuationsAsynchronously);

        lock (_gate)
        {
            Instant dueInstant = _now + duration;
            _pendingDelays.Add(new PendingDelay(dueInstant, taskCompletionSource));
        }

        taskCompletionSource.Task.GetAwaiter().GetResult();
    }

    /// <inheritdoc />
    public Task DelayAsync (Duration duration)
    {
        return DelayAsync(duration, CancellationToken.None);
    }

    /// <inheritdoc />
    public Task DelayAsync (Duration duration, CancellationToken cancellationToken)
    {
        if (duration <= Duration.Zero)
            return Task.CompletedTask;

        TaskCompletionSource<bool> taskCompletionSource = new(TaskCreationOptions.RunContinuationsAsynchronously);

        lock (_gate)
        {
            Instant dueInstant = _now + duration;
            _pendingDelays.Add(new PendingDelay(dueInstant, taskCompletionSource));
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

    #endregion IPrimeClock Implementation — Delays (Duration)

    #region IPrimeClock Implementation — Time cancellation (Duration)

    /// <inheritdoc />
    public TimeCancellationTokenSource GetTimeCancellationToken (Duration cancelAfter)
    {
        CancellationTokenSource cts = new();
        TimeCancellationTokenSource wrapper = new(cts);

        lock (_gate)
        {
            Instant expireInstant = _now + cancelAfter;
            _timeExpiryEntries.Add(new TimeExpiryEntry(expireInstant, wrapper));
        }

        return wrapper;
    }

    /// <inheritdoc />
    public TimeCancellationTokenSource LinkTimeCancellationToken (Duration cancelAfter, CancellationToken cancellationToken)
    {
        CancellationTokenSource timeCts = new();
        CancellationTokenSource linked =
            CancellationTokenSource.CreateLinkedTokenSource(timeCts.Token, cancellationToken);
        TimeCancellationTokenSource wrapper = new(linked, [timeCts]);

        lock (_gate)
        {
            Instant expireInstant = _now + cancelAfter;
            _timeExpiryEntries.Add(new TimeExpiryEntry(expireInstant, wrapper, timeCts));
        }

        return wrapper;
    }

    /// <inheritdoc />
    public TimeCancellationTokenSource LinkTimeCancellationToken (Duration cancelAfter, CancellationToken token1,
        CancellationToken token2)
    {
        CancellationTokenSource timeCts = new();
        CancellationTokenSource linked =
            CancellationTokenSource.CreateLinkedTokenSource(timeCts.Token, token1, token2);
        TimeCancellationTokenSource wrapper = new(linked, [timeCts]);

        lock (_gate)
        {
            Instant expireInstant = _now + cancelAfter;
            _timeExpiryEntries.Add(new TimeExpiryEntry(expireInstant, wrapper, timeCts));
        }

        return wrapper;
    }

    /// <inheritdoc />
    public TimeCancellationTokenSource LinkTimeCancellationToken (Duration cancelAfter,
        params CancellationToken[] cancellationTokens)
    {
        CancellationTokenSource timeCts = new();
        CancellationTokenSource linked =
            CancellationTokenSource.CreateLinkedTokenSource([timeCts.Token, .. cancellationTokens]);
        TimeCancellationTokenSource wrapper = new(linked, [timeCts]);

        lock (_gate)
        {
            Instant expireInstant = _now + cancelAfter;
            _timeExpiryEntries.Add(new TimeExpiryEntry(expireInstant, wrapper, timeCts));
        }

        return wrapper;
    }

    #endregion IPrimeClock Implementation — Time cancellation (Duration)

    #region IPrimeTime Implementation — Time cancellation (TimeSpan/int)

    /// <inheritdoc />
    public TimeCancellationTokenSource GetTimeCancellationToken (TimeSpan cancelTime) =>
        GetTimeCancellationToken(Duration.FromTimeSpan(cancelTime));

    /// <inheritdoc />
    public TimeCancellationTokenSource GetTimeCancellationToken (int cancelMilliseconds) =>
        GetTimeCancellationToken(Duration.FromMilliseconds(cancelMilliseconds));

    /// <inheritdoc />
    public TimeCancellationTokenSource LinkTimeCancellationToken (TimeSpan cancelTime, CancellationToken cancellationToken) =>
        LinkTimeCancellationToken(Duration.FromTimeSpan(cancelTime), cancellationToken);

    /// <inheritdoc />
    public TimeCancellationTokenSource LinkTimeCancellationToken (int cancelMilliseconds, CancellationToken cancellationToken) =>
        LinkTimeCancellationToken(Duration.FromMilliseconds(cancelMilliseconds), cancellationToken);

    /// <inheritdoc />
    public TimeCancellationTokenSource LinkTimeCancellationToken (int cancelMilliseconds, CancellationToken token1,
        CancellationToken token2)
    {
        return LinkTimeCancellationToken(Duration.FromMilliseconds(cancelMilliseconds), token1, token2);
    }

    /// <inheritdoc />
    public TimeCancellationTokenSource LinkTimeCancellationToken (TimeSpan cancelTime, CancellationToken token1,
        CancellationToken token2)
    {
        return LinkTimeCancellationToken(Duration.FromTimeSpan(cancelTime), token1, token2);
    }

    /// <inheritdoc />
    public TimeCancellationTokenSource LinkTimeCancellationToken (TimeSpan cancelTime,
        params CancellationToken[] cancellationTokens)
    {
        return LinkTimeCancellationToken(Duration.FromTimeSpan(cancelTime), cancellationTokens);
    }

    /// <inheritdoc />
    public TimeCancellationTokenSource LinkTimeCancellationToken (int cancelMilliseconds,
        params CancellationToken[] cancellationTokens)
    {
        return LinkTimeCancellationToken(Duration.FromMilliseconds(cancelMilliseconds), cancellationTokens);
    }

    #endregion IPrimeTime Implementation — Time cancellation (TimeSpan/int)

    #region IPrimeClock Implementation — Interval timers

    /// <inheritdoc />
    public IClockIntervalTimer RegisterTimer (Duration callbackTime,
        Action callback,
        CancellationToken cancellationToken,
        bool repeat = false,
        IntervalTimerOptions? timerOptions = null)
    {
        VirtualIntervalTimerBase intervalTimer = new VirtualIntervalTimer(this,
            callbackTime,
            repeat ? callbackTime : NoRepeatSentinel,
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
    public IClockIntervalTimer RegisterTimer (Duration callbackTime,
        Action<ClockTimerCallbackContext> callback,
        CancellationToken cancellationToken,
        object? state = null,
        bool repeat = false,
        IntervalTimerOptions? timerOptions = null)
    {
        VirtualIntervalTimerBase intervalTimer = new VirtualIntervalTimer(this,
            callbackTime,
            repeat ? callbackTime : NoRepeatSentinel,
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
    public IClockIntervalTimer RegisterTimer (Duration callbackTime,
        Action<ClockTimerCallbackContext, CancellationToken> callback,
        CancellationToken cancellationToken,
        object? state = null,
        bool repeat = false,
        IntervalTimerOptions? timerOptions = null)
    {
        VirtualIntervalTimerBase intervalTimer = new VirtualIntervalTimer(this,
            callbackTime,
            repeat ? callbackTime : NoRepeatSentinel,
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
    public IClockIntervalTimer RegisterAsyncTimer (Duration callbackTime,
        Func<CancellationToken, ValueTask> callback,
        CancellationToken cancellationToken,
        bool repeat = false,
        IntervalTimerOptions? timerOptions = null)
    {
        VirtualIntervalTimerBase intervalTimer = new VirtualIntervalTimer(this,
            callbackTime,
            repeat ? callbackTime : NoRepeatSentinel,
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
    public IClockIntervalTimer RegisterAsyncTimer (Duration callbackTime,
        Func<ClockTimerCallbackContext, CancellationToken, ValueTask> callback,
        CancellationToken cancellationToken,
        object? state = null,
        bool repeat = false,
        IntervalTimerOptions? timerOptions = null)
    {
        VirtualIntervalTimerBase intervalTimer = new VirtualIntervalTimer(this,
            callbackTime,
            repeat ? callbackTime : NoRepeatSentinel,
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
    public IClockIntervalTimer RegisterTimer (Duration callbackTime,
        Duration repeatInterval,
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
    public IClockIntervalTimer RegisterTimer (Duration callbackTime,
        Duration repeatInterval,
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
    public IClockIntervalTimer RegisterTimer (Duration callbackTime,
        Duration repeatInterval,
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
    public IClockIntervalTimer RegisterAsyncTimer (Duration callbackTime,
        Duration repeatInterval,
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
    public IClockIntervalTimer RegisterAsyncTimer (Duration callbackTime,
        Duration repeatInterval,
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

    /// <inheritdoc />
    public IClockIntervalTimer RegisterTimer (TimeSpan callbackTime,
        Action callback,
        CancellationToken cancellationToken,
        bool repeat = false,
        IntervalTimerOptions? timerOptions = null) =>
        RegisterTimer(Duration.FromTimeSpan(callbackTime), callback, cancellationToken, repeat, timerOptions);

    /// <inheritdoc />
    public IClockIntervalTimer RegisterTimer (TimeSpan callbackTime,
        Action<ClockTimerCallbackContext> callback,
        CancellationToken cancellationToken,
        object? state = null,
        bool repeat = false,
        IntervalTimerOptions? timerOptions = null) =>
        RegisterTimer(Duration.FromTimeSpan(callbackTime), callback, cancellationToken, state, repeat, timerOptions);

    /// <inheritdoc />
    public IClockIntervalTimer RegisterTimer (TimeSpan callbackTime,
        Action<ClockTimerCallbackContext, CancellationToken> callback,
        CancellationToken cancellationToken,
        object? state = null,
        bool repeat = false,
        IntervalTimerOptions? timerOptions = null) =>
        RegisterTimer(Duration.FromTimeSpan(callbackTime), callback, cancellationToken, state, repeat, timerOptions);

    /// <inheritdoc />
    public IClockIntervalTimer RegisterAsyncTimer (TimeSpan callbackTime,
        Func<CancellationToken, ValueTask> callback,
        CancellationToken cancellationToken,
        bool repeat = false,
        IntervalTimerOptions? timerOptions = null) =>
        RegisterAsyncTimer(Duration.FromTimeSpan(callbackTime), callback, cancellationToken, repeat, timerOptions);

    /// <inheritdoc />
    public IClockIntervalTimer RegisterAsyncTimer (TimeSpan callbackTime,
        Func<ClockTimerCallbackContext, CancellationToken, ValueTask> callback,
        CancellationToken cancellationToken,
        object? state = null,
        bool repeat = false,
        IntervalTimerOptions? timerOptions = null) =>
        RegisterAsyncTimer(Duration.FromTimeSpan(callbackTime), callback, cancellationToken, state, repeat, timerOptions);

    /// <inheritdoc />
    public IClockIntervalTimer RegisterTimer (TimeSpan callbackTime,
        TimeSpan repeatInterval,
        Action callback,
        CancellationToken cancellationToken,
        IntervalTimerOptions? timerOptions = null) =>
        RegisterTimer(Duration.FromTimeSpan(callbackTime),
            Duration.FromTimeSpan(repeatInterval),
            callback,
            cancellationToken,
            timerOptions);

    /// <inheritdoc />
    public IClockIntervalTimer RegisterTimer (TimeSpan callbackTime,
        TimeSpan repeatInterval,
        Action<ClockTimerCallbackContext> callback,
        CancellationToken cancellationToken,
        object? state = null,
        IntervalTimerOptions? timerOptions = null) =>
        RegisterTimer(Duration.FromTimeSpan(callbackTime),
            Duration.FromTimeSpan(repeatInterval),
            callback,
            cancellationToken,
            state,
            timerOptions);

    /// <inheritdoc />
    public IClockIntervalTimer RegisterTimer (TimeSpan callbackTime,
        TimeSpan repeatInterval,
        Action<ClockTimerCallbackContext, CancellationToken> callback,
        CancellationToken cancellationToken,
        object? state = null,
        IntervalTimerOptions? timerOptions = null) =>
        RegisterTimer(Duration.FromTimeSpan(callbackTime),
            Duration.FromTimeSpan(repeatInterval),
            callback,
            cancellationToken,
            state,
            timerOptions);

    /// <inheritdoc />
    public IClockIntervalTimer RegisterAsyncTimer (TimeSpan callbackTime,
        TimeSpan repeatInterval,
        Func<CancellationToken, ValueTask> callback,
        CancellationToken cancellationToken,
        IntervalTimerOptions? timerOptions = null) =>
        RegisterAsyncTimer(Duration.FromTimeSpan(callbackTime),
            Duration.FromTimeSpan(repeatInterval),
            callback,
            cancellationToken,
            timerOptions);

    /// <inheritdoc />
    public IClockIntervalTimer RegisterAsyncTimer (TimeSpan callbackTime,
        TimeSpan repeatInterval,
        Func<ClockTimerCallbackContext, CancellationToken, ValueTask> callback,
        CancellationToken cancellationToken,
        object? state = null,
        IntervalTimerOptions? timerOptions = null) =>
        RegisterAsyncTimer(Duration.FromTimeSpan(callbackTime),
            Duration.FromTimeSpan(repeatInterval),
            callback,
            cancellationToken,
            state,
            timerOptions);

    #endregion IPrimeClock Implementation — Interval timers

    #region IPrimeClock Implementation — Time-of-day timers

#if NET
    /// <inheritdoc />
    public IClockDayTimeTimer RegisterTimeOfDay (LocalTime timeOfDay,
        Action callback,
        CancellationToken cancellationToken,
        DayTimeTimerOptions? timerOptions = null)
    {
        VirtualDayTimeTimerBase dayTimeTimer = new VirtualDayTimeTimer(this,
            timeOfDay,
            IntervalTimerCallbackKind.SimpleAction,
            callback,
            null,
            timerOptions,
            cancellationToken);
        lock (_gate)
            _dayTimeTimers.Add(dayTimeTimer);
        return dayTimeTimer;
    }

    /// <inheritdoc />
    public IClockDayTimeTimer RegisterTimeOfDay (LocalTime timeOfDay,
        Action<ClockTimerCallbackContext> callback,
        CancellationToken cancellationToken,
        object? state = null,
        DayTimeTimerOptions? timerOptions = null)
    {
        VirtualDayTimeTimerBase dayTimeTimer = new VirtualDayTimeTimer(this,
            timeOfDay,
            IntervalTimerCallbackKind.ContextAction,
            callback,
            state,
            timerOptions,
            cancellationToken);
        lock (_gate)
            _dayTimeTimers.Add(dayTimeTimer);
        return dayTimeTimer;
    }

    /// <inheritdoc />
    public IClockDayTimeTimer RegisterTimeOfDay (LocalTime timeOfDay,
        Action<ClockTimerCallbackContext, CancellationToken> callback,
        CancellationToken cancellationToken,
        object? state = null,
        DayTimeTimerOptions? timerOptions = null)
    {
        VirtualDayTimeTimerBase dayTimeTimer = new VirtualDayTimeTimer(this,
            timeOfDay,
            IntervalTimerCallbackKind.ContextActionWithToken,
            callback,
            state,
            timerOptions,
            cancellationToken);
        lock (_gate)
            _dayTimeTimers.Add(dayTimeTimer);
        return dayTimeTimer;
    }

    /// <inheritdoc />
    public IClockDayTimeTimer RegisterAsyncTimeOfDay (LocalTime timeOfDay,
        Func<CancellationToken, ValueTask> callback,
        CancellationToken cancellationToken,
        DayTimeTimerOptions? timerOptions = null)
    {
        VirtualDayTimeTimerBase dayTimeTimer = new VirtualDayTimeTimer(this,
            timeOfDay,
            IntervalTimerCallbackKind.SimpleAsync,
            callback,
            null,
            timerOptions,
            cancellationToken);
        lock (_gate)
            _dayTimeTimers.Add(dayTimeTimer);
        return dayTimeTimer;
    }

    /// <inheritdoc />
    public IClockDayTimeTimer RegisterAsyncTimeOfDay (LocalTime timeOfDay,
        Func<ClockTimerCallbackContext, CancellationToken, ValueTask> callback,
        CancellationToken cancellationToken,
        object? state = null,
        DayTimeTimerOptions? timerOptions = null)
    {
        VirtualDayTimeTimerBase dayTimeTimer = new VirtualDayTimeTimer(this,
            timeOfDay,
            IntervalTimerCallbackKind.ContextAsync,
            callback,
            state,
            timerOptions,
            cancellationToken);
        lock (_gate)
            _dayTimeTimers.Add(dayTimeTimer);
        return dayTimeTimer;
    }

    /// <inheritdoc />
    public IClockDayTimeTimer RegisterTimeOfDay (LocalTimeOfDay timeOfDay,
        Action callback,
        CancellationToken cancellationToken,
        DayTimeTimerOptions? timerOptions = null) =>
        RegisterTimeOfDay(TimeOnlyToLocalTime(timeOfDay.Value), callback, cancellationToken, timerOptions);

    /// <inheritdoc />
    public IClockDayTimeTimer RegisterTimeOfDay (LocalTimeOfDay timeOfDay,
        Action<ClockTimerCallbackContext> callback,
        CancellationToken cancellationToken,
        object? state = null,
        DayTimeTimerOptions? timerOptions = null) =>
        RegisterTimeOfDay(TimeOnlyToLocalTime(timeOfDay.Value), callback, cancellationToken, state, timerOptions);

    /// <inheritdoc />
    public IClockDayTimeTimer RegisterTimeOfDay (LocalTimeOfDay timeOfDay,
        Action<ClockTimerCallbackContext, CancellationToken> callback,
        CancellationToken cancellationToken,
        object? state = null,
        DayTimeTimerOptions? timerOptions = null) =>
        RegisterTimeOfDay(TimeOnlyToLocalTime(timeOfDay.Value), callback, cancellationToken, state, timerOptions);

    /// <inheritdoc />
    public IClockDayTimeTimer RegisterAsyncTimeOfDay (LocalTimeOfDay timeOfDay,
        Func<CancellationToken, ValueTask> callback,
        CancellationToken cancellationToken,
        DayTimeTimerOptions? timerOptions = null) =>
        RegisterAsyncTimeOfDay(TimeOnlyToLocalTime(timeOfDay.Value), callback, cancellationToken, timerOptions);

    /// <inheritdoc />
    public IClockDayTimeTimer RegisterAsyncTimeOfDay (LocalTimeOfDay timeOfDay,
        Func<ClockTimerCallbackContext, CancellationToken, ValueTask> callback,
        CancellationToken cancellationToken,
        object? state = null,
        DayTimeTimerOptions? timerOptions = null) =>
        RegisterAsyncTimeOfDay(TimeOnlyToLocalTime(timeOfDay.Value), callback, cancellationToken, state, timerOptions);

    /// <inheritdoc />
    public IClockDayTimeTimer RegisterTimeOfDay (UtcTimeOfDay timeOfDay,
        Action callback,
        CancellationToken cancellationToken,
        DayTimeTimerOptions? timerOptions = null)
    {
        VirtualDayTimeTimerBase dayTimeTimer = new VirtualDayTimeTimer(this,
            TimeOnlyToLocalTime(timeOfDay.Value),
            IntervalTimerCallbackKind.SimpleAction,
            callback,
            null,
            timerOptions,
            cancellationToken,
            utcTimeOfDaySchedule: true);
        lock (_gate)
            _dayTimeTimers.Add(dayTimeTimer);
        return dayTimeTimer;
    }

    /// <inheritdoc />
    public IClockDayTimeTimer RegisterTimeOfDay (UtcTimeOfDay timeOfDay,
        Action<ClockTimerCallbackContext> callback,
        CancellationToken cancellationToken,
        object? state = null,
        DayTimeTimerOptions? timerOptions = null)
    {
        VirtualDayTimeTimerBase dayTimeTimer = new VirtualDayTimeTimer(this,
            TimeOnlyToLocalTime(timeOfDay.Value),
            IntervalTimerCallbackKind.ContextAction,
            callback,
            state,
            timerOptions,
            cancellationToken,
            utcTimeOfDaySchedule: true);
        lock (_gate)
            _dayTimeTimers.Add(dayTimeTimer);
        return dayTimeTimer;
    }

    /// <inheritdoc />
    public IClockDayTimeTimer RegisterTimeOfDay (UtcTimeOfDay timeOfDay,
        Action<ClockTimerCallbackContext, CancellationToken> callback,
        CancellationToken cancellationToken,
        object? state = null,
        DayTimeTimerOptions? timerOptions = null)
    {
        VirtualDayTimeTimerBase dayTimeTimer = new VirtualDayTimeTimer(this,
            TimeOnlyToLocalTime(timeOfDay.Value),
            IntervalTimerCallbackKind.ContextActionWithToken,
            callback,
            state,
            timerOptions,
            cancellationToken,
            utcTimeOfDaySchedule: true);
        lock (_gate)
            _dayTimeTimers.Add(dayTimeTimer);
        return dayTimeTimer;
    }

    /// <inheritdoc />
    public IClockDayTimeTimer RegisterAsyncTimeOfDay (UtcTimeOfDay timeOfDay,
        Func<CancellationToken, ValueTask> callback,
        CancellationToken cancellationToken,
        DayTimeTimerOptions? timerOptions = null)
    {
        VirtualDayTimeTimerBase dayTimeTimer = new VirtualDayTimeTimer(this,
            TimeOnlyToLocalTime(timeOfDay.Value),
            IntervalTimerCallbackKind.SimpleAsync,
            callback,
            null,
            timerOptions,
            cancellationToken,
            utcTimeOfDaySchedule: true);
        lock (_gate)
            _dayTimeTimers.Add(dayTimeTimer);
        return dayTimeTimer;
    }

    /// <inheritdoc />
    public IClockDayTimeTimer RegisterAsyncTimeOfDay (UtcTimeOfDay timeOfDay,
        Func<ClockTimerCallbackContext, CancellationToken, ValueTask> callback,
        CancellationToken cancellationToken,
        object? state = null,
        DayTimeTimerOptions? timerOptions = null)
    {
        VirtualDayTimeTimerBase dayTimeTimer = new VirtualDayTimeTimer(this,
            TimeOnlyToLocalTime(timeOfDay.Value),
            IntervalTimerCallbackKind.ContextAsync,
            callback,
            state,
            timerOptions,
            cancellationToken,
            utcTimeOfDaySchedule: true);
        lock (_gate)
            _dayTimeTimers.Add(dayTimeTimer);
        return dayTimeTimer;
    }
#else
    /// <inheritdoc />
    public IClockDayTimeTimer RegisterTimeOfDay (LocalTime timeOfDay,
        Action callback,
        CancellationToken cancellationToken,
        DayTimeTimerOptions? timerOptions = null)
    {
        VirtualDayTimeTimerBase dayTimeTimer = new VirtualDayTimeTimer(this,
            timeOfDay,
            IntervalTimerCallbackKind.SimpleAction,
            callback,
            null,
            timerOptions,
            cancellationToken);
        lock (_gate)
            _dayTimeTimers.Add(dayTimeTimer);
        return dayTimeTimer;
    }

    /// <inheritdoc />
    public IClockDayTimeTimer RegisterTimeOfDay (LocalTime timeOfDay,
        Action<ClockTimerCallbackContext> callback,
        CancellationToken cancellationToken,
        object? state = null,
        DayTimeTimerOptions? timerOptions = null)
    {
        VirtualDayTimeTimerBase dayTimeTimer = new VirtualDayTimeTimer(this,
            timeOfDay,
            IntervalTimerCallbackKind.ContextAction,
            callback,
            state,
            timerOptions,
            cancellationToken);
        lock (_gate)
            _dayTimeTimers.Add(dayTimeTimer);
        return dayTimeTimer;
    }

    /// <inheritdoc />
    public IClockDayTimeTimer RegisterTimeOfDay (LocalTime timeOfDay,
        Action<ClockTimerCallbackContext, CancellationToken> callback,
        CancellationToken cancellationToken,
        object? state = null,
        DayTimeTimerOptions? timerOptions = null)
    {
        VirtualDayTimeTimerBase dayTimeTimer = new VirtualDayTimeTimer(this,
            timeOfDay,
            IntervalTimerCallbackKind.ContextActionWithToken,
            callback,
            state,
            timerOptions,
            cancellationToken);
        lock (_gate)
            _dayTimeTimers.Add(dayTimeTimer);
        return dayTimeTimer;
    }

    /// <inheritdoc />
    public IClockDayTimeTimer RegisterAsyncTimeOfDay (LocalTime timeOfDay,
        Func<CancellationToken, ValueTask> callback,
        CancellationToken cancellationToken,
        DayTimeTimerOptions? timerOptions = null)
    {
        VirtualDayTimeTimerBase dayTimeTimer = new VirtualDayTimeTimer(this,
            timeOfDay,
            IntervalTimerCallbackKind.SimpleAsync,
            callback,
            null,
            timerOptions,
            cancellationToken);
        lock (_gate)
            _dayTimeTimers.Add(dayTimeTimer);
        return dayTimeTimer;
    }

    /// <inheritdoc />
    public IClockDayTimeTimer RegisterAsyncTimeOfDay (LocalTime timeOfDay,
        Func<ClockTimerCallbackContext, CancellationToken, ValueTask> callback,
        CancellationToken cancellationToken,
        object? state = null,
        DayTimeTimerOptions? timerOptions = null)
    {
        VirtualDayTimeTimerBase dayTimeTimer = new VirtualDayTimeTimer(this,
            timeOfDay,
            IntervalTimerCallbackKind.ContextAsync,
            callback,
            state,
            timerOptions,
            cancellationToken);
        lock (_gate)
            _dayTimeTimers.Add(dayTimeTimer);
        return dayTimeTimer;
    }
#endif

    #endregion IPrimeClock Implementation — Time-of-day timers
    #endregion Interface Implementations
}


