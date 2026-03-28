using System.Diagnostics;
using NodaTime;

namespace KZDev.PrimeTime;

/// <summary>
///   Internal callback type for interval timer invocations.
/// </summary>
internal enum PrimeClockIntervalTimerCallbackKind
{
    /// <summary>
    ///   Parameterless <see cref="Action"/> callback.
    /// </summary>
    SimpleAction,

    /// <summary>
    ///   Callback that receives <see cref="PrimeClockTimerCallbackContext"/>.
    /// </summary>
    ContextAction,

    /// <summary>
    ///   Callback that receives <see cref="PrimeClockTimerCallbackContext"/> and
    ///   <see cref="CancellationToken"/>.
    /// </summary>
    ContextActionWithToken,

    /// <summary>
    ///   Async callback that receives <see cref="CancellationToken"/> and returns
    ///   <see cref="ValueTask"/>.
    /// </summary>
    SimpleAsync,

    /// <summary>
    ///   Async callback that receives <see cref="PrimeClockTimerCallbackContext"/> and
    ///   <see cref="CancellationToken"/>, and returns <see cref="ValueTask"/>.
    /// </summary>
    ContextAsync
}

/// <summary>
///   Implementation of <see cref="IClockIntervalTimer"/> used by <see cref="PrimeClock"/>
///   for interval timers.
/// </summary>
internal sealed class PrimeClockIntervalTimerRegistration : IClockIntervalTimer
{
    private static int _nextId;

    private static readonly Duration NoRepeatSentinel = Duration.FromTimeSpan(Timeout.InfiniteTimeSpan);

    private readonly IPrimeClock _clock;
    private readonly bool _captureContext;
    private readonly PrimeClockIntervalTimerCallbackKind _callbackKind;
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
    private Duration _initialCallbackTime;
    private Duration _repeatInterval;
    private Instant? _nextCallbackInstant;
    private Instant? _lastCallbackInstant;
    private TimerState _state;
    private bool _enabled = true;
    private bool _disposed;
    private int _callbacksRunning;
    private bool _cancelRequested;

    #region Constructors/Finalizers

    /// <summary>
    ///   Initializes a new instance of the <see cref="PrimeClockIntervalTimerRegistration"/> class.
    /// </summary>
    internal PrimeClockIntervalTimerRegistration (IPrimeClock clock,
        Duration initialCallbackTime,
        Duration repeatInterval,
        PrimeClockIntervalTimerCallbackKind callbackKind,
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
        RegisteredInstant = clock.NowInstant;
        _lastCallbackInstant = null;
        _nextCallbackInstant = null;

        if (cancellationToken.CanBeCanceled)
        {
            _cancelRegistration = cancellationToken.Register(OnCancelRequested);
            if (cancellationToken.IsCancellationRequested)
            {
                _cancelRequested = true;
                _state = TimerState.Cancelled;
                return;
            }
        }

        _state = TimerState.Active;
        ScheduleNext(initialCallbackTime);
    }

    #endregion Constructors/Finalizers

    /// <inheritdoc />
    public int Id { [DebuggerStepThrough] get; }

    /// <inheritdoc />
    public Instant RegisteredInstant { [DebuggerStepThrough] get; }

    /// <inheritdoc />
    public DateTimeOffset RegisteredTime => RegisteredInstant.ToDateTimeOffset();

    /// <inheritdoc />
    public bool IsTimeOfDay => false;

    /// <inheritdoc />
    public bool IsResetAfterCallback { [DebuggerStepThrough] get; }

    /// <inheritdoc />
    public bool IsLocalTimeRepresentation { [DebuggerStepThrough] get; }

    /// <inheritdoc />
    public bool IsRepeating =>
        _repeatInterval > Duration.Zero && _repeatInterval != NoRepeatSentinel;

    /// <inheritdoc />
    public bool IsCancelled => _state == TimerState.Cancelled;

    /// <inheritdoc />
    public bool IsActive =>
        _state != TimerState.Cancelled &&
        _state != TimerState.Completed &&
        _state != TimerState.Disposed &&
        _enabled;

    /// <inheritdoc />
    public TimerState State => _state;

    /// <inheritdoc />
    public bool CallbacksProcessing => _callbacksRunning > 0;

    /// <inheritdoc />
    public bool Enabled
    {
        get => _enabled && !IsCancelled && _state != TimerState.Disposed;
        set
        {
            lock (_gate)
            {
                if (_disposed || _state == TimerState.Cancelled)
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
                if (_lastCallbackInstant is not { } last)
                    return -1;
                if (_callbacksRunning > 0)
                    return 0;
                Instant now = _clock.NowInstant;
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
                if (!IsRepeating && _lastCallbackInstant.HasValue)
                    return -1;
                if (_nextCallbackInstant is not { } next)
                    return -1;
                Instant now = _clock.NowInstant;
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
            if (_disposed || _state == TimerState.Cancelled)
                return;
            _cancelRequested = true;
            _state = TimerState.Cancelled;
            _enabled = false;
            _timer?.Change(Timeout.Infinite, Timeout.Infinite);
        }
    }

    private static int DurationToTimerMilliseconds (Duration duration)
    {
        if (duration <= Duration.Zero)
            return Timeout.Infinite;
        try
        {
            TimeSpan ts = duration.ToTimeSpan();
            long msLong = (long)Math.Min(ts.TotalMilliseconds, int.MaxValue);
            if (msLong <= 0)
                return Timeout.Infinite;
            return (int)msLong;
        }
        catch (OverflowException)
        {
            return int.MaxValue;
        }
    }

    private void ScheduleNext (Duration delay)
    {
        if (delay <= Duration.Zero || delay == NoRepeatSentinel)
            return;
        int ms = DurationToTimerMilliseconds(delay);
        if (ms <= 0)
            return;
        _nextCallbackInstant = _clock.NowInstant + delay;
        if (_timer is null)
            _timer = new Timer(OnTimerTick, null, ms, Timeout.Infinite);
        else
            _timer.Change(ms, Timeout.Infinite);
    }

    private void OnTimerTick (object? _)
    {
        lock (_gate)
        {
            if (_disposed || _cancelRequested || _state == TimerState.Cancelled || !_enabled)
                return;
            _timer!.Change(Timeout.Infinite, Timeout.Infinite);
        }

        Instant now = _clock.NowInstant;
        _lastCallbackInstant = now;
        _nextCallbackInstant = null;
        bool isRepeating = IsRepeating;
        bool resetAfter = IsResetAfterCallback;
        TimerState stateDuringCallback = isRepeating && !resetAfter
            ? TimerState.RepeatProcessingCallback
            : TimerState.ProcessingCallback;
        lock (_gate)
        {
            _state = stateDuringCallback;
            _callbacksRunning++;
        }

        try
        {
            RunCallback(stateDuringCallback, isRepeating);
        }
        finally
        {
            lock (_gate)
            {
                _callbacksRunning--;
            }
        }
    }

    /// <summary>
    ///   Invokes a synchronous callback with the configured execution context behavior:
    ///   either capture and restore the calling context, or suppress flow (Unsafe).
    /// </summary>
    /// <param name="run">The synchronous action to run.</param>
    private void InvokeSynchronousCallbackWithExecutionContext (Action run)
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

    private void RunCallback (TimerState stateDuringCallback, bool isRepeating)
    {
        switch (_callbackKind)
        {
            case PrimeClockIntervalTimerCallbackKind.SimpleAction:
                InvokeSynchronousCallbackWithExecutionContext(() => ((Action)_callback)());
                break;
            case PrimeClockIntervalTimerCallbackKind.ContextAction:
                InvokeSynchronousCallbackWithExecutionContext(() => ((Action<PrimeClockTimerCallbackContext>)_callback)(new PrimeClockTimerCallbackContext(this, _callbackState)));
                break;
            case PrimeClockIntervalTimerCallbackKind.ContextActionWithToken:
                InvokeSynchronousCallbackWithExecutionContext(() =>
                    ((Action<PrimeClockTimerCallbackContext, CancellationToken>)_callback)(new PrimeClockTimerCallbackContext(this, _callbackState),
                        _cancellationToken));
                break;
            case PrimeClockIntervalTimerCallbackKind.SimpleAsync:
                RunAsyncAndScheduleAfter(() => ((Func<CancellationToken, ValueTask>)_callback)(_cancellationToken),
                    isRepeating);
                return;
            case PrimeClockIntervalTimerCallbackKind.ContextAsync:
                RunAsyncAndScheduleAfter(() => ((Func<PrimeClockTimerCallbackContext, CancellationToken, ValueTask>)_callback)(new PrimeClockTimerCallbackContext(this, _callbackState),
                        _cancellationToken),
                    isRepeating);
                return;
            default:
                throw new InvalidOperationException($"Unsupported callback kind: {_callbackKind}");
        }

        OnCallbackCompleted(isRepeating);
    }

    private void RunAsyncAndScheduleAfter (Func<ValueTask> run, bool isRepeating)
    {
        ValueTask vt = run();
        if (vt.IsCompletedSuccessfully)
        {
            OnCallbackCompleted(isRepeating);
            return;
        }
        vt.AsTask().ContinueWith((_, state) =>
            {
                (PrimeClockIntervalTimerRegistration reg, bool rep) =
                    ((PrimeClockIntervalTimerRegistration, bool))state!;
                reg.OnCallbackCompleted(rep);
            },
            (this, isRepeating),
            CancellationToken.None,
            TaskContinuationOptions.None,
            TaskScheduler.Default);
    }

    /// <summary>
    ///   Updates state and optionally schedules the next tick after a timer callback completes
    ///   (sync or async).
    /// </summary>
    /// <param name="isRepeating">Whether the timer is repeating; if <c>false</c>, state is set to <see cref="TimerState.Completed"/>.</param>
    private void OnCallbackCompleted (bool isRepeating)
    {
        lock (_gate)
        {
            if (_disposed || _cancelRequested || _state == TimerState.Cancelled)
                return;
            if (!isRepeating)
            {
                _state = TimerState.Completed;
                return;
            }
            _state = TimerState.RepeatCycle;
            ScheduleNext(_repeatInterval);
        }
    }

    #region IClockIntervalTimer Implementation

    /// <inheritdoc />
    public bool Change (TimeSpan interval)
    {
        Duration duration = Duration.FromTimeSpan(interval);
        return Change(duration, IsRepeating ? duration : NoRepeatSentinel);
    }

    /// <inheritdoc />
    public bool Change (TimeSpan nextInterval, TimeSpan repeatInterval)
    {
        Duration next = Duration.FromTimeSpan(nextInterval);
        // Duration has no infinite value; use NoRepeatSentinel to represent Timeout.InfiniteTimeSpan.
        Duration repeat = repeatInterval == Timeout.InfiniteTimeSpan ? NoRepeatSentinel : Duration.FromTimeSpan(repeatInterval);
        return Change(next, repeat);
    }

    /// <inheritdoc />
    public bool Change (Duration interval) =>
        Change(interval, IsRepeating ? interval : NoRepeatSentinel);

    /// <inheritdoc />
    public bool Change (Duration nextInterval, Duration repeatInterval)
    {
        lock (_gate)
        {
            if (_disposed || _state == TimerState.Cancelled)
                return false;
            bool wouldBeRepeating = repeatInterval > Duration.Zero && repeatInterval != NoRepeatSentinel;
            if (!IsRepeating && wouldBeRepeating)
                throw new InvalidOperationException("Cannot change a non-repeating timer to a repeating timer.");
            _initialCallbackTime = nextInterval;
            _repeatInterval = repeatInterval;
            if (_state == TimerState.Completed)
                _state = TimerState.Active;
            if (!_enabled)
                return true;
            ScheduleNext(nextInterval);
            return true;
        }
    }

    #endregion IClockIntervalTimer Implementation

    #region IRegisteredTimer Implementation

    /// <inheritdoc />
    public void Cancel ()
    {
        lock (_gate)
        {
            if (_state == TimerState.Cancelled || _disposed)
                return;
            _cancelRequested = true;
            _state = TimerState.Cancelled;
            _enabled = false;
            _timer?.Change(Timeout.Infinite, Timeout.Infinite);
        }
    }

    /// <inheritdoc />
    public bool Stop ()
    {
        lock (_gate)
        {
            if (!_enabled || _state == TimerState.Cancelled || _disposed)
                return false;
            _enabled = false;
            _state = TimerState.Disabled;
            _timer?.Change(Timeout.Infinite, Timeout.Infinite);
            return true;
        }
    }

    /// <inheritdoc />
    public bool Start ()
    {
        lock (_gate)
        {
            if (_disposed || _state == TimerState.Cancelled)
                return false;
            if (_state != TimerState.Completed && _state != TimerState.Disabled)
                return false;
            _enabled = true;
            _state = TimerState.Active;
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
            _state = TimerState.Disposed;
            _enabled = false;
            _cancelRegistration.Dispose();
            _timer?.Dispose();
            _timer = null;
        }
    }

    #endregion IRegisteredTimer Implementation
}

