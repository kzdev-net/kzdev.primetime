// Copyright (c) Kevin Zehrer
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Diagnostics;

#if SYSTEMCLOCK
namespace KZDev.SystemClock.PrimeTime;
#else
namespace KZDev.PrimeTime;
#endif

//################################################################################
/// <summary>
///   Implementation of <see cref="IClockIntervalTimer"/> used by <see cref="PrimeClock"/>.
/// </summary>
internal sealed partial class ClockIntervalTimerRegistration : IClockIntervalTimer
{
    //----------------------------------------------------------------------------
    /// <summary>
    ///   Monotonic id assigned to each registration.
    /// </summary>
    private static int _nextId;

    /// <summary>
    ///   Clock used for scheduling and reading "now".
    /// </summary>
    private readonly IPrimeClock _clock;

    /// <summary>
    ///   When <c>true</c>, timer callbacks capture execution context.
    /// </summary>
    private readonly bool _captureContext;

    /// <summary>
    ///   Shape of the user callback delegate.
    /// </summary>
    private readonly IntervalTimerCallbackKind _callbackKind;

    /// <summary>
    ///   User callback delegate.
    /// </summary>
    private readonly Delegate _callback;

    /// <summary>
    ///   Optional state for context callbacks.
    /// </summary>
    private readonly object? _callbackState;

    /// <summary>
    ///   External cancellation token for this registration.
    /// </summary>
    private readonly CancellationToken _cancellationToken;

    /// <summary>
    ///   Registration for <see cref="_cancellationToken"/> cancellation.
    /// </summary>
    private readonly CancellationTokenRegistration _cancelRegistration;

#if NET10_OR_GREATER
    /// <summary>
    ///   Protects mutable registration and timer fields.
    /// </summary>
    private readonly Lock _gate = new();
#else
    /// <summary>
    ///   Protects mutable registration and timer fields.
    /// </summary>
    private readonly object _gate = new();
#endif

    /// <summary>
    ///   Current logical <see cref="IClockTimer.State"/>.
    /// </summary>
    private TimerState _state;

    /// <summary>
    ///   Underlying BCL one-shot timer used between callbacks.
    /// </summary>
    private Timer? _timer;

    /// <summary>
    ///   When <c>false</c>, no further callbacks are scheduled.
    /// </summary>
    private bool _enabled = true;

    /// <summary>
    ///   When <c>true</c>, this registration has been disposed.
    /// </summary>
    private bool _disposed;

    /// <summary>
    ///   Number of callbacks currently executing.
    /// </summary>
    private int _callbacksRunning;

    /// <summary>
    ///   When <c>true</c>, external cancellation was requested.
    /// </summary>
    private bool _cancelRequested;
    //----------------------------------------------------------------------------

    //----------------------------------------------------------------------------
    /// <summary>
    ///   Initial delay and current per-tick delay basis (stack-specific partial).
    /// </summary>
    private partial TimeSpan InitialCallbackTimeSpan { get; set; }
    //----------------------------------------------------------------------------

    //----------------------------------------------------------------------------
    /// <summary>
    ///   Repeat interval between callbacks, or infinite for one-shot.
    /// </summary>
    private partial TimeSpan RepeatTimeSpanInterval { get; set; }
    //----------------------------------------------------------------------------

    //----------------------------------------------------------------------------
    /// <summary>
    ///   Next scheduled callback instant in UTC-offset form (partial).
    /// </summary>
    private partial DateTimeOffset? NextCallbackUtc { get; set; }
    //----------------------------------------------------------------------------

    //----------------------------------------------------------------------------
    /// <summary>
    ///   Last callback start instant in UTC-offset form (partial).
    /// </summary>
    private partial DateTimeOffset? LastCallbackUtc { get; set; }
    //----------------------------------------------------------------------------

    //----------------------------------------------------------------------------
    /// <summary>
    ///   Whether this registration repeats after the first fire.
    /// </summary>
    private partial bool IsRepeatingTimer { get; }
    //----------------------------------------------------------------------------

    #region Constructors/Finalizers

    //----------------------------------------------------------------------------
    /// <summary>
    ///   Initializes a new instance of the <see cref="ClockIntervalTimerRegistration"/> class.
    /// </summary>
    /// <param name="clock">
    ///   The BCL clock used for scheduling and time.
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
    /// <exception cref="ArgumentNullException">
    ///   <paramref name="clock"/> or <paramref name="callback"/> is <c>null</c>.
    /// </exception>
    internal ClockIntervalTimerRegistration (IPrimeClock clock,
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
        InitialCallbackTimeSpan = initialCallbackTime;
        RepeatTimeSpanInterval = repeatInterval;
        IntervalTimerOptions opts = options ?? new IntervalTimerOptions();
        IsResetAfterCallback = opts.ResetIntervalAfterCallback;
        IsLocalTimeRepresentation = opts.LocalTimeRepresentation;
        _captureContext = opts.CallbackExecutionContext != TimerCallbackExecutionContext.Unsafe;
        _cancellationToken = cancellationToken;
        Id = Interlocked.Increment(ref _nextId);
        CaptureRegisteredTime();

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
    //----------------------------------------------------------------------------

    #endregion Constructors/Finalizers

    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public int Id { [DebuggerStepThrough] get; }
    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public DateTimeOffset RegisteredTime { [DebuggerStepThrough] get => GetRegisteredTime(); }
    //----------------------------------------------------------------------------

    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public bool IsTimeOfDay => false;
    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public bool IsResetAfterCallback { [DebuggerStepThrough] get; }
    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public bool IsLocalTimeRepresentation { [DebuggerStepThrough] get; }
    //----------------------------------------------------------------------------

    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public bool IsRepeating { [DebuggerStepThrough] get => IsRepeatingTimer; }
    //----------------------------------------------------------------------------

    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public bool IsCancelled => _state == TimerState.Cancelled;
    //----------------------------------------------------------------------------

    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public bool IsActive
    {
        get
        {
            lock (_gate)
            {
                // Capture local state value
                TimerState state = _state;
                return state != TimerState.Cancelled &&
                       state != TimerState.Completed &&
                       state != TimerState.Disposed;
            }
        }
    }
    //----------------------------------------------------------------------------

    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public TimerState State { [DebuggerStepThrough] get => _state; [DebuggerStepThrough] private set => _state = value; }
    //----------------------------------------------------------------------------

    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public bool CallbacksProcessing
    {
        get
        {
            lock (_gate)
            {
                TimerState state = _state;
                return state != TimerState.Cancelled &&
                       state != TimerState.Disposed &&
                       _callbacksRunning > 0;
            }
        }
    }
    //----------------------------------------------------------------------------

    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public bool Enabled
    {
        get
        {
            lock (_gate)
            {
                return _enabled && _state != TimerState.Cancelled && _state != TimerState.Disposed;
            }
        }
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
    //----------------------------------------------------------------------------

    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public long ElapsedTime
    {
        [DebuggerStepThrough]
        get
        {
            return GetElapsedTime();
        }
    }
    //----------------------------------------------------------------------------

    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public long TimeUntilNextCallback
    {
        get
        {
            lock (_gate)
            {
                return GetTimeUntilNextCallbackMillisecondsWhileLocked();
            }
        }
    }
    //----------------------------------------------------------------------------

    //----------------------------------------------------------------------------
    /// <summary>
    ///   Captures <see cref="RegisteredTime"/> from the clock per local/UTC option (partial).
    /// </summary>
    private partial void CaptureRegisteredTime ();
    //----------------------------------------------------------------------------

    //----------------------------------------------------------------------------
    /// <summary>
    ///   Returns <see cref="RegisteredTime"/> in the registration time basis (partial).
    /// </summary>
    private partial DateTimeOffset GetRegisteredTime ();
    //----------------------------------------------------------------------------

    //----------------------------------------------------------------------------
    /// <summary>
    ///   Computes elapsed milliseconds since last callback per contract (partial).
    /// </summary>
    private partial long GetElapsedTime ();
    //----------------------------------------------------------------------------

    //----------------------------------------------------------------------------
    /// <summary>
    ///   When <c>true</c>, converts <paramref name="delay"/> to a due-time in milliseconds for
    ///   <see cref="Timer"/>; when <c>false</c>, the registration should not arm the timer.
    /// </summary>
    private partial bool TryGetTimerMillisecondsForSchedule (TimeSpan delay, out int milliseconds);
    //----------------------------------------------------------------------------

    //----------------------------------------------------------------------------
    /// <summary>
    ///   Records the logical next callback instant/offset as <c>now + delay</c> for the active time basis.
    /// </summary>
    private partial void SetNextCallbackScheduledForDelay (TimeSpan delay);
    //----------------------------------------------------------------------------

    //----------------------------------------------------------------------------
    /// <summary>
    ///   Marks the start of a timer callback: records "last callback" and clears the next-callback marker.
    /// </summary>
    private partial void RecordIntervalCallbackStarted ();
    //----------------------------------------------------------------------------

    //----------------------------------------------------------------------------
    /// <summary>
    ///   Computes <see cref="TimeUntilNextCallback"/> while <see cref="_gate"/> is held.
    /// </summary>
    private partial long GetTimeUntilNextCallbackMillisecondsWhileLocked ();
    //----------------------------------------------------------------------------

    //----------------------------------------------------------------------------
    /// <summary>
    ///   Cancels the registration and disarms the BCL timer.
    /// </summary>
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
    //----------------------------------------------------------------------------

    //----------------------------------------------------------------------------
    /// <summary>
    ///   Schedules or reschedules the next callback after <paramref name="delay"/>.
    /// </summary>
    /// <param name="delay">Delay until the next tick.</param>
    private void ScheduleNext (TimeSpan delay)
    {
        if (!TryGetTimerMillisecondsForSchedule(delay, out int ms))
            return;
        SetNextCallbackScheduledForDelay(delay);
        if (_timer is null)
            _timer = new Timer(OnTimerTick, null, ms, Timeout.Infinite);
        else
            _timer.Change(ms, Timeout.Infinite);
    }
    //----------------------------------------------------------------------------

    //----------------------------------------------------------------------------
    /// <summary>
    ///   BCL timer callback: runs on a thread-pool thread and invokes the user callback.
    /// </summary>
    /// <param name="_">Unused state object.</param>
    private void OnTimerTick (object? _)
    {
        lock (_gate)
        {
            if (_disposed || _cancelRequested || State == TimerState.Cancelled || !_enabled)
                return;
            _timer!.Change(Timeout.Infinite, Timeout.Infinite);
        }

        RecordIntervalCallbackStarted();
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
    //----------------------------------------------------------------------------

    //----------------------------------------------------------------------------
    /// <summary>
    ///   Dispatches the user callback synchronously or starts async completion handling.
    /// </summary>
    /// <param name="stateDuringCallback">Timer state set for the duration of the callback.</param>
    /// <param name="resetAfter">Whether repeat interval resets after completion.</param>
    /// <param name="isRepeating">Whether this is a repeating registration.</param>
    /// <exception cref="InvalidOperationException">
    ///   <see cref="_callbackKind"/> is not supported.
    /// </exception>
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
    //----------------------------------------------------------------------------

    //----------------------------------------------------------------------------
    /// <summary>
    ///   Runs an async callback and continues on the thread pool when it does not complete synchronously.
    /// </summary>
    /// <param name="run">Async callback invocation.</param>
    /// <param name="resetAfter">Whether repeat interval resets after completion.</param>
    /// <param name="isRepeating">Whether this is a repeating registration.</param>
    private void RunAsyncAndScheduleAfter (Func<ValueTask> run, bool resetAfter, bool isRepeating)
    {
        ValueTask runResultTask;
        try
        {
            runResultTask = run();
        }
        catch
        {
            OnAsyncCallbackCompleted(resetAfter, isRepeating);
            return;
        }

        if (runResultTask.IsCompletedSuccessfully)
        {
            OnAsyncCallbackCompleted(resetAfter, isRepeating);
            return;
        }
        runResultTask.AsTask().ContinueWith((task, state) =>
            {
                (ClockIntervalTimerRegistration reg, bool ra, bool rep) =
                    ((ClockIntervalTimerRegistration, bool, bool))state!;
                if (task.IsFaulted)
                {
                    _ = task.Exception;
                }
                reg.OnAsyncCallbackCompleted(ra, rep);
            },
            (this, resetAfter, isRepeating),
            CancellationToken.None,
            TaskContinuationOptions.None,
            TaskScheduler.Default);
    }
    //----------------------------------------------------------------------------

    //----------------------------------------------------------------------------
    /// <summary>
    ///   After a synchronous callback, completes one-shot timers or schedules the next repeat.
    /// </summary>
    /// <param name="resetAfter">Unused; reserved for symmetry with async path.</param>
    /// <param name="isRepeating">Whether this registration repeats.</param>
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
            TimeSpan next = RepeatTimeSpanInterval;
            ScheduleNext(next);
        }
    }
    //----------------------------------------------------------------------------

    //----------------------------------------------------------------------------
    /// <summary>
    ///   After an asynchronous callback completes, completes one-shot timers or schedules the next repeat.
    /// </summary>
    /// <param name="resetAfter">Unused; reserved for symmetry with sync path.</param>
    /// <param name="isRepeating">Whether this registration repeats.</param>
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
            ScheduleNext(RepeatTimeSpanInterval);
        }
    }
    //----------------------------------------------------------------------------

    #region Interface Implementations

    #region IClockIntervalTimer Implementation

    //----------------------------------------------------------------------------
    /// <summary>
    ///   Applies a new due time and repeat interval to this registration.
    /// </summary>
    /// <param name="nextInterval">Time until the next callback.</param>
    /// <param name="repeatInterval">
    ///   Interval for subsequent callbacks, or <see cref="Timeout.InfiniteTimeSpan"/> for one-shot.
    /// </param>
    /// <returns>
    ///   <c>true</c> if the change was stored or applied; <c>false</c> if invalid or cancelled.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    ///   This registration is one-shot and <paramref name="repeatInterval"/> is a finite positive interval.
    /// </exception>
    private bool ChangeNextAndRepeat (TimeSpan nextInterval, TimeSpan repeatInterval)
    {
        lock (_gate)
        {
            if (_disposed || State == TimerState.Cancelled)
                return false;
            if (!IsRepeating && repeatInterval != Timeout.InfiniteTimeSpan && repeatInterval > TimeSpan.Zero)
                throw new InvalidOperationException("Cannot change a non-repeating timer to a repeating timer.");
            InitialCallbackTimeSpan = nextInterval;
            RepeatTimeSpanInterval = repeatInterval;
            if (State == TimerState.Completed)
                State = TimerState.Active;
            if (!_enabled)
                return true;
            ScheduleNext(nextInterval);
            return true;
        }
    }
    //----------------------------------------------------------------------------

    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public bool Change (TimeSpan interval) =>
        ChangeNextAndRepeat(interval, IsRepeating ? interval : Timeout.InfiniteTimeSpan);
    //----------------------------------------------------------------------------

    #region ITimer Implementation

    //----------------------------------------------------------------------------
    /// <inheritdoc />
    bool ITimer.Change (TimeSpan dueTime, TimeSpan period) =>
        ChangeNextAndRepeat(dueTime, period);
    //----------------------------------------------------------------------------

    #endregion ITimer Implementation


    //----------------------------------------------------------------------------
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
    //----------------------------------------------------------------------------

    //----------------------------------------------------------------------------
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
    //----------------------------------------------------------------------------

    //----------------------------------------------------------------------------
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
            ScheduleNext(InitialCallbackTimeSpan);
            return true;
        }
    }
    //----------------------------------------------------------------------------

    //----------------------------------------------------------------------------
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
    //----------------------------------------------------------------------------

    #region IAsyncDisposable Implementation

    //----------------------------------------------------------------------------
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
    //----------------------------------------------------------------------------

    #endregion IAsyncDisposable Implementation

    #endregion IClockIntervalTimer Implementation

    #endregion Interface Implementations
}
//################################################################################
