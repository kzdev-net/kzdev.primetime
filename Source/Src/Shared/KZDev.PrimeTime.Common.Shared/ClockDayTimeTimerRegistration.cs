// Copyright (c) Kevin Zehrer. All rights reserved.
// This file is part of the PrimeTime project.

#if NET || !SYSTEMCLOCK

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

#if SYSTEMCLOCK
namespace KZDev.SystemClock.PrimeTime;
#else
namespace KZDev.PrimeTime;
#endif

/// <summary>
///   Shared implementation of day-time timer registration: lifecycle, callback dispatch,
///   and scheduling orchestration. Time-basis storage and delay math live in partials.
/// </summary>
internal sealed partial class ClockDayTimeTimerRegistration : IClockDayTimeTimer
{
    private static readonly TimeSpan OneDay = TimeSpan.FromDays(1);
    private static int _nextId;

#if NET10_OR_GREATER
    private readonly Lock _gate = new();
#else
    private readonly object _gate = new();
#endif

    private IPrimeClock _clock;
    private bool _captureContext;
    private IntervalTimerCallbackKind _callbackKind;
    private Delegate _callback;
    private object? _callbackState;
    private CancellationToken _cancellationToken;
    private CancellationTokenRegistration _cancelRegistration;
    private ConcurrentTriggerProcessing _concurrentTriggerProcessing;
    private SkippedTimeBehavior _skippedTimeBehavior;
    private DuplicateTimeBehavior _duplicateTimeBehavior;
    private int _id;
    private Timer? _timer;
    private TimerState _state;
    private bool _enabled = true;
    private bool _disposed;
    private int _callbacksRunning;
    private bool _cancelRequested;
    private bool _pendingRunSequential;

#if NET
    private partial bool IsLocalDayTimeSchedule { get; }

    private partial bool IsUtcDayTimeSchedule { get; }

    private partial void ApplyLocalScheduleTimeOfDay (TimeOnly value);

    private partial void ApplyUtcScheduleTimeOfDay (TimeOnly value);

#endif

    #region Constructors/Finalizers

    /// <summary>
    ///   Completes initialization shared by stack-specific constructors.
    /// </summary>
    private void FinishConstruction (
        IPrimeClock clock,
        IntervalTimerCallbackKind callbackKind,
        Delegate callback,
        object? callbackState,
        DayTimeTimerOptions? options,
        CancellationToken cancellationToken)
    {
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _callbackKind = callbackKind;
        _callback = callback ?? throw new ArgumentNullException(nameof(callback));
        _callbackState = callbackState;

        DayTimeTimerOptions opts = options ?? new DayTimeTimerOptions();
        _concurrentTriggerProcessing = opts.ConcurrentTriggerProcessing;
        _skippedTimeBehavior = opts.SkippedTimeBehavior;
        _duplicateTimeBehavior = opts.DuplicateTimeBehavior;
        _captureContext = opts.CallbackExecutionContext != TimerCallbackExecutionContext.Unsafe;
        _cancellationToken = cancellationToken;

        _id = Interlocked.Increment(ref _nextId);
        CaptureRegisteredTimeForDayTimer();

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
    public int Id { [DebuggerStepThrough] get => _id; }

    /// <inheritdoc />
    public DateTimeOffset RegisteredTime { [DebuggerStepThrough] get => GetRegisteredTimeOffset(); }

    /// <inheritdoc />
    public bool IsTimeOfDay => true;

    /// <inheritdoc />
    public bool IsResetAfterCallback => false;

    /// <inheritdoc />
    public bool IsLocalTimeRepresentation { [DebuggerStepThrough] get => GetIsLocalTimeRepresentation(); }

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
    public ConcurrentTriggerProcessing ConcurrentTriggerProcessing { [DebuggerStepThrough] get => _concurrentTriggerProcessing; }

    /// <inheritdoc />
    public SkippedTimeBehavior SkippedTimeBehavior { [DebuggerStepThrough] get => _skippedTimeBehavior; }

    /// <inheritdoc />
    public DuplicateTimeBehavior DuplicateTimeBehavior { [DebuggerStepThrough] get => _duplicateTimeBehavior; }

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
                return GetDayTimeElapsedMillisecondsWhileLocked();
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
                return GetDayTimeTimeUntilNextMillisecondsWhileLocked();
            }
        }
    }

    private partial void CaptureRegisteredTimeForDayTimer ();

    private partial DateTimeOffset GetRegisteredTimeOffset ();

    private partial bool GetIsLocalTimeRepresentation ();

    private partial TimeSpan GetDelayUntilNextForTimer ();

    private partial void SetNextCallbackScheduledFromDelay (TimeSpan delay);

    /// <summary>
    ///   Clears the next-fire marker and records the callback start instant/offset for elapsed queries.
    /// </summary>
    private partial void RecordDayTimeCallbackTickStarted ();

    private partial long GetDayTimeElapsedMillisecondsWhileLocked ();

    private partial long GetDayTimeTimeUntilNextMillisecondsWhileLocked ();

    private partial bool TryGetTimerMillisecondsFromDelay (TimeSpan delay, out int milliseconds);

    private partial int GetRunSequentiallyRetryMilliseconds ();

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

    private void ScheduleNext ()
    {
        lock (_gate)
        {
            if (_disposed || _cancelRequested || _state == TimerState.Cancelled || !_enabled)
                return;
            TimeSpan delay = GetDelayUntilNextForTimer();
            if (delay <= TimeSpan.Zero)
                delay = OneDay;
            if (!TryGetTimerMillisecondsFromDelay(delay, out int ms))
                return;
            SetNextCallbackScheduledFromDelay(delay);
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
            int ms = GetRunSequentiallyRetryMilliseconds();
            if (ms <= 0)
                ms = 1;
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

        RecordDayTimeCallbackTickStarted();
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

    #region Interface Implementations

#if NET
    /// <inheritdoc />
    public bool Change (LocalTimeOfDay newTimeOfDay)
    {
        if (!IsLocalDayTimeSchedule)
            return false;
        lock (_gate)
        {
            if (_disposed || _state == TimerState.Cancelled)
                return false;
            ApplyLocalScheduleTimeOfDay(newTimeOfDay.Value);
            if (!_enabled)
                return true;
            ScheduleNext();
            return true;
        }
    }

    /// <inheritdoc />
    public bool Change (UtcTimeOfDay newTimeOfDay)
    {
        if (!IsUtcDayTimeSchedule)
            return false;
        lock (_gate)
        {
            if (_disposed || _state == TimerState.Cancelled)
                return false;
            ApplyUtcScheduleTimeOfDay(newTimeOfDay.Value);
            if (!_enabled)
                return true;
            ScheduleNext();
            return true;
        }
    }

#endif

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

    #endregion Interface Implementations
}
#endif
