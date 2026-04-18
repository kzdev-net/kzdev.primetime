// Copyright (c) Kevin Zehrer
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

#if NET || !SYSTEMCLOCK

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;

#if SYSTEMCLOCK
using KZDev.SystemClock.PrimeTime.Observability;

namespace KZDev.SystemClock.PrimeTime;
#else
using KZDev.PrimeTime.Observability;

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
internal sealed partial class ClockDayTimeTimerRegistration : ClockTimerRegistration, IClockDayTimeTimer
{
    //----------------------------------------------------------------------------
    /// <summary>
    ///   One calendar day for delay clamping when the next fire is in the past.
    /// </summary>
    private static readonly TimeSpan OneDayTimeSpan = TimeSpan.FromDays(1);

    /// <summary>
    ///   When <c>true</c>, a sequential run is pending after the current callback.
    /// </summary>
    private bool _pendingRunSequential;
    //----------------------------------------------------------------------------

#if NET
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

    /// <summary>
    ///   Gets whether this registration schedules using local calendar days.
    /// </summary>
    private bool IsLocalDayTimeSchedule { [DebuggerStepThrough] get => !UtcTimeOfDaySchedule; }

    /// <summary>
    ///   Gets whether this registration schedules using UTC calendar days.
    /// </summary>
    private bool IsUtcDayTimeSchedule { [DebuggerStepThrough] get => UtcTimeOfDaySchedule; }

#endif

    //----------------------------------------------------------------------------
    /// <summary>
    ///   Computes delay until the next scheduled fire (partial).
    /// </summary>
    private partial TimeSpan GetDelayUntilNextForTimer();
    //----------------------------------------------------------------------------
    /// <summary>
    ///   Records the next fire from a wall-clock delay (partial).
    /// </summary>
    /// <param name="delay">Delay until the next tick.</param>
    private partial void SetNextCallbackScheduledFromDelay(TimeSpan delay);
    //----------------------------------------------------------------------------
    /// <summary>
    ///   Clears the next-fire marker and records the callback start for elapsed-time queries (partial).
    /// </summary>
    private partial void RecordDayTimeCallbackTickStarted();
    //----------------------------------------------------------------------------
    /// <summary>
    ///   Elapsed milliseconds since last callback while <see cref="Gate"/> is held (partial).
    /// </summary>
    private partial long GetDayTimeElapsedMillisecondsWhileLocked();
    //----------------------------------------------------------------------------
    /// <summary>
    ///   Milliseconds until next callback while <see cref="Gate"/> is held (partial).
    /// </summary>
    private partial long GetDayTimeTimeUntilNextMillisecondsWhileLocked();
    //----------------------------------------------------------------------------
    /// <summary>
    ///   Converts <paramref name="delay"/> to BCL timer milliseconds when valid (partial).
    /// </summary>
    private partial bool TryGetTimerMillisecondsFromDelay(TimeSpan delay, out int milliseconds);
    //----------------------------------------------------------------------------
    /// <summary>
    ///   Retry delay when running sequential day-time triggers (partial).
    /// </summary>
    private partial int GetRunSequentiallyRetryMilliseconds();
    //----------------------------------------------------------------------------
    /// <summary>
    ///   Computes and arms the next day-time callback.
    /// </summary>
    private void ScheduleNext()
    {
        lock (Gate)
        {
            if (ShouldSkipTimerWorkWhileLocked())
                return;
            TimeSpan delayUntilNextFire = GetDelayUntilNextForTimer();
            if (delayUntilNextFire <= TimeSpan.Zero)
                delayUntilNextFire = OneDayTimeSpan;
            if (!TryGetTimerMillisecondsFromDelay(delayUntilNextFire, out int timerMilliseconds))
                return;
            SetNextCallbackScheduledFromDelay(delayUntilNextFire);
            if (Timer is null)
                Timer = new Timer(OnTimerTick, null, timerMilliseconds, Timeout.Infinite);
            else
                Timer.Change(timerMilliseconds, Timeout.Infinite);
        }
    }
    //----------------------------------------------------------------------------
    /// <summary>
    ///   Re-arms the timer after a short delay for sequential trigger retry.
    /// </summary>
    private void ScheduleNextAfterShortDelay()
    {
        lock (Gate)
        {
            if (ShouldSkipTimerWorkWhileLocked())
                return;
            int retryMilliseconds = GetRunSequentiallyRetryMilliseconds();
            if (retryMilliseconds <= 0)
                retryMilliseconds = 1;
            if (Timer is null)
                Timer = new Timer(OnTimerTick, null, retryMilliseconds, Timeout.Infinite);
            else
                Timer.Change(retryMilliseconds, Timeout.Infinite);
        }
    }
    //----------------------------------------------------------------------------
    /// <summary>
    ///   Processing method for processing the callback delegate.
    /// </summary>
    /// <param name="callbackIsAsynchronous">
    ///   Indicates whether the callback is asynchronous, which affects whether callback completion
    ///   handling is done in this method or an async continuation. This is determined by the
    ///   <see cref="ClockTimerRegistration.CallbackKind"/> property.
    /// </param>
    private void ProcessCallback(bool callbackIsAsynchronous)
    {
        bool shouldRetryAfterSequentialCallback;

        do
        {
            shouldRetryAfterSequentialCallback = false;
            lock (Gate)
            {
                if (ShouldSkipTimerWorkWhileLocked())
                {
                    Timer!.Change(Timeout.Infinite, Timeout.Infinite);
                    return;
                }
                State = TimerState.ProcessingCallback;
                CallbacksRunning++;
            }

            try
            {
                RunCallback();
            }
            catch (Exception ex)
            {
                PrimeTimeEventSource.Log.ClockTimerCallbackException("DayTime", ex);
            }
            finally
            {
                lock (Gate)
                {
                    if (!callbackIsAsynchronous)
                    {
                        CallbacksRunning--;
                        shouldRetryAfterSequentialCallback = _pendingRunSequential;
                        _pendingRunSequential = false;
                    }
                    if (!shouldRetryAfterSequentialCallback && !ShouldSkipTimerWorkWhileLocked())
                    {
                        State = TimerState.Active;
                    }
                }
            }
            if (callbackIsAsynchronous)
            {
                // Let the async completion handler take care of cleanup, etc.
                return;
            }
        } while (shouldRetryAfterSequentialCallback);
    }
    //----------------------------------------------------------------------------
    /// <summary>
    ///   BCL timer callback for the next day-time fire.
    /// </summary>
    private void OnTimerTick(object? _)
    {
        bool callbackIsAsynchronous = IsAsyncCallback;
        bool shouldRetryAfterSequentialCallback = false;

        lock (Gate)
        {
            if (ShouldSkipTimerWorkWhileLocked())
            {
                Timer!.Change(Timeout.Infinite, Timeout.Infinite);
                return;
            }
            RecordDayTimeCallbackTickStarted();
            ScheduleNext();

            if (CallbacksRunning > 0)
            {
                switch (ConcurrentTriggerProcessing)
                {
                    case ConcurrentTriggerProcessing.Skip:
                        return;

                    case ConcurrentTriggerProcessing.RunSequentially:
                        _pendingRunSequential = true;
                        return;
                }
            }

            State = TimerState.ProcessingCallback;
            CallbacksRunning++;
        }

        try
        {
            RunCallback();
        }
        catch (Exception ex)
        {
            PrimeTimeEventSource.Log.ClockTimerCallbackException("DayTime", ex);
        }
        finally
        {
            lock (Gate)
            {
                if (!callbackIsAsynchronous)
                {
                    CallbacksRunning--;
                    shouldRetryAfterSequentialCallback = _pendingRunSequential;
                    _pendingRunSequential = false;
                }
                if (!shouldRetryAfterSequentialCallback && !ShouldSkipTimerWorkWhileLocked())
                {
                    State = TimerState.Active;
                }
            }
        }
        if (callbackIsAsynchronous)
        {
            // Let the async completion handler take care of cleanup, etc.
            return;
        }

        if (shouldRetryAfterSequentialCallback)
            ProcessCallback(callbackIsAsynchronous);
    }
    //----------------------------------------------------------------------------
    /// <summary>
    ///   Invokes the user callback synchronously or starts async completion handling.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    ///   <see cref="ClockTimerRegistration.CallbackKind"/> is not supported.
    /// </exception>
    private void RunCallback()
    {
        switch (CallbackKind)
        {
            case TimerCallbackKind.SimpleAction:
                InvokeSync((Action)Callback);
                break;

            case TimerCallbackKind.ContextAction:
                InvokeCallbackSync((Action<ClockTimerCallbackContext>)Callback, new ClockTimerCallbackContext(this, CallbackState));
                break;

            case TimerCallbackKind.ContextActionWithToken:
                InvokeCallbackCancelSync((Action<ClockTimerCallbackContext, CancellationToken>)Callback, new ClockTimerCallbackContext(this, CallbackState));
                break;

            default:
                RunAsyncCallback();
                return;
        }
    }
    //----------------------------------------------------------------------------
    /// <summary>
    ///  Invokes the user callback asynchronously and sets up continuation for completion handling.
    /// </summary>
    private void RunAsyncCallback()
    {
        switch (CallbackKind)
        {
            case TimerCallbackKind.SimpleAsync:
                RunAsync((Func<CancellationToken, ValueTask>)Callback);
                return;

            case TimerCallbackKind.ContextAsync:
                RunAsync((Func<ClockTimerCallbackContext, CancellationToken, ValueTask>)Callback);
                return;

            default:
                PrimeTimeEventSource.Log.ClockTimerUnsupportedCallbackKind((int)CallbackKind, "DayTime");
                throw new InvalidOperationException($"Unsupported callback kind: {CallbackKind}");
        }
    }
    //----------------------------------------------------------------------------
    /// <summary>
    ///  Handles completion of the async callback, including cleanup and scheduling of sequential retries.
    /// </summary>
    private void OnAsyncComplete()
    {
        lock (Gate)
        {
            if (ShouldSkipTimerWorkWhileLocked())
            {
                Timer!.Change(Timeout.Infinite, Timeout.Infinite);
                CallbacksRunning--;
                return;
            }
            if (!_pendingRunSequential)
            {
                State = TimerState.Active;
                CallbacksRunning--;
                return;
            }
            // Run another loop
            _pendingRunSequential = false;
        }
        Task.Run(() => RunAsyncCallback())
            .ContinueWith(static (task, state) =>
                {
                    ClockDayTimeTimerRegistration registration = (ClockDayTimeTimerRegistration)state!;
                    if (task is { IsFaulted: true, Exception: not null })
                    {
                        PrimeTimeEventSource.Log.ClockTimerCallbackException("DayTime", task.Exception);
                    }
                    registration.OnAsyncComplete();
                },
                this,
                CancellationToken.None,
                TaskContinuationOptions.None,
                TaskScheduler.Default);
    }
    //----------------------------------------------------------------------------
    /// <summary>
    ///   Runs an async callback and continues on the thread pool when it does not complete synchronously.
    /// </summary>
    /// <param name="run">Async callback invocation.</param>
    private void RunAsync(Func<CancellationToken, ValueTask> run)
    {
        while (true)
        {
            ValueTask runResultTask = run(CancellationToken);

            if (runResultTask.IsCompletedSuccessfully)
            {
                lock (Gate)
                {
                    if (ShouldSkipTimerWorkWhileLocked())
                    {
                        Timer!.Change(Timeout.Infinite, Timeout.Infinite);
                        CallbacksRunning--;
                        return;
                    }
                    if (!_pendingRunSequential)
                    {
                        State = TimerState.Active;
                        CallbacksRunning--;
                        return;
                    }
                    // Run another loop
                    _pendingRunSequential = false;
                }
                // If we didn't return, then run another loop
                continue;
            }

            runResultTask.AsTask().ContinueWith(static (task, state) =>
                {
                    ClockDayTimeTimerRegistration registration = (ClockDayTimeTimerRegistration)state!;
                    if (task is { IsFaulted: true, Exception: not null })
                    {
                        PrimeTimeEventSource.Log.ClockTimerCallbackException("DayTime", task.Exception);
                    }
                    registration.OnAsyncComplete();
                },
                this,
                CancellationToken.None,
                TaskContinuationOptions.None,
                TaskScheduler.Default);
            // If we get here, we need to break the loop and return;
            return;
        }
    }
    //----------------------------------------------------------------------------
    /// <summary>
    ///   Runs an async callback and continues on the thread pool when it does not complete synchronously.
    /// </summary>
    /// <param name="run">Async callback invocation.</param>
    private void RunAsync(Func<ClockTimerCallbackContext, CancellationToken, ValueTask> run)
    {
        while (true)
        {
            ValueTask runResultTask = run(new ClockTimerCallbackContext(this, CallbackState), CancellationToken);

            if (runResultTask.IsCompletedSuccessfully)
            {
                lock (Gate)
                {
                    if (ShouldSkipTimerWorkWhileLocked())
                    {
                        Timer!.Change(Timeout.Infinite, Timeout.Infinite);
                        CallbacksRunning--;
                        return;
                    }
                    if (!_pendingRunSequential)
                    {
                        State = TimerState.Active;
                        CallbacksRunning--;
                        return;
                    }
                    // Run another loop
                    _pendingRunSequential = false;
                }
                // If we didn't return, then run another loop
                continue;
            }

            runResultTask.AsTask().ContinueWith(static (task, state) =>
                {
                    ClockDayTimeTimerRegistration registration = (ClockDayTimeTimerRegistration)state!;
                    if (task is { IsFaulted: true, Exception: not null })
                    {
                        PrimeTimeEventSource.Log.ClockTimerCallbackException("DayTime", task.Exception);
                    }
                    registration.OnAsyncComplete();
                },
                this,
                CancellationToken.None,
                TaskContinuationOptions.None,
                TaskScheduler.Default);
            // If we get here, we need to break the loop and return;
            return;
        }
    }
    //----------------------------------------------------------------------------
    /// <summary>
    ///   Completes initialization shared by stack-specific constructors.
    /// </summary>
    /// <param name="clock">
    ///   The clock instance used for scheduling and time.
    /// </param>
    /// <param name="utcTimeOfDaySchedule">
    ///   <c>true</c> for UTC calendar-day scheduling; <c>false</c> for local zone days.
    /// </param>
    /// <param name="callbackKind">The kind of callback delegate to invoke.</param>
    /// <param name="callback">User callback delegate.</param>
    /// <param name="callbackState">
    ///   Optional state passed to the callback via <see cref="ClockTimerCallbackContext"/>.
    /// </param>
    /// <param name="options">
    ///   Day-time timer options, or <c>null</c> to use default option values.
    /// </param>
    /// <param name="cancellationToken">Token that cancels the registration.</param>
    private void FinishConstruction(IPrimeClock clock, bool utcTimeOfDaySchedule,
        TimerCallbackKind callbackKind, Delegate callback,
        object? callbackState, DayTimeTimerOptions? options, CancellationToken cancellationToken)
    {
        DayTimeTimerOptions resolvedOptions = options ?? new DayTimeTimerOptions();
        ConcurrentTriggerProcessing = resolvedOptions.ConcurrentTriggerProcessing;
        SkippedTimeBehavior = resolvedOptions.SkippedTimeBehavior;
        DuplicateTimeBehavior = resolvedOptions.DuplicateTimeBehavior;

        FinishConstruction(clock, resolvedOptions.CallbackExecutionContext != TimerCallbackExecutionContext.Unsafe, utcTimeOfDaySchedule,
            callbackKind, callback, callbackState, cancellationToken);
        ScheduleNext();
    }
    //----------------------------------------------------------------------------

    #region Interface Implementations

#if NET
    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public bool Change (LocalTimeOfDay newTimeOfDay)
    {
        if (!IsLocalDayTimeSchedule)
            return false;
        lock (Gate)
        {
            if (Disposed || State == TimerState.Cancelled)
                return false;
            ApplyLocalScheduleTimeOfDay(newTimeOfDay.Value);
            if (!InternalEnabled)
                return true;
            ScheduleNext();
            return true;
        }
    }
    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public bool Change (UtcTimeOfDay newTimeOfDay)
    {
        if (!IsUtcDayTimeSchedule)
            return false;
        lock (Gate)
        {
            if (Disposed || State == TimerState.Cancelled)
                return false;
            ApplyUtcScheduleTimeOfDay(newTimeOfDay.Value);
            if (!InternalEnabled)
                return true;
            ScheduleNext();
            return true;
        }
    }

#endif

    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public ConcurrentTriggerProcessing ConcurrentTriggerProcessing { [DebuggerStepThrough] get; [DebuggerStepThrough] private set; }
    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public SkippedTimeBehavior SkippedTimeBehavior { [DebuggerStepThrough] get; [DebuggerStepThrough] private set; }
    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public DuplicateTimeBehavior DuplicateTimeBehavior { [DebuggerStepThrough] get; [DebuggerStepThrough] private set; }
    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public long ElapsedTime
    {
        get
        {
            lock (Gate)
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
            lock (Gate)
            {
                return GetDayTimeTimeUntilNextMillisecondsWhileLocked();
            }
        }
    }
    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public override bool IsTimeOfDay => true;
    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public override bool IsRepeating => true;
    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public override bool Start()
    {
        lock (Gate)
        {
            if (Disposed || State == TimerState.Cancelled)
                return false;
            if (State != TimerState.Disabled)
                return false;
            InternalEnabled = true;
            State = TimerState.Active;
            ScheduleNext();
            return true;
        }
    }
    //----------------------------------------------------------------------------

    #endregion Interface Implementations
}
//################################################################################
#endif
