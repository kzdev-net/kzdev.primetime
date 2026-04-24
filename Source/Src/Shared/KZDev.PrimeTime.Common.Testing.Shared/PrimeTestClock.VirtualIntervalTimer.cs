// Copyright (c) Kevin Zehrer
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Diagnostics;

#if SYSTEMCLOCK
namespace KZDev.SystemClock.PrimeTime;
#else
using NodaTime;

namespace KZDev.PrimeTime;
#endif

//################################################################################
/// <summary>
///   Shared implementation of <see cref="IPrimeTestClock"/> virtual time, delays,
///   cancellation entries, and virtual interval/day-time timers. Stack-specific instant
///   storage and local-time mapping live in partials.
/// </summary>
public sealed partial class PrimeTestClock
{
    #region Nested types — Virtual interval timer

    //============================================================================
    /// <summary>
    ///   Base type for virtual interval timers scheduled against <see cref="PrimeTestClock"/> time.
    /// </summary>
    private abstract partial class VirtualIntervalTimerBase : IClockIntervalTimer
    {
        //------------------------------------------------------------------------
        /// <summary>
        ///   When <c>true</c>, registration uses local-offset representation for registered time.
        /// </summary>
        private readonly bool _isLocalTimeRepresentation;

        /// <summary>
        ///   When <c>true</c>, the next tick&apos;s countdown starts when the tick fires, before the callback completes.
        /// </summary>
        private readonly bool _resetBeforeCallback;
        //------------------------------------------------------------------------
        /// <summary>
        ///   Gets the owning test clock.
        /// </summary>
        protected PrimeTestClock Clock { [DebuggerStepThrough] get; }
        //------------------------------------------------------------------------
        /// <summary>
        ///   Gets the callback delegate shape for this registration.
        /// </summary>
        protected TimerCallbackKind CallbackKind { [DebuggerStepThrough] get; }
        //------------------------------------------------------------------------
        /// <summary>
        ///   Gets the user callback delegate.
        /// </summary>
        protected Delegate Callback { [DebuggerStepThrough] get; }
        //------------------------------------------------------------------------
        /// <summary>
        ///   Gets optional state passed to context-based callbacks.
        /// </summary>
        protected object? CallbackState { [DebuggerStepThrough] get; }
        //------------------------------------------------------------------------
        /// <summary>
        ///   Gets the cancellation token associated with this registration.
        /// </summary>
        protected CancellationToken CancellationToken { [DebuggerStepThrough] get; }
        //------------------------------------------------------------------------

#if NET10_OR_GREATER
        /// <summary>
        ///   Synchronizes access to this registration's mutable fields.
        /// </summary>
        protected Lock Gate { [DebuggerStepThrough] get; } = new();
#else
        //------------------------------------------------------------------------
        /// <summary>
        ///   Synchronizes access to this registration's mutable fields.
        /// </summary>
        protected object Gate { [DebuggerStepThrough] get; } = new();
        //------------------------------------------------------------------------
#endif

        //------------------------------------------------------------------------
        /// <summary>
        ///   Gets or sets the virtual UTC instant of the next scheduled callback, if any.
        /// </summary>
        protected DateTimeOffset? NextDueUtc { [DebuggerStepThrough] get; [DebuggerStepThrough] set; }
        //------------------------------------------------------------------------
        /// <summary>
        ///   Gets or sets the virtual UTC instant when the last callback started, if any.
        /// </summary>
        protected DateTimeOffset? LastCallbackUtc { [DebuggerStepThrough] get; [DebuggerStepThrough] set; }
        //------------------------------------------------------------------------
        /// <summary>
        ///   Gets or sets the logical timer state.
        /// </summary>
        protected TimerState State { [DebuggerStepThrough] get; [DebuggerStepThrough] set; } = TimerState.Active;
        //------------------------------------------------------------------------
        /// <summary>
        ///   Gets or sets whether further callbacks may be scheduled.
        /// </summary>
        protected bool IntervalTimerEnabled { [DebuggerStepThrough] get; [DebuggerStepThrough] private set; } = true;
        //------------------------------------------------------------------------
        /// <summary>
        ///   Gets or sets whether this registration has been disposed.
        /// </summary>
        protected bool Disposed { [DebuggerStepThrough] get; [DebuggerStepThrough] set; }
        //------------------------------------------------------------------------
        /// <summary>
        ///   Gets or sets whether external cancellation was requested.
        /// </summary>
        protected bool CancelRequested { [DebuggerStepThrough] get; [DebuggerStepThrough] set; }
        //------------------------------------------------------------------------
        /// <summary>
        ///   Gets or sets how many callbacks are currently in flight.
        /// </summary>
        protected int CallbacksRunning { [DebuggerStepThrough] get; [DebuggerStepThrough] set; }
        //------------------------------------------------------------------------
        /// <summary>
        ///   Gets or sets the delay until the first or next rescheduled callback.
        /// </summary>
        protected TimeSpan InitialCallbackTime { [DebuggerStepThrough] get; [DebuggerStepThrough] set; }
        //------------------------------------------------------------------------
        /// <summary>
        ///   Gets or sets the repeat interval, or <see cref="Timeout.InfiniteTimeSpan"/> for one-shot.
        /// </summary>
        protected TimeSpan RepeatInterval { [DebuggerStepThrough] get; [DebuggerStepThrough] set; }
        //------------------------------------------------------------------------
        /// <summary>
        ///   Initializes base state for a virtual interval timer registration.
        /// </summary>
        /// <param name="clock">The test clock that owns this registration.</param>
        /// <param name="initialCallbackTime">Delay until the first callback.</param>
        /// <param name="repeatInterval">Repeat interval, or infinite for one-shot.</param>
        /// <param name="callbackKind">Callback delegate shape.</param>
        /// <param name="callback">The callback delegate.</param>
        /// <param name="callbackState">Optional state for context callbacks.</param>
        /// <param name="options">Interval options, or <c>null</c> for defaults.</param>
        /// <param name="cancellationToken">Token that cancels this registration.</param>
        protected VirtualIntervalTimerBase (PrimeTestClock clock, TimeSpan initialCallbackTime,
            TimeSpan repeatInterval, TimerCallbackKind callbackKind, Delegate callback,
            object? callbackState, IntervalTimerOptions? options, CancellationToken cancellationToken)
        {
            Clock = clock;
            InitialCallbackTime = initialCallbackTime;
            RepeatInterval = repeatInterval;
            CallbackKind = callbackKind;
            Callback = callback;
            CallbackState = callbackState;
            CancellationToken = cancellationToken;
            _isLocalTimeRepresentation = options?.LocalTimeRepresentation == true;
            _resetBeforeCallback = options?.ResetIntervalBeforeCallback ?? false;
            Id = Interlocked.Increment(ref _nextTimerId);
            DateTimeOffset now = clock.UtcNowDateTimeOffset;
            RegisteredTime = _isLocalTimeRepresentation ? clock.LocalNowDateTimeOffset : now;
            NextDueUtc = now + initialCallbackTime;

            if (!cancellationToken.CanBeCanceled)
            {
                return;
            }

            cancellationToken.Register(OnCancelRequested);
            if (cancellationToken.IsCancellationRequested)
            {
                CancelRequested = true;
                State = TimerState.Cancelled;
                IntervalTimerEnabled = false;
            }
        }
        //------------------------------------------------------------------------

        //------------------------------------------------------------------------
        /// <inheritdoc />
        public int Id { [DebuggerStepThrough] get; }
        //------------------------------------------------------------------------
        /// <inheritdoc />
        public DateTimeOffset RegisteredTime { [DebuggerStepThrough] get; }
        //------------------------------------------------------------------------
        /// <inheritdoc />
        public bool IsTimeOfDay => false;
        //------------------------------------------------------------------------
        /// <inheritdoc />
        public bool IsRepeating => RepeatInterval != Timeout.InfiniteTimeSpan && RepeatInterval > TimeSpan.Zero;
        //------------------------------------------------------------------------
        /// <inheritdoc />
        public bool IsResetBeforeCallback => _resetBeforeCallback && IsRepeating;
        //------------------------------------------------------------------------
        /// <inheritdoc />
        public bool IsCancelled => State == TimerState.Cancelled;
        //------------------------------------------------------------------------
        /// <inheritdoc />
        public bool IsLocalTimeRepresentation => _isLocalTimeRepresentation;
        //------------------------------------------------------------------------
        /// <inheritdoc />
        public bool IsActive
        {
            get
            {
                // Capture local state value
                TimerState state = State;
                return state != TimerState.Cancelled &&
                       state != TimerState.Completed &&
                       state != TimerState.Disposed;
            }
        }
        //------------------------------------------------------------------------
        /// <inheritdoc />
        public bool CallbacksProcessing
        {
            get
            {
                lock (Gate)
                {
                    TimerState state = State;
                    return state != TimerState.Cancelled &&
                           state != TimerState.Disposed &&
                           CallbacksRunning > 0;
                }
            }
        }
        //------------------------------------------------------------------------
        /// <inheritdoc />
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
        //------------------------------------------------------------------------
        /// <inheritdoc />
        TimerState IClockTimer.State => State;
        //------------------------------------------------------------------------
        /// <inheritdoc />
        public long ElapsedTime
        {
            get
            {
                lock (Gate)
                {
                    if (LastCallbackUtc is not { } last)
                        return -1;
                    if (CallbacksRunning > 0)
                        return 0;
                    DateTimeOffset now = Clock.UtcNowDateTimeOffset;
                    if (last >= now)
                        return 0;
                    return (long)(now - last).TotalMilliseconds;
                }
            }
        }
        //------------------------------------------------------------------------
        /// <inheritdoc />
        public long TimeUntilNextCallback
        {
            get
            {
                lock (Gate)
                {
                    if (!IsRepeating && LastCallbackUtc.HasValue)
                        return -1;
                    if (NextDueUtc is not { } next)
                        return -1;
                    DateTimeOffset now = Clock.UtcNowDateTimeOffset;
                    if (next <= now)
                        return 0;
                    return (long)(next - now).TotalMilliseconds;
                }
            }
        }
        //------------------------------------------------------------------------
        /// <summary>
        ///   Returns whether this registration is due at the given virtual UTC instant.
        /// </summary>
        /// <param name="now">Current virtual UTC time from the test clock.</param>
        /// <returns>
        ///   <c>true</c> if a callback should run at <paramref name="now"/>; otherwise <c>false</c>.
        /// </returns>
        public bool IsDue (DateTimeOffset now)
        {
            lock (Gate)
            {
                if (Disposed || CancelRequested || State == TimerState.Cancelled || !IntervalTimerEnabled)
                    return false;
                if (NextDueUtc is not { } next)
                    return false;
                return next <= now;
            }
        }
        //------------------------------------------------------------------------
        /// <summary>
        ///   Runs the callback when this registration is due at <paramref name="now"/>.
        /// </summary>
        /// <param name="now">Virtual UTC instant passed from <see cref="PrimeTestClock.Advance(System.TimeSpan)"/>.</param>
        public abstract void RunDueCallback (DateTimeOffset now);
        //------------------------------------------------------------------------
        /// <summary>
        ///   Handles cancellation token registration: marks cancelled and removes from the clock.
        /// </summary>
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
        //------------------------------------------------------------------------
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
                if (Disposed || State == TimerState.Cancelled)
                    return false;
                if (!IsRepeating && repeatInterval != Timeout.InfiniteTimeSpan && repeatInterval > TimeSpan.Zero)
                    throw new InvalidOperationException("Cannot change a non-repeating timer to a repeating timer.");
                InitialCallbackTime = nextInterval;
                RepeatInterval = repeatInterval;
                if (State == TimerState.Completed)
                    State = TimerState.Active;
                if (!IntervalTimerEnabled)
                    return true;
                NextDueUtc = Clock.UtcNowDateTimeOffset + nextInterval;
                return true;
            }
        }
        //------------------------------------------------------------------------
        /// <inheritdoc />
        public bool Change (TimeSpan interval) =>
            ChangeNextAndRepeat(interval, IsRepeating ? interval : Timeout.InfiniteTimeSpan);
        //------------------------------------------------------------------------

        #region ITimer Implementation

        //------------------------------------------------------------------------
        /// <inheritdoc />
        bool ITimer.Change (TimeSpan dueTime, TimeSpan period) =>
            ChangeNextAndRepeat(dueTime, period);
        //------------------------------------------------------------------------

        #endregion ITimer Implementation

        //------------------------------------------------------------------------
        /// <inheritdoc />
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
        //------------------------------------------------------------------------
        /// <inheritdoc />
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
        //------------------------------------------------------------------------
        /// <inheritdoc />
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
                NextDueUtc = Clock.UtcNowDateTimeOffset + InitialCallbackTime;
                return true;
            }
        }
        //------------------------------------------------------------------------
        /// <inheritdoc />
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
        //------------------------------------------------------------------------

        #region IAsyncDisposable Implementation

        //------------------------------------------------------------------------
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
        //------------------------------------------------------------------------

        #endregion IAsyncDisposable Implementation
    }
    //============================================================================

    //============================================================================
    /// <summary>
    ///   Virtual interval timer implementation that invokes the user callback when due.
    /// </summary>
    private sealed partial class VirtualIntervalTimer : VirtualIntervalTimerBase
    {
        //------------------------------------------------------------------------
        /// <summary>
        ///   Initializes a new instance of the <see cref="VirtualIntervalTimer"/> class.
        /// </summary>
        /// <param name="clock">The owning test clock.</param>
        /// <param name="initialCallbackTime">Delay until the first callback.</param>
        /// <param name="repeatInterval">Repeat interval, or infinite for one-shot.</param>
        /// <param name="callbackKind">Callback delegate shape.</param>
        /// <param name="callback">The callback delegate.</param>
        /// <param name="callbackState">Optional callback state.</param>
        /// <param name="options">Interval options, or <c>null</c> for defaults.</param>
        /// <param name="cancellationToken">Token that cancels this registration.</param>
        public VirtualIntervalTimer (PrimeTestClock clock,
            TimeSpan initialCallbackTime,
            TimeSpan repeatInterval,
            TimerCallbackKind callbackKind,
            Delegate callback,
            object? callbackState,
            IntervalTimerOptions? options,
            CancellationToken cancellationToken)
            : base(clock, initialCallbackTime, repeatInterval, callbackKind, callback, callbackState, options, cancellationToken)
        {
        }
        //------------------------------------------------------------------------

        //------------------------------------------------------------------------
        /// <inheritdoc />
        public override void RunDueCallback (DateTimeOffset now)
        {
            bool resetBefore;
            bool isRepeating;
            DateTimeOffset firedAt;

            lock (Gate)
            {
                if (Disposed || CancelRequested || State == TimerState.Cancelled || !IntervalTimerEnabled)
                    return;
                if (NextDueUtc is not { } next || next > now)
                    return;
                firedAt = next;
                isRepeating = IsRepeating;
                resetBefore = IsResetBeforeCallback;
                if (isRepeating && resetBefore)
                    NextDueUtc = now + RepeatInterval;
                else
                    NextDueUtc = null;
                LastCallbackUtc = now;
                State = isRepeating && resetBefore ? TimerState.RepeatProcessingCallback : TimerState.ProcessingCallback;
                CallbacksRunning++;
            }

            try
            {
                RunCallback(resetBefore, isRepeating);
            }
            finally
            {
                lock (Gate)
                    CallbacksRunning--;
            }
        }
        //------------------------------------------------------------------------

        //------------------------------------------------------------------------
        /// <summary>
        ///   Invokes the registered callback synchronously or schedules async completion.
        /// </summary>
        /// <param name="resetBefore">
        ///   When <c>true</c>, the next due time was set when this tick started; do not reschedule on completion.
        /// </param>
        /// <param name="isRepeating">Whether this is a repeating timer.</param>
        /// <exception cref="InvalidOperationException">
        ///   <see cref="VirtualIntervalTimerBase.CallbackKind"/> is not supported.
        /// </exception>
        private void RunCallback (bool resetBefore, bool isRepeating)
        {
            void InvokeSync (Action run)
            {
                run();
            }

            switch (CallbackKind)
            {
                case TimerCallbackKind.SimpleAction:
                    InvokeSync(() => ((Action)Callback)());
                    break;
                case TimerCallbackKind.ContextAction:
                    InvokeSync(() => ((Action<ClockTimerCallbackContext>)Callback)(new ClockTimerCallbackContext(this, CallbackState)));
                    break;
                case TimerCallbackKind.ContextActionWithToken:
                    InvokeSync(() => ((Action<ClockTimerCallbackContext, CancellationToken>)Callback)(new ClockTimerCallbackContext(this, CallbackState),
                        CancellationToken));
                    break;
                case TimerCallbackKind.SimpleAsync:
                    RunAsyncAndScheduleAfter(() => ((Func<CancellationToken, ValueTask>)Callback)(CancellationToken),
                        resetBefore,
                        isRepeating);
                    return;
                case TimerCallbackKind.ContextAsync:
                    RunAsyncAndScheduleAfter(() => ((Func<ClockTimerCallbackContext, CancellationToken, ValueTask>)Callback)(new ClockTimerCallbackContext(this, CallbackState),
                            CancellationToken),
                        resetBefore,
                        isRepeating);
                    return;
                default:
                    throw new InvalidOperationException($"Unsupported callback kind: {CallbackKind}");
            }

            OnSyncCallbackCompleted(resetBefore, isRepeating);
        }
        //------------------------------------------------------------------------

        //------------------------------------------------------------------------
        /// <summary>
        ///   Runs an async callback and continues on the thread pool when it does not complete synchronously.
        /// </summary>
        /// <param name="run">The async callback invocation.</param>
        /// <param name="resetBefore">
        ///   When <c>true</c>, the next due time was set when this tick started; do not reschedule on completion.
        /// </param>
        /// <param name="isRepeating">Whether this is a repeating timer.</param>
        private void RunAsyncAndScheduleAfter (Func<ValueTask> run, bool resetBefore, bool isRepeating)
        {
            ValueTask vt;
            try
            {
                vt = run();
            }
            catch
            {
                OnAsyncCallbackCompleted(resetBefore, isRepeating);
                return;
            }

            if (vt.IsCompletedSuccessfully)
            {
                OnAsyncCallbackCompleted(resetBefore, isRepeating);
                return;
            }

            vt.AsTask().ContinueWith((_, state) =>
                {
                    (VirtualIntervalTimer reg, bool rb, bool rep) =
                        ((VirtualIntervalTimer, bool, bool))state!;
                    reg.OnAsyncCallbackCompleted(rb, rep);
                },
                (this, resetBefore, isRepeating),
                CancellationToken.None,
                TaskContinuationOptions.None,
                TaskScheduler.Default);
        }
        //------------------------------------------------------------------------

        //------------------------------------------------------------------------
        /// <summary>
        ///   After a synchronous callback, completes one-shot timers or schedules the next repeat.
        /// </summary>
        /// <param name="resetBefore">
        ///   When <c>true</c>, the next due time was already set when this tick started.
        /// </param>
        /// <param name="isRepeating">Whether this is a repeating timer.</param>
        private void OnSyncCallbackCompleted (bool resetBefore, bool isRepeating)
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
                if (!resetBefore)
                    NextDueUtc = Clock.UtcNowDateTimeOffset + RepeatInterval;
            }
        }
        //------------------------------------------------------------------------

        //------------------------------------------------------------------------
        /// <summary>
        ///   After an asynchronous callback completes, completes one-shot timers or schedules the next repeat.
        /// </summary>
        /// <param name="resetBefore">
        ///   When <c>true</c>, the next due time was already set when this tick started.
        /// </param>
        /// <param name="isRepeating">Whether this is a repeating timer.</param>
        private void OnAsyncCallbackCompleted (bool resetBefore, bool isRepeating)
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
                if (!resetBefore)
                    NextDueUtc = Clock.UtcNowDateTimeOffset + RepeatInterval;
            }
        }
        //------------------------------------------------------------------------
    }
    //============================================================================

    #endregion Nested types — Virtual interval timer

#if !SYSTEMCLOCK
    #region Nested types — Noda virtual interval extensions

    //============================================================================
    /// <summary>
    ///   NodaTime <see cref="Duration"/> and <see cref="Instant"/> surface for shared virtual interval timers.
    /// </summary>
    private abstract partial class VirtualIntervalTimerBase
    {
        //------------------------------------------------------------------------
        /// <summary>
        ///   Sentinel <see cref="Duration"/> mapped to <see cref="Timeout.InfiniteTimeSpan"/> for one-shot repeat intervals.
        /// </summary>
        private static readonly Duration NoRepeatSentinel = Duration.FromTimeSpan(Timeout.InfiniteTimeSpan);
        //------------------------------------------------------------------------

        //------------------------------------------------------------------------
        /// <inheritdoc />
        public Instant RegisteredInstant { [DebuggerStepThrough] get => Instant.FromDateTimeOffset(RegisteredTime); }
        //------------------------------------------------------------------------

        //------------------------------------------------------------------------
        /// <inheritdoc />
        public bool Change (Duration interval) =>
            ChangeNextAndRepeat(interval.ToTimeSpan(), IsRepeating ? interval.ToTimeSpan() : Timeout.InfiniteTimeSpan);
        //------------------------------------------------------------------------

        //------------------------------------------------------------------------
        /// <inheritdoc />
        public bool Change (Duration nextInterval, Duration repeatInterval)
        {
            TimeSpan repeatTs = repeatInterval == NoRepeatSentinel ? Timeout.InfiniteTimeSpan : repeatInterval.ToTimeSpan();
            return ChangeNextAndRepeat(nextInterval.ToTimeSpan(), repeatTs);
        }
        //------------------------------------------------------------------------
    }
    //============================================================================

    #endregion Nested types — Noda virtual interval extensions
#endif
}
//################################################################################


