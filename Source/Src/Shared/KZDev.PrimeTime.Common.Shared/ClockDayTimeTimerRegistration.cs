// Copyright (c) Kevin Zehrer
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

#if NET || !SYSTEMCLOCK

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;

#if SYSTEMCLOCK
namespace KZDev.SystemClock.PrimeTime;
#else
namespace KZDev.PrimeTime;
#endif

//################################################################################
/// <summary>
///   Shared implementation of day-time timer registration: lifecycle, callback dispatch,
///   and scheduling orchestration. Time-basis storage and delay math live in partials.
/// </summary>
/// <remarks>
///   <para>
///     For local calendar day-time registrations, stack-specific partials compute the next fire
///     using the clock's zone and the registration's <see cref="SkippedTimeBehavior"/> and
///     <see cref="DuplicateTimeBehavior"/> (from <see cref="DayTimeTimerOptions"/>), following
///     the same rules documented on those enums and summarized on
///     <see cref="DayTimeSchedulingPolicyTable"/>. UTC time-of-day registrations use fixed-offset
///     semantics; skipped/duplicate behaviors apply only to the local wall-time path.
///   </para>
/// </remarks>
internal sealed partial class ClockDayTimeTimerRegistration : IClockDayTimeTimer
{
    //----------------------------------------------------------------------------
    /// <summary>
    ///   One calendar day for delay clamping when the next fire is in the past.
    /// </summary>
    private static readonly TimeSpan OneDay = TimeSpan.FromDays(1);

    /// <summary>
    ///   Monotonic sequence source for registration identifiers.
    /// </summary>
    private static int _nextRegistrationIdentifier;

    /// <summary>
    ///   Protects mutable registration and timer fields.
    /// </summary>
#if NET9_OR_GREATER
    private readonly Lock _gate = new();
#else
    private readonly object _gate = new();
#endif

    /// <summary>
    ///   Clock used for scheduling and reading "now".
    /// </summary>
    private readonly IPrimeClock _clock;

    /// <summary>
    ///   When <c>true</c>, callbacks capture execution context.
    /// </summary>
    private bool _captureContext;

    /// <summary>
    ///   Shape of the user callback delegate.
    /// </summary>
    private IntervalTimerCallbackKind _callbackKind;

    /// <summary>
    ///   User callback delegate.
    /// </summary>
    private readonly Delegate _callback;

    /// <summary>
    ///   Optional state for context callbacks.
    /// </summary>
    private object? _callbackState;

    /// <summary>
    ///   External cancellation token for this registration.
    /// </summary>
    private CancellationToken _cancellationToken;

    /// <summary>
    ///   Registration for <see cref="_cancellationToken"/> cancellation.
    /// </summary>
    private CancellationTokenRegistration _cancelRegistration;

    /// <summary>
    ///   Concurrent invocation policy from options.
    /// </summary>
    private ConcurrentTriggerProcessing _concurrentTriggerProcessing;

    /// <summary>
    ///   Skipped-time behavior from options.
    /// </summary>
    private SkippedTimeBehavior _skippedTimeBehavior;

    /// <summary>
    ///   Duplicate-trigger behavior from options.
    /// </summary>
    private DuplicateTimeBehavior _duplicateTimeBehavior;

    /// <summary>
    ///   Unique registration identifier.
    /// </summary>
    private int _registrationIdentifier;

    /// <summary>
    ///   Underlying BCL timer between day-time fires.
    /// </summary>
    private Timer? _timer;

    /// <summary>
    ///   Current logical timer state.
    /// </summary>
    private TimerState _state;

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

    /// <summary>
    ///   When <c>true</c>, a sequential run is pending after the current callback.
    /// </summary>
    private bool _pendingRunSequential;
    //----------------------------------------------------------------------------

#if NET
    /// <summary>
    ///   Whether this registration uses local time-of-day (partial).
    /// </summary>
    private partial bool IsLocalDayTimeSchedule { get; }

    /// <summary>
    ///   Whether this registration uses UTC time-of-day (partial).
    /// </summary>
    private partial bool IsUtcDayTimeSchedule { get; }

    /// <summary>
    ///   Applies a new local time-of-day schedule (partial).
    /// </summary>
    /// <param name="newTimeOfDay">New local time of day.</param>
    private partial void ApplyLocalScheduleTimeOfDay (TimeOnly newTimeOfDay);

    /// <summary>
    ///   Applies a new UTC time-of-day schedule (partial).
    /// </summary>
    /// <param name="newTimeOfDay">New UTC time of day.</param>
    private partial void ApplyUtcScheduleTimeOfDay (TimeOnly newTimeOfDay);

#endif

    #region Constructors/Finalizers

    //----------------------------------------------------------------------------
    /// <summary>
    ///   Completes initialization shared by stack-specific constructors.
    /// </summary>
    /// <remarks>
    ///   <para>
    ///     Stack-specific constructors assign and validate <see cref="_clock"/> and <see cref="_callback"/> before calling
    ///     this method; this method does not take a clock or callback parameter and does not perform those assignments.
    ///   </para>
    /// </remarks>
    /// <param name="callbackKind">The kind of callback delegate to invoke.</param>
    /// <param name="callbackState">
    ///   Optional state passed to the callback via <see cref="ClockTimerCallbackContext"/>.
    /// </param>
    /// <param name="options">
    ///   Day-time timer options, or <c>null</c> to use default option values.
    /// </param>
    /// <param name="cancellationToken">Token that cancels the registration.</param>
    private void FinishConstruction (
        IntervalTimerCallbackKind callbackKind,
        object? callbackState,
        DayTimeTimerOptions? options,
        CancellationToken cancellationToken)
    {
        _callbackKind = callbackKind;
        _callbackState = callbackState;

        DayTimeTimerOptions resolvedOptions = options ?? new DayTimeTimerOptions();
        _concurrentTriggerProcessing = resolvedOptions.ConcurrentTriggerProcessing;
        _skippedTimeBehavior = resolvedOptions.SkippedTimeBehavior;
        _duplicateTimeBehavior = resolvedOptions.DuplicateTimeBehavior;
        _captureContext = resolvedOptions.CallbackExecutionContext != TimerCallbackExecutionContext.Unsafe;
        _cancellationToken = cancellationToken;

        _registrationIdentifier = Interlocked.Increment(ref _nextRegistrationIdentifier);
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
    //----------------------------------------------------------------------------

    #endregion Constructors/Finalizers

    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public int Id { [DebuggerStepThrough] get => _registrationIdentifier; }
    //----------------------------------------------------------------------------

    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public DateTimeOffset RegisteredTime { [DebuggerStepThrough] get => GetRegisteredTimeOffset(); }
    //----------------------------------------------------------------------------

    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public bool IsTimeOfDay => true;
    //----------------------------------------------------------------------------

    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public bool IsLocalTimeRepresentation { [DebuggerStepThrough] get => GetIsLocalTimeRepresentation(); }
    //----------------------------------------------------------------------------

    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public bool IsRepeating => true;
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
            // Capture local state value
            TimerState state = _state;
            return state != TimerState.Cancelled &&
                   state != TimerState.Disposed;
        }
    }
    //----------------------------------------------------------------------------

    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public TimerState State { [DebuggerStepThrough] get => _state; }
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
    public ConcurrentTriggerProcessing ConcurrentTriggerProcessing { [DebuggerStepThrough] get => _concurrentTriggerProcessing; }
    //----------------------------------------------------------------------------

    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public SkippedTimeBehavior SkippedTimeBehavior { [DebuggerStepThrough] get => _skippedTimeBehavior; }
    //----------------------------------------------------------------------------

    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public DuplicateTimeBehavior DuplicateTimeBehavior { [DebuggerStepThrough] get => _duplicateTimeBehavior; }
    //----------------------------------------------------------------------------

    //----------------------------------------------------------------------------
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
    //----------------------------------------------------------------------------

    //----------------------------------------------------------------------------
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
    //----------------------------------------------------------------------------
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
    //----------------------------------------------------------------------------
    /// <summary>
    ///   Captures <see cref="RegisteredTime"/> for this day-time registration (partial).
    /// </summary>
    private partial void CaptureRegisteredTimeForDayTimer ();
    //----------------------------------------------------------------------------
    /// <summary>
    ///   Returns registered time in the registration basis (partial).
    /// </summary>
    private partial DateTimeOffset GetRegisteredTimeOffset ();
    //----------------------------------------------------------------------------
    /// <summary>
    ///   Whether properties use local representation (partial).
    /// </summary>
    private partial bool GetIsLocalTimeRepresentation ();
    //----------------------------------------------------------------------------
    /// <summary>
    ///   Computes delay until the next scheduled fire (partial).
    /// </summary>
    private partial TimeSpan GetDelayUntilNextForTimer ();
    //----------------------------------------------------------------------------
    /// <summary>
    ///   Records the next fire from a wall-clock delay (partial).
    /// </summary>
    /// <param name="delay">Delay until the next tick.</param>
    private partial void SetNextCallbackScheduledFromDelay (TimeSpan delay);
    //----------------------------------------------------------------------------
    /// <summary>
    ///   Clears the next-fire marker and records the callback start for elapsed-time queries (partial).
    /// </summary>
    private partial void RecordDayTimeCallbackTickStarted ();
    //----------------------------------------------------------------------------
    /// <summary>
    ///   Elapsed milliseconds since last callback while <see cref="_gate"/> is held (partial).
    /// </summary>
    private partial long GetDayTimeElapsedMillisecondsWhileLocked ();
    //----------------------------------------------------------------------------
    /// <summary>
    ///   Milliseconds until next callback while <see cref="_gate"/> is held (partial).
    /// </summary>
    private partial long GetDayTimeTimeUntilNextMillisecondsWhileLocked ();
    //----------------------------------------------------------------------------
    /// <summary>
    ///   Converts <paramref name="delay"/> to BCL timer milliseconds when valid (partial).
    /// </summary>
    private partial bool TryGetTimerMillisecondsFromDelay (TimeSpan delay, out int milliseconds);
    //----------------------------------------------------------------------------
    /// <summary>
    ///   Retry delay when running sequential day-time triggers (partial).
    /// </summary>
    private partial int GetRunSequentiallyRetryMilliseconds ();
    //----------------------------------------------------------------------------
    /// <summary>
    ///   Cancels the registration and disarms the BCL timer.
    /// </summary>
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
    //----------------------------------------------------------------------------

    //----------------------------------------------------------------------------
    /// <summary>
    ///   Computes and arms the next day-time callback.
    /// </summary>
    private void ScheduleNext ()
    {
        lock (_gate)
        {
            if (_disposed || _cancelRequested || _state == TimerState.Cancelled || !_enabled)
                return;
            TimeSpan delayUntilNextFire = GetDelayUntilNextForTimer();
            if (delayUntilNextFire <= TimeSpan.Zero)
                delayUntilNextFire = OneDay;
            if (!TryGetTimerMillisecondsFromDelay(delayUntilNextFire, out int timerMilliseconds))
                return;
            SetNextCallbackScheduledFromDelay(delayUntilNextFire);
            if (_timer is null)
                _timer = new Timer(OnTimerTick, null, timerMilliseconds, Timeout.Infinite);
            else
                _timer.Change(timerMilliseconds, Timeout.Infinite);
        }
    }
    //----------------------------------------------------------------------------

    //----------------------------------------------------------------------------
    /// <summary>
    ///   Re-arms the timer after a short delay for sequential trigger retry.
    /// </summary>
    private void ScheduleNextAfterShortDelay ()
    {
        lock (_gate)
        {
            if (_disposed || _cancelRequested || _state == TimerState.Cancelled || !_enabled)
                return;
            int retryMilliseconds = GetRunSequentiallyRetryMilliseconds();
            if (retryMilliseconds <= 0)
                retryMilliseconds = 1;
            if (_timer is null)
                _timer = new Timer(OnTimerTick, null, retryMilliseconds, Timeout.Infinite);
            else
                _timer.Change(retryMilliseconds, Timeout.Infinite);
        }
    }
    //----------------------------------------------------------------------------

    //----------------------------------------------------------------------------
    /// <summary>
    ///   BCL timer callback for the next day-time fire.
    /// </summary>
    /// <param name="unusedTimerState">Unused state object passed by <see cref="Timer"/>.</param>
    private void OnTimerTick (object? unusedTimerState)
    {
        lock (_gate)
        {
            if (_disposed || _cancelRequested || _state == TimerState.Cancelled || !_enabled)
                return;
            _timer!.Change(Timeout.Infinite, Timeout.Infinite);

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
            _state = TimerState.RepeatProcessingCallback;
            _callbacksRunning++;
        }

        bool callbackIsAsynchronous = _callbackKind == IntervalTimerCallbackKind.SimpleAsync ||
            _callbackKind == IntervalTimerCallbackKind.ContextAsync;
        try
        {
            RunCallback();
        }
        finally
        {
            if (!callbackIsAsynchronous)
            {
                lock (_gate)
                {
                    _callbacksRunning--;
                }
            }
        }

        if (callbackIsAsynchronous)
        {
            return;
        }

        bool shouldRetryAfterSequentialCallback;
        lock (_gate)
        {
            shouldRetryAfterSequentialCallback = _pendingRunSequential;
            _pendingRunSequential = false;
        }
        if (shouldRetryAfterSequentialCallback)
            ScheduleNextAfterShortDelay();
        else
            ScheduleNext();
    }
    //----------------------------------------------------------------------------

    //----------------------------------------------------------------------------
    /// <summary>
    ///   Invokes the user callback synchronously or starts async completion handling.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    ///   <see cref="_callbackKind"/> is not supported.
    /// </exception>
    private void RunCallback ()
    {
        void InvokeSync (Action run)
        {
            if (_captureContext && !ExecutionContext.IsFlowSuppressed())
            {
                ExecutionContext? executionContext = ExecutionContext.Capture();
                if (executionContext is not null)
                {
                    ExecutionContext.Run(executionContext, ignoredState => run(), null);
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
    //----------------------------------------------------------------------------

    //----------------------------------------------------------------------------
    /// <summary>
    ///   Runs an async callback and continues on the thread pool when it does not complete synchronously.
    /// </summary>
    /// <param name="run">Async callback invocation.</param>
    private void RunAsyncAndScheduleAfter (Func<ValueTask> run)
    {
        ValueTask callbackValueTask;
        try
        {
            callbackValueTask = run();
        }
        catch
        {
            ScheduleNextFromAsync();
            return;
        }

        if (callbackValueTask.IsCompletedSuccessfully)
        {
            ScheduleNextFromAsync();
            return;
        }
        callbackValueTask.AsTask().ContinueWith((completedTask, registrationState) =>
            {
                if (completedTask.IsFaulted)
                {
                    _ = completedTask.Exception;
                }
                ((ClockDayTimeTimerRegistration)registrationState!).ScheduleNextFromAsync();
            },
            this,
            CancellationToken.None,
            TaskContinuationOptions.None,
            TaskScheduler.Default);
    }
    //----------------------------------------------------------------------------

    //----------------------------------------------------------------------------
    /// <summary>
    ///   Decrements in-flight count after async work and schedules the next fire.
    /// </summary>
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
    //----------------------------------------------------------------------------

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

    //----------------------------------------------------------------------------
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
    //----------------------------------------------------------------------------

    //----------------------------------------------------------------------------
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
    //----------------------------------------------------------------------------

    //----------------------------------------------------------------------------
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
            _state = TimerState.Disposed;
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

    #endregion Interface Implementations
}
//################################################################################
#endif
