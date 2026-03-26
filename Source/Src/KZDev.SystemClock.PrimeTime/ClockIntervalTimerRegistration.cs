// Copyright (c) Kevin Zehrer. All rights reserved.
// This file is part of the PrimeTime project.

using System.Diagnostics;

namespace KZDev.PrimeTime;

/// <summary>
///   Internal callback type for interval timer invocations.
/// </summary>
internal enum IntervalTimerCallbackKind
{
    /// <summary>
    ///   Parameterless <see cref="Action"/> callback.
    /// </summary>
    SimpleAction,
    /// <summary>
    ///   Callback that receives <see cref="ClockTimerCallbackContext"/>.
    /// </summary>
    ContextAction,
    /// <summary>
    ///   Callback that receives <see cref="ClockTimerCallbackContext"/> and
    ///   <see cref="CancellationToken"/>.
    /// </summary>
    ContextActionWithToken,
    /// <summary>
    ///   Async callback that receives <see cref="CancellationToken"/> and returns
    ///   <see cref="ValueTask"/>.
    /// </summary>
    SimpleAsync,
    /// <summary>
    ///   Async callback that receives <see cref="ClockTimerCallbackContext"/> and
    ///   <see cref="CancellationToken"/>, and returns <see cref="ValueTask"/>.
    /// </summary>
    ContextAsync
}

/// <summary>
///   Implementation of <see cref="IClockIntervalTimer"/> used by <see cref="PrimeSystemClock"/>.
/// </summary>
internal sealed class ClockIntervalTimerRegistration : IClockIntervalTimer
{
    private static int _nextId;
    private readonly IPrimeSystemClock _clock;
    private readonly bool _captureContext;
    private readonly IntervalTimerCallbackKind _callbackKind;
    private readonly Delegate _callback;
    private readonly object? _callbackState;
    private readonly CancellationToken _cancellationToken;
    private readonly CancellationTokenRegistration _cancelRegistration;
#if NET10_OR_GREATER
    private readonly Lock _gate = new();
#else
    private readonly object _gate = new();
#endif
    private Timer? _timer;
    private TimeSpan _initialCallbackTime;
    private TimeSpan _repeatInterval;
    private DateTimeOffset? _nextCallbackUtc;
    private DateTimeOffset? _lastCallbackUtc;
    private bool _enabled = true;
    private bool _disposed;
    private int _callbacksRunning;
    private bool _cancelRequested;

    #region Constructors/Finalizers

    /// <summary>
    ///   Initializes a new instance of the <see cref="ClockIntervalTimerRegistration"/> class.
    /// </summary>
    /// <param name="clock">
    ///   The system clock used for scheduling and time.
    /// </param>
    /// <param name="initialCallbackTime">
    ///   Delay until the first callback.
    /// </param>
    /// <param name="repeatInterval">
    ///   Interval between subsequent callbacks, or <see cref="Timeout.InfiniteTimeSpan"/> for
    ///   one-shot.
    /// </param>
    /// <param name="callbackKind">
    ///   The kind of callback (sync/async, with or without context/token).
    /// </param>
    /// <param name="callback">
    ///   The delegate to invoke on each tick.
    /// </param>
    /// <param name="callbackState">
    ///   Optional state passed to the callback via <see cref="ClockTimerCallbackContext"/>.
    /// </param>
    /// <param name="options">
    ///   Timer options (reset-after-callback, execution context, local time). May be
    ///   <c>null</c> for defaults.
    /// </param>
    /// <param name="cancellationToken">
    ///   Token to cancel the registration.
    /// </param>
    internal ClockIntervalTimerRegistration (IPrimeSystemClock clock,
        TimeSpan initialCallbackTime,
        TimeSpan repeatInterval,
        IntervalTimerCallbackKind callbackKind,
        Delegate callback,
        object? callbackState,
        IntervalTimerOptions? options,
        CancellationToken cancellationToken)
    {
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _callbackKind = callbackKind;
        _callback = callback ?? throw new ArgumentNullException(nameof(callback));
        _callbackState = callbackState;
        _initialCallbackTime = initialCallbackTime;
        _repeatInterval = repeatInterval;
        IntervalTimerOptions opts = options ?? new IntervalTimerOptions();
        IsResetAfterCallback = opts.ResetIntervalAfterCallback;
        IsLocalTimeRepresentation = opts.LocalTimeRepresentation;
        _captureContext = opts.CallbackExecutionContext != TimerCallbackExecutionContext.Unsafe;
        _cancellationToken = cancellationToken;
        Id = Interlocked.Increment(ref _nextId);
        RegisteredTime = IsLocalTimeRepresentation ? clock.LocalNow : clock.UtcNow;
        _lastCallbackUtc = null;
        _nextCallbackUtc = null;

        if (cancellationToken.CanBeCanceled)
        {
            _cancelRegistration = cancellationToken.Register(OnCancelRequested);
            if (cancellationToken.IsCancellationRequested)
            {
                _cancelRequested = true;
                State = TimerState.Cancelled;
                return;
            }
        }

        State = TimerState.Active;
        ScheduleNext(initialCallbackTime);
    }

    #endregion Constructors/Finalizers

    /// <inheritdoc />
    public int Id { [DebuggerStepThrough] get; }
    /// <inheritdoc />
    public DateTimeOffset RegisteredTime { [DebuggerStepThrough] get; }

    /// <inheritdoc />
    public bool IsTimeOfDay => false;
    /// <inheritdoc />
    public bool IsResetAfterCallback { [DebuggerStepThrough] get; }
    /// <inheritdoc />
    public bool IsLocalTimeRepresentation { [DebuggerStepThrough] get; }

    /// <inheritdoc />
    public bool IsRepeating => _repeatInterval != Timeout.InfiniteTimeSpan && _repeatInterval > TimeSpan.Zero;

    /// <inheritdoc />
    public bool IsCancelled => State == TimerState.Cancelled;
    /// <inheritdoc />
    public bool IsActive =>
        State != TimerState.Cancelled &&
        State != TimerState.Completed &&
        State != TimerState.Disposed &&
        _enabled;

    /// <inheritdoc />
    public TimerState State { [DebuggerStepThrough] get; [DebuggerStepThrough] private set; }
    /// <inheritdoc />
    public bool CallbacksProcessing => _callbacksRunning > 0;
    /// <inheritdoc />
    public bool Enabled
    {
        get => _enabled && !IsCancelled && State != TimerState.Disposed;
        set
        {
            lock (_gate)
            {
                if (_disposed || State == TimerState.Cancelled)
                    return;
                if (value)
                    Start();
                else
                    Stop();
            }
        }
    }

    /// <inheritdoc />
    public long ElapsedTime
    {
        get
        {
            lock (_gate)
            {
                if (_lastCallbackUtc is not { } last)
                    return -1;
                if (_callbacksRunning > 0)
                    return 0;
                DateTimeOffset now = _clock.UtcNow;
                if (last >= now)
                    return 0;
                return (long)(now - last).TotalMilliseconds;
            }
        }
    }

    /// <inheritdoc />
    public long TimeUntilNextCallback
    {
        get
        {
            lock (_gate)
            {
                if (!IsRepeating && _lastCallbackUtc.HasValue)
                    return -1;
                if (_nextCallbackUtc is not { } next)
                    return -1;
                DateTimeOffset now = _clock.UtcNow;
                if (next <= now)
                    return 0;
                if (_callbacksRunning > 0 && IsResetAfterCallback)
                    return (long)_repeatInterval.TotalMilliseconds;
                return (long)(next - now).TotalMilliseconds;
            }
        }
    }

    private void OnCancelRequested ()
    {
        lock (_gate)
        {
            if (_disposed || State == TimerState.Cancelled)
                return;
            _cancelRequested = true;
            State = TimerState.Cancelled;
            _enabled = false;
            _timer?.Change(Timeout.Infinite, Timeout.Infinite);
        }
    }

    private void ScheduleNext (TimeSpan delay)
    {
        if (delay < TimeSpan.Zero || delay == Timeout.InfiniteTimeSpan)
            return;
        long msLong = (long)Math.Min(delay.TotalMilliseconds, int.MaxValue);
        if (msLong < 0)
            msLong = 0;
        int ms = (int)msLong;
        _nextCallbackUtc = _clock.UtcNow + delay;
        if (_timer is null)
            _timer = new Timer(OnTimerTick, null, ms, Timeout.Infinite);
        else
            _timer.Change(ms, Timeout.Infinite);
    }

    private void OnTimerTick (object? _)
    {
        lock (_gate)
        {
            if (_disposed || _cancelRequested || State == TimerState.Cancelled || !_enabled)
                return;
            _timer!.Change(Timeout.Infinite, Timeout.Infinite);
        }

        DateTimeOffset now = _clock.UtcNow;
        _lastCallbackUtc = now;
        _nextCallbackUtc = null;
        bool isRepeating = IsRepeating;
        bool resetAfter = IsResetAfterCallback;
        TimerState stateDuringCallback = isRepeating && !resetAfter
            ? TimerState.RepeatProcessingCallback
            : TimerState.ProcessingCallback;
        lock (_gate)
        {
            State = stateDuringCallback;
            _callbacksRunning++;
        }

        try
        {
            RunCallback(stateDuringCallback, resetAfter, isRepeating);
        }
        finally
        {
            lock (_gate)
            {
                _callbacksRunning--;
            }
        }
    }

    private void RunCallback (TimerState stateDuringCallback, bool resetAfter, bool isRepeating)
    {
        void InvokeSync (Action run)
        {
            if (_captureContext && !ExecutionContext.IsFlowSuppressed())
            {
                ExecutionContext? ec = ExecutionContext.Capture();
                if (ec is not null)
                {
                    ExecutionContext.Run(ec, _ => run(), null);
                    return;
                }
            }
            if (!_captureContext && !ExecutionContext.IsFlowSuppressed())
            {
                using (ExecutionContext.SuppressFlow())
                {
                    run();
                }
                return;
            }
            run();
        }

        switch (_callbackKind)
        {
            case IntervalTimerCallbackKind.SimpleAction:
                InvokeSync(() => ((Action)_callback)());
                break;
            case IntervalTimerCallbackKind.ContextAction:
                InvokeSync(() => ((Action<ClockTimerCallbackContext>)_callback)(new ClockTimerCallbackContext(this, _callbackState)));
                break;
            case IntervalTimerCallbackKind.ContextActionWithToken:
                InvokeSync(() => ((Action<ClockTimerCallbackContext, CancellationToken>)_callback)(new ClockTimerCallbackContext(this, _callbackState),
                    _cancellationToken));
                break;
            case IntervalTimerCallbackKind.SimpleAsync:
                RunAsyncAndScheduleAfter(() => ((Func<CancellationToken, ValueTask>)_callback)(_cancellationToken),
                    resetAfter,
                    isRepeating);
                return;
            case IntervalTimerCallbackKind.ContextAsync:
                RunAsyncAndScheduleAfter(() => ((Func<ClockTimerCallbackContext, CancellationToken, ValueTask>)_callback)(new ClockTimerCallbackContext(this, _callbackState),
                    _cancellationToken),
                    resetAfter,
                    isRepeating);
                return;
            default:
                throw new InvalidOperationException($"Unsupported callback kind: {_callbackKind}");
        }

        OnSyncCallbackCompleted(resetAfter, isRepeating);
    }

    private void RunAsyncAndScheduleAfter (Func<ValueTask> run, bool resetAfter, bool isRepeating)
    {
        ValueTask vt = run();
        if (vt.IsCompletedSuccessfully)
        {
            OnAsyncCallbackCompleted(resetAfter, isRepeating);
            return;
        }
        vt.AsTask().ContinueWith((_, state) =>
            {
                (ClockIntervalTimerRegistration reg, bool ra, bool rep) =
                    ((ClockIntervalTimerRegistration, bool, bool))state!;
                reg.OnAsyncCallbackCompleted(ra, rep);
            },
            (this, resetAfter, isRepeating),
            CancellationToken.None,
            TaskContinuationOptions.None,
            TaskScheduler.Default);
    }

    private void OnSyncCallbackCompleted (bool resetAfter, bool isRepeating)
    {
        lock (_gate)
        {
            if (_disposed || _cancelRequested || State == TimerState.Cancelled)
                return;
            if (!isRepeating)
            {
                State = TimerState.Completed;
                return;
            }
            State = TimerState.RepeatCycle;
            TimeSpan next = _repeatInterval;
            ScheduleNext(next);
        }
    }

    private void OnAsyncCallbackCompleted (bool resetAfter, bool isRepeating)
    {
        lock (_gate)
        {
            if (_disposed || _cancelRequested || State == TimerState.Cancelled)
                return;
            if (!isRepeating)
            {
                State = TimerState.Completed;
                return;
            }
            State = TimerState.RepeatCycle;
            ScheduleNext(_repeatInterval);
        }
    }

    #region Interface Implementations

    /// <inheritdoc />
    public bool Change (TimeSpan interval) =>
        Change(interval, IsRepeating ? interval : Timeout.InfiniteTimeSpan);

    /// <inheritdoc />
    public bool Change (TimeSpan nextInterval, TimeSpan repeatInterval)
    {
        lock (_gate)
        {
            if (_disposed || State == TimerState.Cancelled)
                return false;
            if (!IsRepeating && repeatInterval != Timeout.InfiniteTimeSpan && repeatInterval > TimeSpan.Zero)
                throw new InvalidOperationException("Cannot change a non-repeating timer to a repeating timer.");
            _initialCallbackTime = nextInterval;
            _repeatInterval = repeatInterval;
            if (State == TimerState.Completed)
                State = TimerState.Active;
            if (!_enabled)
                return true;
            ScheduleNext(nextInterval);
            return true;
        }
    }

    /// <inheritdoc />
    public void Cancel ()
    {
        lock (_gate)
        {
            if (State == TimerState.Cancelled || _disposed)
                return;
            _cancelRequested = true;
            State = TimerState.Cancelled;
            _enabled = false;
            _timer?.Change(Timeout.Infinite, Timeout.Infinite);
        }
    }

    /// <inheritdoc />
    public bool Stop ()
    {
        lock (_gate)
        {
            if (!_enabled || State == TimerState.Cancelled || _disposed)
                return false;
            _enabled = false;
            State = TimerState.Disabled;
            _timer?.Change(Timeout.Infinite, Timeout.Infinite);
            return true;
        }
    }

    /// <inheritdoc />
    public bool Start ()
    {
        lock (_gate)
        {
            if (_disposed || State == TimerState.Cancelled)
                return false;
            if (State != TimerState.Completed && State != TimerState.Disabled)
                return false;
            _enabled = true;
            State = TimerState.Active;
            ScheduleNext(_initialCallbackTime);
            return true;
        }
    }

    /// <inheritdoc />
    public void Dispose ()
    {
        lock (_gate)
        {
            if (_disposed)
                return;
            _disposed = true;
            State = TimerState.Disposed;
            _enabled = false;
            _cancelRegistration.Dispose();
            _timer?.Dispose();
            _timer = null;
        }
    }

    #endregion Interface Implementations
}
