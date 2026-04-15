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
    /// <summary>
    ///   Initial delay and current per-tick delay basis (stack-specific partial).
    /// </summary>
    private partial TimeSpan InitialCallbackTimeSpan { get; set; }
    //----------------------------------------------------------------------------
    /// <summary>
    ///   Repeat interval between callbacks, or infinite for one-shot.
    /// </summary>
    private partial TimeSpan RepeatTimeSpanInterval { get; set; }
    //----------------------------------------------------------------------------
    /// <summary>
    ///   Next scheduled callback instant in UTC-offset form (partial).
    /// </summary>
    private partial DateTimeOffset? NextCallbackUtc { get; set; }
    //----------------------------------------------------------------------------
    /// <summary>
    ///   Last callback start instant in UTC-offset form (partial).
    /// </summary>
    private partial DateTimeOffset? LastCallbackUtc { get; set; }
    //----------------------------------------------------------------------------
    /// <summary>
    ///   Whether this registration repeats after the first fire.
    /// </summary>
    private partial bool IsRepeatingTimer { get; }
    //----------------------------------------------------------------------------

    //----------------------------------------------------------------------------
    /// <summary>
    ///   Captures <see cref="RegisteredTime"/> from the clock per local/UTC option (partial).
    /// </summary>
    private partial void CaptureRegisteredTime ();
    //----------------------------------------------------------------------------
    /// <summary>
    ///   Returns <see cref="RegisteredTime"/> in the registration time basis (partial).
    /// </summary>
    private partial DateTimeOffset GetRegisteredTime ();
    //----------------------------------------------------------------------------
    /// <summary>
    ///   Computes elapsed milliseconds since last callback per contract (partial).
    /// </summary>
    private partial long GetElapsedTime ();
    //----------------------------------------------------------------------------
    /// <summary>
    ///   Records the logical next callback instant/offset as <c>now + delay</c> for the active time basis.
    /// </summary>
    private partial void SetNextCallbackScheduledForDelay (TimeSpan delay);
    //----------------------------------------------------------------------------
    /// <summary>
    ///   Marks the start of a timer callback: records &quot;last callback&quot; and, when
    ///   <paramref name="resetIntervalBeforeCallback"/> is <c>false</c>, clears the next-callback marker.
    /// </summary>
    /// <param name="resetIntervalBeforeCallback">
    ///   When <c>true</c>, the next callback instant was already scheduled and is left unchanged.
    /// </param>
    private partial void RecordIntervalCallbackStarted (bool resetIntervalBeforeCallback);
    //----------------------------------------------------------------------------
    /// <summary>
    ///   Computes <see cref="TimeUntilNextCallback"/> while <see cref="_gate"/> is held.
    /// </summary>
    private partial long GetTimeUntilNextCallbackMillisecondsWhileLocked ();
    //----------------------------------------------------------------------------
    /// <summary>
    ///   When <c>true</c>, converts <paramref name="delay"/> to a due-time in milliseconds for
    ///   <see cref="Timer"/>; when <c>false</c>, the registration should not arm the timer.
    /// </summary>
    /// <param name="delay">
    ///   Desired delay until the next tick.
    /// </param>
    /// <param name="milliseconds">
    ///   When this method returns <c>true</c>, the non-negative millisecond due time; otherwise <c>0</c>.
    /// </param>
    /// <returns>
    ///   <c>true</c> when <paramref name="delay"/> is a finite, non-negative interval; otherwise <c>false</c>.
    /// </returns>
    private bool TryGetTimerMillisecondsForSchedule (TimeSpan delay, out int milliseconds)
    {
        if (delay < TimeSpan.Zero || delay == Timeout.InfiniteTimeSpan)
        {
            milliseconds = 0;
            return false;
        }
        long msLong = (long)Math.Min(delay.TotalMilliseconds, int.MaxValue);
        if (msLong < 0)
            msLong = 0;
        milliseconds = (int)msLong;
        return true;
    }
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
            _timer?.Change(Timeout.Infinite, Timeout.Infinite);
            _cancelRequested = true;
            State = TimerState.Cancelled;
            _enabled = false;
        }
    }
    //----------------------------------------------------------------------------
    /// <summary>
    ///   Schedules or reschedules the next callback after <paramref name="delay"/>.
    /// </summary>
    /// <param name="delay">Delay until the next tick.</param>
    private void ScheduleNext (TimeSpan delay)
    {
        if (!TryGetTimerMillisecondsForSchedule(delay, out int timerMilliseconds))
            return;
        SetNextCallbackScheduledForDelay(delay);
        if (_timer is null)
            _timer = new Timer(OnTimerTick, null, timerMilliseconds, Timeout.Infinite);
        else
            _timer.Change(timerMilliseconds, Timeout.Infinite);
    }
    //----------------------------------------------------------------------------
    /// <summary>
    ///   Returns <c>true</c> when timer work must not continue because the registration is disposed,
    ///   cancellation was requested, state is <see cref="TimerState.Cancelled"/>, or callbacks are disabled.
    /// </summary>
    /// <returns>
    ///   <c>true</c> when the caller should bail out of further scheduling or callback setup; otherwise <c>false</c>.
    /// </returns>
    /// <remarks>
    ///   The caller must hold <see cref="_gate"/>.
    /// </remarks>
    private bool ShouldSkipTimerWorkWhileLocked () => _disposed || _cancelRequested || State == TimerState.Cancelled || !_enabled;
    //----------------------------------------------------------------------------
    /// <summary>
    ///   BCL timer callback: runs on a thread-pool thread and invokes the user callback.
    /// </summary>
    /// <param name="_">Unused state object.</param>
    private void OnTimerTick (object? _)
    {
        bool isRepeating = IsRepeating;
        bool resetBefore = IsResetBeforeCallback;
        lock (_gate)
        {
            if (ShouldSkipTimerWorkWhileLocked())
            {
                _timer!.Change(Timeout.Infinite, Timeout.Infinite);
                return;
            }
            RecordIntervalCallbackStarted(resetBefore);

            if (isRepeating && resetBefore)
            {
                ScheduleNext(RepeatTimeSpanInterval);
            }
            else
            {
                _timer!.Change(Timeout.Infinite, Timeout.Infinite);
            }

            TimerState stateDuringCallback = isRepeating && resetBefore
                ? TimerState.RepeatProcessingCallback
                : TimerState.ProcessingCallback;

            State = stateDuringCallback;
            _callbacksRunning++;
        }

        try
        {
            RunCallback(resetBefore, isRepeating);
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
    /// <summary>
    ///   Dispatches the user callback synchronously or starts async completion handling.
    /// </summary>
    /// <param name="resetBefore">
    ///   When <c>true</c>, the next tick was scheduled at the start of this tick; do not reschedule on completion.
    /// </param>
    /// <param name="isRepeating">Whether this is a repeating registration.</param>
    /// <exception cref="InvalidOperationException">
    ///   <see cref="_callbackKind"/> is not supported.
    /// </exception>
    private void RunCallback (bool resetBefore, bool isRepeating)
    {
        switch (_callbackKind)
        {
            case IntervalTimerCallbackKind.SimpleAction:
                InvokeSync((Action)_callback);
                break;

            case IntervalTimerCallbackKind.ContextAction:
                InvokeCallbackSync((Action<ClockTimerCallbackContext>)_callback, new ClockTimerCallbackContext(this, _callbackState));
                break;

            case IntervalTimerCallbackKind.ContextActionWithToken:
                InvokeCallbackCancelSync((Action<ClockTimerCallbackContext, CancellationToken>)_callback, new ClockTimerCallbackContext(this, _callbackState));
                break;

            case IntervalTimerCallbackKind.SimpleAsync:
                RunAsyncAndScheduleAfter((Func<CancellationToken, ValueTask>)_callback, resetBefore, isRepeating);
                return;

            case IntervalTimerCallbackKind.ContextAsync:
                RunAsyncAndScheduleAfter((Func<ClockTimerCallbackContext, CancellationToken, ValueTask>)_callback, resetBefore, isRepeating);
                return;

            default:
                throw new InvalidOperationException($"Unsupported callback kind: {_callbackKind}");
        }

        OnCallbackCompleted(resetBefore, isRepeating);

        // Local helper method to invoke a simple synchronous callback with optional execution context flow
        void InvokeSync (Action run)
        {
            if (!ExecutionContext.IsFlowSuppressed())
            {
                if (_captureContext)
                {
                    ExecutionContext? executionContext = ExecutionContext.Capture();
                    if (executionContext is not null)
                    {
                        ExecutionContext.Run(executionContext, static action => ((Action)action!)(), run);
                        return;
                    }
                }
                else
                {
                    using (ExecutionContext.SuppressFlow())
                    {
                        run();
                    }
                    return;
                }
            }
            run();
        }

        // Local helper method to invoke a context-aware synchronous callback with optional execution context flow
        void InvokeCallbackSync (Action<ClockTimerCallbackContext> run, ClockTimerCallbackContext callbackContext)
        {
            if (!ExecutionContext.IsFlowSuppressed())
            {
                if (_captureContext)
                {
                    ExecutionContext? executionContext = ExecutionContext.Capture();
                    if (executionContext is not null)
                    {
                        // We accept the closure allocation here to avoid a tuple allocation from passing multiple parameters via state
                        ExecutionContext.Run(executionContext, _ => run(callbackContext), null);
                        return;
                    }
                }
                else
                {
                    using (ExecutionContext.SuppressFlow())
                    {
                        run(callbackContext);
                    }
                    return;
                }
            }
            run(callbackContext);
        }

        // Local helper method to invoke a context-aware synchronous callback with cancellation token and optional execution context flow
        void InvokeCallbackCancelSync (Action<ClockTimerCallbackContext, CancellationToken> run, ClockTimerCallbackContext callbackContext)
        {
            if (!ExecutionContext.IsFlowSuppressed())
            {
                if (_captureContext)
                {
                    ExecutionContext? executionContext = ExecutionContext.Capture();
                    if (executionContext is not null)
                    {
                        // We accept the closure allocation here to avoid a tuple allocation from passing multiple parameters via state
                        ExecutionContext.Run(executionContext, _ => run(callbackContext, _cancellationToken), null);
                        return;
                    }
                }
                else
                {
                    using (ExecutionContext.SuppressFlow())
                    {
                        run(callbackContext, _cancellationToken);
                    }
                    return;
                }
            }
            run(callbackContext, _cancellationToken);
        }
    }
    //----------------------------------------------------------------------------
    /// <summary>
    ///   Runs an async callback and continues on the thread pool when it does not complete synchronously.
    /// </summary>
    /// <param name="run">Async callback invocation.</param>
    /// <param name="resetBefore">
    ///   When <c>true</c>, the next tick was scheduled at the start of this tick; do not reschedule on completion.
    /// </param>
    /// <param name="isRepeating">Whether this is a repeating registration.</param>
    private void RunAsyncAndScheduleAfter (Func<CancellationToken, ValueTask> run, bool resetBefore, bool isRepeating)
    {
        ValueTask runResultTask;
        try
        {
            runResultTask = run(_cancellationToken);
        }
        catch
        {
            OnCallbackCompleted(resetBefore, isRepeating);
            return;
        }

        if (runResultTask.IsCompletedSuccessfully)
        {
            OnCallbackCompleted(resetBefore, isRepeating);
            return;
        }
        runResultTask.AsTask().ContinueWith(static (task, state) =>
            {
                (ClockIntervalTimerRegistration @this, bool resetBefore, bool isRepeating) =
                    (Tuple<ClockIntervalTimerRegistration, bool, bool>)state!;
                if (task.IsFaulted)
                {
                    _ = task.Exception;
                }
                @this.OnCallbackCompleted(resetBefore, isRepeating);
            },
            Tuple.Create(this, resetBefore, isRepeating),
            CancellationToken.None,
            TaskContinuationOptions.None,
            TaskScheduler.Default);
    }
    //----------------------------------------------------------------------------
    /// <summary>
    ///  Runs an async callback with context and continues on the thread pool when it does not complete synchronously.
    /// </summary>
    /// <param name="run">Async callback invocation with context.</param>
    /// <param name="resetBefore">When <c>true</c>, the next tick was scheduled at the start of this tick; do not reschedule on completion.</param>
    /// <param name="isRepeating">Whether this is a repeating registration.</param>
    private void RunAsyncAndScheduleAfter (Func<ClockTimerCallbackContext, CancellationToken, ValueTask> run, bool resetBefore, bool isRepeating)
    {
        ValueTask runResultTask;
        try
        {
            runResultTask = run(new ClockTimerCallbackContext(this, _callbackState), _cancellationToken);
        }
        catch
        {
            OnCallbackCompleted(resetBefore, isRepeating);
            return;
        }

        if (runResultTask.IsCompletedSuccessfully)
        {
            OnCallbackCompleted(resetBefore, isRepeating);
            return;
        }
        runResultTask.AsTask().ContinueWith(static (task, state) =>
            {
                (ClockIntervalTimerRegistration @this, bool resetBefore, bool isRepeating) =
                    (Tuple<ClockIntervalTimerRegistration, bool, bool>)state!;
                if (task.IsFaulted)
                {
                    _ = task.Exception;
                }
                @this.OnCallbackCompleted(resetBefore, isRepeating);
            },
            Tuple.Create(this, resetBefore, isRepeating),
            CancellationToken.None,
            TaskContinuationOptions.None,
            TaskScheduler.Default);
    }
    //----------------------------------------------------------------------------
    /// <summary>
    ///   After a synchronous callback, completes one-shot timers or schedules the next repeat.
    /// </summary>
    /// <param name="resetBefore">
    ///   When <c>true</c>, the next tick was already scheduled when this tick started.
    /// </param>
    /// <param name="isRepeating">Whether this registration repeats.</param>
    private void OnCallbackCompleted (bool resetBefore, bool isRepeating)
    {
        lock (_gate)
        {
            if (ShouldSkipTimerWorkWhileLocked())
                return;
            if (!isRepeating)
            {
                State = TimerState.Completed;
                return;
            }
            if (resetBefore)
            {
                // Take into consideration that another callback may have started while this one was running,
                // so we don't regress to the non-callback state until all concurrent callbacks complete,
                // while noting that one of the _callbacksRunning value is this callback which is completing now.
                State = (_callbacksRunning > 1) ? TimerState.ProcessingCallback : TimerState.RepeatCycle;
            }
            else
            {
                State = TimerState.RepeatCycle;
                ScheduleNext(RepeatTimeSpanInterval);
            }
        }
    }
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
    ///   Timer options (reset-before-callback, execution context, local time). May be
    ///   <c>null</c> for defaults.
    /// </param>
    /// <param name="cancellationToken">
    ///   Token to cancel the registration.
    /// </param>
    /// <exception cref="ArgumentNullException">
    ///   <paramref name="clock"/> or <paramref name="callback"/> is <c>null</c>.
    /// </exception>
    internal ClockIntervalTimerRegistration (IPrimeClock clock, TimeSpan initialCallbackTime,
        TimeSpan repeatInterval, IntervalTimerCallbackKind callbackKind, Delegate callback,
        object? callbackState, IntervalTimerOptions? options, CancellationToken cancellationToken)
    {
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _callbackKind = callbackKind;
        _callback = callback ?? throw new ArgumentNullException(nameof(callback));
        _callbackState = callbackState;
        InitialCallbackTimeSpan = initialCallbackTime;
        RepeatTimeSpanInterval = repeatInterval;
        IntervalTimerOptions opts = options ?? new IntervalTimerOptions();
        IsResetBeforeCallback = opts.ResetIntervalBeforeCallback;
        IsLocalTimeRepresentation = opts.LocalTimeRepresentation;
        _captureContext = opts.CallbackExecutionContext != TimerCallbackExecutionContext.Unsafe;
        _cancellationToken = cancellationToken;
        Id = Interlocked.Increment(ref _nextId);
        CaptureRegisteredTime();

        if (cancellationToken.CanBeCanceled)
        {
            _cancelRegistration = cancellationToken.Register(static @this => ((ClockIntervalTimerRegistration)@this!).OnCancelRequested(), this);
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

    #region Interface Implementations

    #region IClockIntervalTimer Implementation

    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public int Id { [DebuggerStepThrough] get; }
    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public DateTimeOffset RegisteredTime { [DebuggerStepThrough] get => GetRegisteredTime(); }
    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public bool IsTimeOfDay => false;
    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public bool IsResetBeforeCallback { [DebuggerStepThrough] get; }
    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public bool IsLocalTimeRepresentation { [DebuggerStepThrough] get; }
    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public bool IsRepeating { [DebuggerStepThrough] get => IsRepeatingTimer; }
    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public bool IsCancelled => _state == TimerState.Cancelled;
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
    /// <inheritdoc />
    public TimerState State { [DebuggerStepThrough] get => _state; [DebuggerStepThrough] private set => _state = value; }
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
    /// <inheritdoc />
    public bool Stop ()
    {
        lock (_gate)
        {
            if (!_enabled || State == TimerState.Cancelled || _disposed)
                return false;
            _timer?.Change(Timeout.Infinite, Timeout.Infinite);
            _enabled = false;
            State = TimerState.Disabled;
            return true;
        }
    }
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
    /// <inheritdoc />
    public void Dispose ()
    {
        Timer? timerToDispose = null;
        lock (_gate)
        {
            if (_disposed)
                return;
            _disposed = true;
            State = TimerState.Disposed;
            _enabled = false;
            timerToDispose = _timer;
            _timer = null;
        }
        timerToDispose?.Dispose();
        _cancelRegistration.Dispose();
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
