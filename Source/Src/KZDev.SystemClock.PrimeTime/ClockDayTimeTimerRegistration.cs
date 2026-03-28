#if NET

using System.Diagnostics;
using KZDev.PrimeTime;

namespace KZDev.SystemClock.PrimeTime;

/// <summary>
///   Implementation of <see cref="IClockDayTimeTimer"/> used by <see cref="PrimeClock"/>.
/// </summary>
internal sealed class ClockDayTimeTimerRegistration : IClockDayTimeTimer
{
    private static int _nextId;
    private static readonly TimeSpan OneDay = TimeSpan.FromDays(1);
    private static readonly TimeSpan RunSequentiallyRetryDelay = TimeSpan.FromMilliseconds(30);

    private readonly IPrimeClock _clock;
    private readonly bool _isLocal;
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
    private TimeOnly _targetTimeOfDay;
    private DateTimeOffset? _nextCallbackUtc;
    private TimerState _state;
    private bool _enabled = true;
    private bool _disposed;
    private int _callbacksRunning;
    private bool _cancelRequested;
    private bool _pendingRunSequential;

    #region Constructors/Finalizers

    /// <summary>
    ///   Initializes a new instance of the <see cref="ClockDayTimeTimerRegistration"/> class
    ///   for a local-time day-time timer.
    /// </summary>
    /// <param name="clock">
    ///   The BCL clock used for scheduling and callback execution.
    /// </param>
    /// <param name="timeOfDay">
    ///   The local time of day at which the callback should run.
    /// </param>
    /// <param name="callbackKind">
    ///   The callback signature kind used to invoke <paramref name="callback"/>.
    /// </param>
    /// <param name="callback">
    ///   The callback delegate to invoke when the timer fires.
    /// </param>
    /// <param name="callbackState">
    ///   Optional state passed to callbacks that accept <see cref="ClockTimerCallbackContext"/>.
    /// </param>
    /// <param name="options">
    ///   Optional day-time timer options that control callback scheduling and execution behavior.
    /// </param>
    /// <param name="cancellationToken">
    ///   Token that requests cancellation of this timer registration.
    /// </param>
    /// <exception cref="ArgumentNullException">
    ///   Thrown when <paramref name="clock"/> or <paramref name="callback"/> is <c>null</c>.
    /// </exception>
    internal ClockDayTimeTimerRegistration (IPrimeClock clock,
        LocalTimeOfDay timeOfDay,
        IntervalTimerCallbackKind callbackKind,
        Delegate callback,
        object? callbackState,
        DayTimeTimerOptions? options,
        CancellationToken cancellationToken)
        : this(clock, true, timeOfDay.Value, callbackKind, callback, callbackState, options, cancellationToken)
    {
    }

    /// <summary>
    ///   Initializes a new instance of the <see cref="ClockDayTimeTimerRegistration"/> class
    ///   for a UTC day-time timer.
    /// </summary>
    /// <param name="clock">
    ///   The BCL clock used for scheduling and callback execution.
    /// </param>
    /// <param name="timeOfDay">
    ///   The UTC time of day at which the callback should run.
    /// </param>
    /// <param name="callbackKind">
    ///   The callback signature kind used to invoke <paramref name="callback"/>.
    /// </param>
    /// <param name="callback">
    ///   The callback delegate to invoke when the timer fires.
    /// </param>
    /// <param name="callbackState">
    ///   Optional state passed to callbacks that accept <see cref="ClockTimerCallbackContext"/>.
    /// </param>
    /// <param name="options">
    ///   Optional day-time timer options that control callback scheduling and execution behavior.
    /// </param>
    /// <param name="cancellationToken">
    ///   Token that requests cancellation of this timer registration.
    /// </param>
    /// <exception cref="ArgumentNullException">
    ///   Thrown when <paramref name="clock"/> or <paramref name="callback"/> is <c>null</c>.
    /// </exception>
    internal ClockDayTimeTimerRegistration (IPrimeClock clock,
        UtcTimeOfDay timeOfDay,
        IntervalTimerCallbackKind callbackKind,
        Delegate callback,
        object? callbackState,
        DayTimeTimerOptions? options,
        CancellationToken cancellationToken)
        : this(clock, false, timeOfDay.Value, callbackKind, callback, callbackState, options, cancellationToken)
    {
    }

    private ClockDayTimeTimerRegistration (IPrimeClock clock,
        bool isLocal,
        TimeOnly targetTimeOfDay,
        IntervalTimerCallbackKind callbackKind,
        Delegate callback,
        object? callbackState,
        DayTimeTimerOptions? options,
        CancellationToken cancellationToken)
    {
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _isLocal = isLocal;
        _targetTimeOfDay = targetTimeOfDay;
        _callbackKind = callbackKind;
        _callback = callback ?? throw new ArgumentNullException(nameof(callback));
        _callbackState = callbackState;

        DayTimeTimerOptions opts = options ?? new DayTimeTimerOptions();
        ConcurrentTriggerProcessing = opts.ConcurrentTriggerProcessing;
        SkippedTimeBehavior = opts.SkippedTimeBehavior;
        DuplicateTimeBehavior = opts.DuplicateTimeBehavior;
        _captureContext = opts.CallbackExecutionContext != TimerCallbackExecutionContext.Unsafe;
        _cancellationToken = cancellationToken;

        Id = Interlocked.Increment(ref _nextId);
        RegisteredTime = _clock.UtcNowOffset;
        IsLocalTimeRepresentation = isLocal;

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
        ScheduleNext();
    }

    #endregion Constructors/Finalizers

    /// <inheritdoc />
    public int Id { [DebuggerStepThrough] get; }
    /// <inheritdoc />
    public DateTimeOffset RegisteredTime { [DebuggerStepThrough] get; }
    /// <inheritdoc />
    public bool IsTimeOfDay => true;
    /// <inheritdoc />
    public bool IsLocalTimeRepresentation { [DebuggerStepThrough] get; }
    /// <inheritdoc />
    public bool IsRepeating => true;
    /// <inheritdoc />
    public bool IsCancelled => _state == TimerState.Cancelled;
    /// <inheritdoc />
    public bool IsActive =>
        _state != TimerState.Cancelled &&
        _state != TimerState.Disposed &&
        _enabled;
    /// <inheritdoc />
    public TimerState State => _state;
    /// <inheritdoc />
    public bool CallbacksProcessing => _callbacksRunning > 0;
    /// <inheritdoc />
    public ConcurrentTriggerProcessing ConcurrentTriggerProcessing { [DebuggerStepThrough] get; }
    /// <inheritdoc />
    public SkippedTimeBehavior SkippedTimeBehavior { [DebuggerStepThrough] get; }
    /// <inheritdoc />
    public DuplicateTimeBehavior DuplicateTimeBehavior { [DebuggerStepThrough] get; }
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

    /// <summary>
    ///   Computes the delay until the next occurrence of the target time of day.
    /// </summary>
    private TimeSpan GetDelayUntilNext ()
    {
        DateTimeOffset now = _isLocal ? _clock.LocalNowOffset : _clock.UtcNowOffset;
        DateOnly today = DateOnly.FromDateTime(now.DateTime);
        DateTime nextDt = today.ToDateTime(_targetTimeOfDay);
        if (nextDt <= now.DateTime)
            nextDt = today.AddDays(1).ToDateTime(_targetTimeOfDay);
        DateTimeOffset nextOff = _isLocal
            ? new DateTimeOffset(nextDt, now.Offset)
            : new DateTimeOffset(nextDt, TimeSpan.Zero);
        TimeSpan delay = nextOff - now;
        if (delay <= TimeSpan.Zero)
            delay = OneDay;
        return delay;
    }

    private void ScheduleNext ()
    {
        lock (_gate)
        {
            if (_disposed || _cancelRequested || _state == TimerState.Cancelled || !_enabled)
                return;
            TimeSpan delay = GetDelayUntilNext();
            long msLong = (long)Math.Min(delay.TotalMilliseconds, int.MaxValue);
            if (msLong < 0)
                msLong = 0;
            int ms = (int)msLong;
            DateTimeOffset now = _isLocal ? _clock.LocalNowOffset : _clock.UtcNowOffset;
            _nextCallbackUtc = now + delay;
            if (_timer is null)
                _timer = new Timer(OnTimerTick, null, ms, Timeout.Infinite);
            else
                _timer.Change(ms, Timeout.Infinite);
        }
    }

    private void ScheduleNextAfterShortDelay ()
    {
        lock (_gate)
        {
            if (_disposed || _cancelRequested || _state == TimerState.Cancelled || !_enabled)
                return;
            int ms = (int)Math.Min(RunSequentiallyRetryDelay.TotalMilliseconds, int.MaxValue);
            if (_timer is null)
                _timer = new Timer(OnTimerTick, null, ms, Timeout.Infinite);
            else
                _timer.Change(ms, Timeout.Infinite);
        }
    }

    private void OnTimerTick (object? _)
    {
        lock (_gate)
        {
            if (_disposed || _cancelRequested || _state == TimerState.Cancelled || !_enabled)
                return;
            _timer!.Change(Timeout.Infinite, Timeout.Infinite);
        }

        if (ConcurrentTriggerProcessing == ConcurrentTriggerProcessing.Skip && _callbacksRunning > 0)
        {
            ScheduleNext();
            return;
        }

        if (ConcurrentTriggerProcessing == ConcurrentTriggerProcessing.RunSequentially && _callbacksRunning > 0)
        {
            lock (_gate)
            {
                _pendingRunSequential = true;
            }
            ScheduleNextAfterShortDelay();
            return;
        }

        _nextCallbackUtc = null;
        lock (_gate)
        {
            _state = TimerState.RepeatProcessingCallback;
            _callbacksRunning++;
        }

        bool isAsync = _callbackKind == IntervalTimerCallbackKind.SimpleAsync ||
            _callbackKind == IntervalTimerCallbackKind.ContextAsync;
        try
        {
            RunCallback();
        }
        finally
        {
            if (!isAsync)
            {
                lock (_gate)
                {
                    _callbacksRunning--;
                }
            }
        }
        if (!isAsync)
        {
            bool doRetry;
            lock (_gate)
            {
                doRetry = _pendingRunSequential;
                _pendingRunSequential = false;
            }
            if (doRetry)
                ScheduleNextAfterShortDelay();
            else
                ScheduleNext();
        }
    }

    private void RunCallback ()
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
                RunAsyncAndScheduleAfter(() => ((Func<CancellationToken, ValueTask>)_callback)(_cancellationToken));
                return;
            case IntervalTimerCallbackKind.ContextAsync:
                RunAsyncAndScheduleAfter(() => ((Func<ClockTimerCallbackContext, CancellationToken, ValueTask>)_callback)(new ClockTimerCallbackContext(this, _callbackState),
                    _cancellationToken));
                return;
            default:
                throw new InvalidOperationException($"Unsupported callback kind: {_callbackKind}");
        }
    }

    private void RunAsyncAndScheduleAfter (Func<ValueTask> run)
    {
        ValueTask vt = run();
        if (vt.IsCompletedSuccessfully)
        {
            ScheduleNextFromAsync();
            return;
        }
        vt.AsTask().ContinueWith((_, state) =>
            {
                ((ClockDayTimeTimerRegistration)state!).ScheduleNextFromAsync();
            },
            this,
            CancellationToken.None,
            TaskContinuationOptions.None,
            TaskScheduler.Default);
    }

    private void ScheduleNextFromAsync ()
    {
        lock (_gate)
        {
            _callbacksRunning--;
            if (_disposed || _cancelRequested || _state == TimerState.Cancelled)
                return;
            if (_pendingRunSequential)
            {
                _pendingRunSequential = false;
                ScheduleNextAfterShortDelay();
                return;
            }
        }
        ScheduleNext();
    }

    #region IClockDayTimeTimer Implementation

    /// <inheritdoc />
    public bool Change (LocalTimeOfDay newTimeOfDay)
    {
        if (!_isLocal)
            return false;
        lock (_gate)
        {
            if (_disposed || _state == TimerState.Cancelled)
                return false;
            _targetTimeOfDay = newTimeOfDay.Value;
            if (!_enabled)
                return true;
            ScheduleNext();
            return true;
        }
    }

    /// <inheritdoc />
    public bool Change (UtcTimeOfDay newTimeOfDay)
    {
        if (_isLocal)
            return false;
        lock (_gate)
        {
            if (_disposed || _state == TimerState.Cancelled)
                return false;
            _targetTimeOfDay = newTimeOfDay.Value;
            if (!_enabled)
                return true;
            ScheduleNext();
            return true;
        }
    }

    #endregion IClockDayTimeTimer Implementation

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
            if (_state != TimerState.Disabled)
                return false;
            _enabled = true;
            _state = TimerState.Active;
            ScheduleNext();
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
#endif

