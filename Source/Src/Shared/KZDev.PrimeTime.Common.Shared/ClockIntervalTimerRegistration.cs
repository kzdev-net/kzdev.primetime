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
internal sealed partial class ClockIntervalTimerRegistration : ClockTimerRegistration, IClockIntervalTimer
{
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
    ///   Computes <see cref="TimeUntilNextCallback"/> while <see cref="ClockTimerRegistration.Gate"/> is held.
    /// </summary>
    private partial long GetTimeUntilNextCallbackMillisecondsWhileLocked ();
    //----------------------------------------------------------------------------
    /// <summary>
    ///   When <c>true</c>, converts <paramref name="delay"/> to a due-time in milliseconds for
    ///   <see cref="System.Threading.Timer"/>; when <c>false</c>, the registration should not arm the timer.
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
    ///   Schedules or reschedules the next callback after <paramref name="delay"/>.
    /// </summary>
    /// <param name="delay">Delay until the next tick.</param>
    private void ScheduleNext (TimeSpan delay)
    {
        if (!TryGetTimerMillisecondsForSchedule(delay, out int timerMilliseconds))
            return;
        SetNextCallbackScheduledForDelay(delay);
        if (Timer is null)
            Timer = new Timer(OnTimerTick, null, timerMilliseconds, Timeout.Infinite);
        else
            Timer.Change(timerMilliseconds, Timeout.Infinite);
    }
    //----------------------------------------------------------------------------
    /// <summary>
    ///   BCL timer callback: runs on a thread-pool thread and invokes the user callback.
    /// </summary>
    /// <param name="_">Unused state object.</param>
    private void OnTimerTick (object? _)
    {
        bool isRepeating = IsRepeating;
        bool resetBefore = IsResetBeforeCallback;
        lock (Gate)
        {
            if (ShouldSkipTimerWorkWhileLocked())
            {
                Timer!.Change(Timeout.Infinite, Timeout.Infinite);
                return;
            }
            RecordIntervalCallbackStarted(resetBefore);

            if (isRepeating && resetBefore)
            {
                ScheduleNext(RepeatTimeSpanInterval);
            }
            else
            {
                Timer!.Change(Timeout.Infinite, Timeout.Infinite);
            }

            TimerState stateDuringCallback = isRepeating && resetBefore
                ? TimerState.RepeatProcessingCallback
                : TimerState.ProcessingCallback;

            State = stateDuringCallback;
            CallbacksRunning++;
        }

        try
        {
            RunCallback(resetBefore, isRepeating);
        }
        finally
        {
            lock (Gate)
            {
                CallbacksRunning--;
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
    ///   <see cref="ClockTimerRegistration.CallbackKind"/> is not supported.
    /// </exception>
    private void RunCallback (bool resetBefore, bool isRepeating)
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

            case TimerCallbackKind.SimpleAsync:
                RunAsyncAndScheduleAfter((Func<CancellationToken, ValueTask>)Callback, resetBefore, isRepeating);
                return;

            case TimerCallbackKind.ContextAsync:
                RunAsyncAndScheduleAfter((Func<ClockTimerCallbackContext, CancellationToken, ValueTask>)Callback, resetBefore, isRepeating);
                return;

            default:
                throw new InvalidOperationException($"Unsupported callback kind: {CallbackKind}");
        }

        OnCallbackCompleted(resetBefore, isRepeating);
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
            runResultTask = run(CancellationToken);
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
            runResultTask = run(new ClockTimerCallbackContext(this, CallbackState), CancellationToken);
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
        lock (Gate)
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
                State = (CallbacksRunning > 1) ? TimerState.ProcessingCallback : TimerState.RepeatCycle;
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
        lock (Gate)
        {
            if (Disposed)
                throw new ObjectDisposedException(nameof(IClockIntervalTimer));
            if (State == TimerState.Cancelled)
                return false;
            if (!IsRepeating && repeatInterval != Timeout.InfiniteTimeSpan && repeatInterval > TimeSpan.Zero)
                throw new InvalidOperationException("Cannot change a non-repeating timer to a repeating timer.");
            InitialCallbackTimeSpan = nextInterval;
            RepeatTimeSpanInterval = repeatInterval;
            if (State == TimerState.Completed)
                State = TimerState.Active;
            if (!InternalEnabled)
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
    ///   The clock instance used for scheduling and time.
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
        TimeSpan repeatInterval, TimerCallbackKind callbackKind, Delegate callback,
        object? callbackState, IntervalTimerOptions? options, CancellationToken cancellationToken)
    {
        InitialCallbackTimeSpan = initialCallbackTime;
        RepeatTimeSpanInterval = repeatInterval;
        IntervalTimerOptions resolvedOptions = options ?? new IntervalTimerOptions();
        IsResetBeforeCallback = resolvedOptions.ResetIntervalBeforeCallback;

        FinishConstruction (clock, resolvedOptions.CallbackExecutionContext != TimerCallbackExecutionContext.Unsafe, !resolvedOptions.LocalTimeRepresentation, 
            callbackKind, callback, callbackState, cancellationToken);
        ScheduleNext(initialCallbackTime);
    }
    //----------------------------------------------------------------------------

    #endregion Constructors/Finalizers

    #region ClockTimerRegistration Overrides

    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public override bool IsTimeOfDay => false;
    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public override bool IsRepeating { [DebuggerStepThrough] get => IsRepeatingTimer; }
    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public override bool Start ()
    {
        lock (Gate)
        {
            if (Disposed)
                throw new ObjectDisposedException(nameof(IClockIntervalTimer));

            if (State == TimerState.Cancelled)
                return false;
            if (State != TimerState.Completed && State != TimerState.Disabled)
                return false;
            InternalEnabled = true;
            State = TimerState.Active;
            ScheduleNext(InitialCallbackTimeSpan);
            return true;
        }
    }
    //----------------------------------------------------------------------------

    #endregion ClockTimerRegistration Overrides

    #region Interface Implementations

    #region IIntervalTimer Implementation

    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public bool IsResetBeforeCallback { [DebuggerStepThrough] get; }
    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public long ElapsedTime
    {
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
            lock (Gate)
            {
                return GetTimeUntilNextCallbackMillisecondsWhileLocked();
            }
        }
    }
    //----------------------------------------------------------------------------

    #endregion IIntervalTimer Implementation

    #region IClockIntervalTimer Implementation

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

    #endregion IClockIntervalTimer Implementation

    #endregion Interface Implementations
}
//################################################################################
