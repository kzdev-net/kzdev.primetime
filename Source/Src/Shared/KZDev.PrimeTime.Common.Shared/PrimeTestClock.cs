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
public sealed partial class PrimeTestClock : IPrimeTestClock
{
    #region Nested types — Pending delay and time expiry

    //============================================================================
    /// <summary>
    ///   A virtual-time delay that completes a <see cref="TaskCompletionSource{TResult}"/> when
    ///   <see cref="PrimeTestClock.Advance"/> reaches <see cref="DueUtc"/>.
    /// </summary>
    private sealed class PendingDelay
    {
        //------------------------------------------------------------------------
        /// <summary>
        ///   Initializes a new instance of the <see cref="PendingDelay"/> class.
        /// </summary>
        /// <param name="dueUtc">Virtual UTC instant when the delay completes.</param>
        /// <param name="taskCompletionSource">Completion source signaled when due.</param>
        public PendingDelay (DateTimeOffset dueUtc, TaskCompletionSource<bool> taskCompletionSource)
        {
            DueUtc = dueUtc;
            TaskCompletionSource = taskCompletionSource;
        }
        //------------------------------------------------------------------------

        //------------------------------------------------------------------------
        /// <summary>
        ///   Gets the virtual UTC instant when this delay completes.
        /// </summary>
        public DateTimeOffset DueUtc { [DebuggerStepThrough] get; }
        //------------------------------------------------------------------------

        //------------------------------------------------------------------------
        /// <summary>
        ///   Gets the task completion source completed when the delay elapses.
        /// </summary>
        public TaskCompletionSource<bool> TaskCompletionSource { [DebuggerStepThrough] get; }
        //------------------------------------------------------------------------

        //------------------------------------------------------------------------
        /// <summary>
        ///   Completes the delay successfully if not already completed.
        /// </summary>
        public void Complete ()
        {
            TaskCompletionSource.TrySetResult(true);
        }
        //------------------------------------------------------------------------
    }
    //============================================================================

    //============================================================================
    /// <summary>
    ///   A time-based cancellation entry that cancels its wrapper when virtual UTC reaches
    ///   <see cref="ExpireUtc"/>.
    /// </summary>
    private sealed class TimeExpiryEntry
    {
        //------------------------------------------------------------------------
        /// <summary>
        ///   Cancellation wrapper whose token is cancelled when virtual time reaches <see cref="ExpireUtc"/>.
        /// </summary>
        private readonly TimeCancellationTokenSource _wrapper;
        //------------------------------------------------------------------------

        //------------------------------------------------------------------------
        /// <summary>
        ///   Initializes a new instance of the <see cref="TimeExpiryEntry"/> class.
        /// </summary>
        /// <param name="expireUtc">Virtual UTC instant when cancellation is requested.</param>
        /// <param name="wrapper">Wrapper whose token should be cancelled at expiry.</param>
        /// <param name="timeCts">Unused; reserved for future use.</param>
        public TimeExpiryEntry (DateTimeOffset expireUtc, TimeCancellationTokenSource wrapper,
            CancellationTokenSource? timeCts = null)
        {
            ExpireUtc = expireUtc;
            _wrapper = wrapper;
        }
        //------------------------------------------------------------------------

        //------------------------------------------------------------------------
        /// <summary>
        ///   Gets the virtual UTC instant when cancellation is requested.
        /// </summary>
        public DateTimeOffset ExpireUtc { [DebuggerStepThrough] get; }
        //------------------------------------------------------------------------

        //------------------------------------------------------------------------
        /// <summary>
        ///   Requests cancellation on the wrapper, ignoring <see cref="ObjectDisposedException"/> if already disposed.
        /// </summary>
        public void Cancel ()
        {
            try
            {
                _wrapper.Cancel();
            }
            catch (ObjectDisposedException)
            {
                // Caller may have disposed already.
            }
        }
        //------------------------------------------------------------------------
    }
    //============================================================================

    #endregion Nested types — Pending delay and time expiry

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
        ///   When <c>true</c>, repeat interval is measured from callback completion.
        /// </summary>
        private readonly bool _resetAfterCallback;
        //------------------------------------------------------------------------

        //------------------------------------------------------------------------
        /// <summary>
        ///   Gets the owning test clock.
        /// </summary>
        protected PrimeTestClock Clock { [DebuggerStepThrough] get; }
        //------------------------------------------------------------------------

        //------------------------------------------------------------------------
        /// <summary>
        ///   Gets the callback delegate shape for this registration.
        /// </summary>
        protected IntervalTimerCallbackKind CallbackKind { [DebuggerStepThrough] get; }
        //------------------------------------------------------------------------

        //------------------------------------------------------------------------
        /// <summary>
        ///   Gets the user callback delegate.
        /// </summary>
        protected Delegate Callback { [DebuggerStepThrough] get; }
        //------------------------------------------------------------------------

        //------------------------------------------------------------------------
        /// <summary>
        ///   Gets optional state passed to context-based callbacks.
        /// </summary>
        protected object? CallbackState { [DebuggerStepThrough] get; }
        //------------------------------------------------------------------------

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

        //------------------------------------------------------------------------
        /// <summary>
        ///   Gets or sets the virtual UTC instant when the last callback started, if any.
        /// </summary>
        protected DateTimeOffset? LastCallbackUtc { [DebuggerStepThrough] get; [DebuggerStepThrough] set; }
        //------------------------------------------------------------------------

        //------------------------------------------------------------------------
        /// <summary>
        ///   Gets or sets the logical timer state.
        /// </summary>
        protected TimerState State { [DebuggerStepThrough] get; [DebuggerStepThrough] set; } = TimerState.Active;
        //------------------------------------------------------------------------

        //------------------------------------------------------------------------
        /// <summary>
        ///   Gets or sets whether further callbacks may be scheduled.
        /// </summary>
        protected bool IntervalTimerEnabled { [DebuggerStepThrough] get; [DebuggerStepThrough] private set; } = true;
        //------------------------------------------------------------------------

        //------------------------------------------------------------------------
        /// <summary>
        ///   Gets or sets whether this registration has been disposed.
        /// </summary>
        protected bool Disposed { [DebuggerStepThrough] get; [DebuggerStepThrough] set; }
        //------------------------------------------------------------------------

        //------------------------------------------------------------------------
        /// <summary>
        ///   Gets or sets whether external cancellation was requested.
        /// </summary>
        protected bool CancelRequested { [DebuggerStepThrough] get; [DebuggerStepThrough] set; }
        //------------------------------------------------------------------------

        //------------------------------------------------------------------------
        /// <summary>
        ///   Gets or sets how many callbacks are currently in flight.
        /// </summary>
        protected int CallbacksRunning { [DebuggerStepThrough] get; [DebuggerStepThrough] set; }
        //------------------------------------------------------------------------

        //------------------------------------------------------------------------
        /// <summary>
        ///   Gets or sets the delay until the first or next rescheduled callback.
        /// </summary>
        protected TimeSpan InitialCallbackTime { [DebuggerStepThrough] get; [DebuggerStepThrough] set; }
        //------------------------------------------------------------------------

        //------------------------------------------------------------------------
        /// <summary>
        ///   Gets or sets the repeat interval, or <see cref="Timeout.InfiniteTimeSpan"/> for one-shot.
        /// </summary>
        protected TimeSpan RepeatInterval { [DebuggerStepThrough] get; [DebuggerStepThrough] set; }
        //------------------------------------------------------------------------

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
        protected VirtualIntervalTimerBase (PrimeTestClock clock,
            TimeSpan initialCallbackTime,
            TimeSpan repeatInterval,
            IntervalTimerCallbackKind callbackKind,
            Delegate callback,
            object? callbackState,
            IntervalTimerOptions? options,
            CancellationToken cancellationToken)
        {
            Clock = clock;
            InitialCallbackTime = initialCallbackTime;
            RepeatInterval = repeatInterval;
            CallbackKind = callbackKind;
            Callback = callback;
            CallbackState = callbackState;
            CancellationToken = cancellationToken;
            _isLocalTimeRepresentation = options?.LocalTimeRepresentation == true;
            _resetAfterCallback = options?.ResetIntervalAfterCallback ?? false;
            Id = Interlocked.Increment(ref _nextTimerId);
            DateTimeOffset now = clock.UtcNowDateTimeOffset;
            RegisteredTime = _isLocalTimeRepresentation ? clock.LocalNowDateTimeOffset : now;
            NextDueUtc = now + initialCallbackTime;

            if (cancellationToken.CanBeCanceled)
            {
                cancellationToken.Register(OnCancelRequested);
                if (cancellationToken.IsCancellationRequested)
                {
                    CancelRequested = true;
                    State = TimerState.Cancelled;
                    IntervalTimerEnabled = false;
                }
            }
        }
        //------------------------------------------------------------------------

        //------------------------------------------------------------------------
        /// <inheritdoc />
        public int Id { [DebuggerStepThrough] get; }
        //------------------------------------------------------------------------

        //------------------------------------------------------------------------
        /// <inheritdoc />
        public DateTimeOffset RegisteredTime { [DebuggerStepThrough] get; }
        //------------------------------------------------------------------------

        //------------------------------------------------------------------------
        /// <inheritdoc />
        public bool IsTimeOfDay => false;
        //------------------------------------------------------------------------

        //------------------------------------------------------------------------
        /// <inheritdoc />
        public bool IsRepeating => RepeatInterval != Timeout.InfiniteTimeSpan && RepeatInterval > TimeSpan.Zero;
        //------------------------------------------------------------------------

        //------------------------------------------------------------------------
        /// <inheritdoc />
        public bool IsResetAfterCallback => _resetAfterCallback && IsRepeating;
        //------------------------------------------------------------------------

        //------------------------------------------------------------------------
        /// <inheritdoc />
        public bool IsCancelled => State == TimerState.Cancelled;
        //------------------------------------------------------------------------

        //------------------------------------------------------------------------
        /// <inheritdoc />
        public bool IsLocalTimeRepresentation => _isLocalTimeRepresentation;
        //------------------------------------------------------------------------

        //------------------------------------------------------------------------
        /// <inheritdoc />
        public bool IsActive =>
            State != TimerState.Cancelled && State != TimerState.Disposed && IntervalTimerEnabled;
        //------------------------------------------------------------------------

        //------------------------------------------------------------------------
        /// <inheritdoc />
        public bool CallbacksProcessing => CallbacksRunning > 0;
        //------------------------------------------------------------------------

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

        //------------------------------------------------------------------------
        /// <inheritdoc />
        TimerState IClockTimer.State => State;
        //------------------------------------------------------------------------

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
                    if (CallbacksRunning > 0 && IsResetAfterCallback)
                        return (long)RepeatInterval.TotalMilliseconds;
                    return (long)(next - now).TotalMilliseconds;
                }
            }
        }
        //------------------------------------------------------------------------

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

        //------------------------------------------------------------------------
        /// <summary>
        ///   Runs the callback when this registration is due at <paramref name="now"/>.
        /// </summary>
        /// <param name="now">Virtual UTC instant passed from <see cref="PrimeTestClock.Advance"/>.</param>
        public abstract void RunDueCallback (DateTimeOffset now);
        //------------------------------------------------------------------------

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
            IntervalTimerCallbackKind callbackKind,
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
            bool resetAfter;
            bool isRepeating;
            DateTimeOffset firedAt;

            lock (Gate)
            {
                if (Disposed || CancelRequested || State == TimerState.Cancelled || !IntervalTimerEnabled)
                    return;
                if (NextDueUtc is not { } next || next > now)
                    return;
                firedAt = next;
                NextDueUtc = null;
                LastCallbackUtc = firedAt;
                isRepeating = IsRepeating;
                resetAfter = IsResetAfterCallback;
                State = isRepeating && !resetAfter ? TimerState.RepeatProcessingCallback : TimerState.ProcessingCallback;
                CallbacksRunning++;
            }

            try
            {
                RunCallback(resetAfter, isRepeating, firedAt);
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
        /// <param name="resetAfter">Whether the repeat interval resets after callback completion.</param>
        /// <param name="isRepeating">Whether this is a repeating timer.</param>
        /// <param name="now">Virtual instant at which the callback was due.</param>
        /// <exception cref="InvalidOperationException">
        ///   <see cref="VirtualIntervalTimerBase.CallbackKind"/> is not supported.
        /// </exception>
        private void RunCallback (bool resetAfter, bool isRepeating, DateTimeOffset now)
        {
            void InvokeSync (Action run)
            {
                run();
            }

            switch (CallbackKind)
            {
                case IntervalTimerCallbackKind.SimpleAction:
                    InvokeSync(() => ((Action)Callback)());
                    break;
                case IntervalTimerCallbackKind.ContextAction:
                    InvokeSync(() => ((Action<ClockTimerCallbackContext>)Callback)(new ClockTimerCallbackContext(this, CallbackState)));
                    break;
                case IntervalTimerCallbackKind.ContextActionWithToken:
                    InvokeSync(() => ((Action<ClockTimerCallbackContext, CancellationToken>)Callback)(new ClockTimerCallbackContext(this, CallbackState),
                        CancellationToken));
                    break;
                case IntervalTimerCallbackKind.SimpleAsync:
                    RunAsyncAndScheduleAfter(() => ((Func<CancellationToken, ValueTask>)Callback)(CancellationToken),
                        resetAfter,
                        isRepeating,
                        now);
                    return;
                case IntervalTimerCallbackKind.ContextAsync:
                    RunAsyncAndScheduleAfter(() => ((Func<ClockTimerCallbackContext, CancellationToken, ValueTask>)Callback)(new ClockTimerCallbackContext(this, CallbackState),
                            CancellationToken),
                        resetAfter,
                        isRepeating,
                        now);
                    return;
                default:
                    throw new InvalidOperationException($"Unsupported callback kind: {CallbackKind}");
            }

            OnSyncCallbackCompleted(resetAfter, isRepeating, now);
        }
        //------------------------------------------------------------------------

        //------------------------------------------------------------------------
        /// <summary>
        ///   Runs an async callback and continues on the thread pool when it does not complete synchronously.
        /// </summary>
        /// <param name="run">The async callback invocation.</param>
        /// <param name="resetAfter">Whether the repeat interval resets after callback completion.</param>
        /// <param name="isRepeating">Whether this is a repeating timer.</param>
        /// <param name="now">Virtual instant at which the callback was due.</param>
        private void RunAsyncAndScheduleAfter (Func<ValueTask> run, bool resetAfter, bool isRepeating, DateTimeOffset now)
        {
            ValueTask vt = run();
            if (vt.IsCompletedSuccessfully)
            {
                OnAsyncCallbackCompleted(resetAfter, isRepeating, now);
                return;
            }

            vt.AsTask().ContinueWith((_, state) =>
                {
                    (VirtualIntervalTimer reg, bool ra, bool rep, DateTimeOffset n) =
                        ((VirtualIntervalTimer, bool, bool, DateTimeOffset))state!;
                    reg.OnAsyncCallbackCompleted(ra, rep, n);
                },
                (this, resetAfter, isRepeating, now),
                CancellationToken.None,
                TaskContinuationOptions.None,
                TaskScheduler.Default);
        }
        //------------------------------------------------------------------------

        //------------------------------------------------------------------------
        /// <summary>
        ///   After a synchronous callback, completes one-shot timers or schedules the next repeat.
        /// </summary>
        /// <param name="resetAfter">Whether the repeat interval resets after callback completion.</param>
        /// <param name="isRepeating">Whether this is a repeating timer.</param>
        /// <param name="now">Virtual instant when the callback ran.</param>
        private void OnSyncCallbackCompleted (bool resetAfter, bool isRepeating, DateTimeOffset now)
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
                NextDueUtc = now + RepeatInterval;
            }
        }
        //------------------------------------------------------------------------

        //------------------------------------------------------------------------
        /// <summary>
        ///   After an asynchronous callback completes, completes one-shot timers or schedules the next repeat.
        /// </summary>
        /// <param name="resetAfter">Whether the repeat interval resets after callback completion.</param>
        /// <param name="isRepeating">Whether this is a repeating timer.</param>
        /// <param name="now">Virtual instant when the callback ran.</param>
        private void OnAsyncCallbackCompleted (bool resetAfter, bool isRepeating, DateTimeOffset now)
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
                NextDueUtc = now + RepeatInterval;
            }
        }
        //------------------------------------------------------------------------
    }
    //============================================================================

    #endregion Nested types — Virtual interval timer

#if NET || !SYSTEMCLOCK
    #region Nested types — Virtual day-time timer

    //============================================================================
    /// <summary>
    ///   Base type for virtual time-of-day timers driven by <see cref="PrimeTestClock"/> time.
    /// </summary>
    private abstract partial class VirtualDayTimeTimerBase : IClockDayTimeTimer
    {
        //------------------------------------------------------------------------
        /// <summary>
        ///   One calendar day as a <see cref="TimeSpan"/> for arithmetic in partial implementations.
        /// </summary>
        private static readonly TimeSpan OneDay = TimeSpan.FromDays(1);
        //------------------------------------------------------------------------

        //------------------------------------------------------------------------
        /// <summary>
        ///   Gets the owning test clock.
        /// </summary>
        protected PrimeTestClock Clock { [DebuggerStepThrough] get; }
        //------------------------------------------------------------------------

        //------------------------------------------------------------------------
        /// <summary>
        ///   Gets whether this registration uses local time-of-day versus UTC time-of-day.
        /// </summary>
        protected bool IsLocal { [DebuggerStepThrough] get; }
        //------------------------------------------------------------------------

        //------------------------------------------------------------------------
        /// <summary>
        ///   Gets the callback delegate shape for this registration.
        /// </summary>
        protected IntervalTimerCallbackKind CallbackKind { [DebuggerStepThrough] get; }
        //------------------------------------------------------------------------

        //------------------------------------------------------------------------
        /// <summary>
        ///   Gets the user callback delegate.
        /// </summary>
        protected Delegate Callback { [DebuggerStepThrough] get; }
        //------------------------------------------------------------------------

        //------------------------------------------------------------------------
        /// <summary>
        ///   Gets optional state passed to context-based callbacks.
        /// </summary>
        protected object? CallbackState { [DebuggerStepThrough] get; }
        //------------------------------------------------------------------------

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
        ///   Gets or sets the next scheduled callback instant in UTC.
        /// </summary>
        protected DateTimeOffset? NextDueUtc { [DebuggerStepThrough] get; [DebuggerStepThrough] set; }
        //------------------------------------------------------------------------

        //------------------------------------------------------------------------
        /// <summary>
        ///   The instant of the last callback.
        /// </summary>
        /// <remarks>
        ///   <para>
        ///     Because the offset depends on <see cref="IsLocal"/> and on the time zone and DST
        ///     rules in effect at the instant of the callback, values from different timers, or
        ///     from the same timer across offset changes (such as daylight saving time
        ///     transitions), MUST NOT be naively compared or ordered based solely on the
        ///     <see cref="DateTimeOffset"/> value. In particular:
        ///   </para>
        ///   <list type="bullet">
        ///     <item>
        ///       <description>
        ///         Do not assume that two <see cref="LastCallbackAt"/> values with different
        ///         <see cref="DateTimeOffset.Offset"/> values are different instants; they may
        ///         represent the same UTC instant with different offsets.
        ///       </description>
        ///     </item>
        ///     <item>
        ///       <description>
        ///         Do not compare <see cref="LastCallbackAt"/> values from local and UTC
        ///         timers (or from different time zones) without first normalizing to a common
        ///         time basis (for example, via <see cref="DateTimeOffset.UtcDateTime"/> or
        ///         <see cref="DateTimeOffset.UtcTicks"/>).
        ///       </description>
        ///     </item>
        ///   </list>
        ///   <para>
        ///     This property is primarily intended for diagnostics and for reasoning about a
        ///     single registration in isolation. Code that needs to compare instants across
        ///     timers or across time zones should perform such comparisons in UTC.
        ///   </para>
        ///   <para>
        ///     For <c>IsLocal == true</c> day-time registrations, this stores the same offset
        ///     representation as <see cref="PrimeTestClock.LocalNowDateTimeOffset"/> (that is, the local
        ///     offset that was in effect at the time of the callback). For UTC day-time
        ///     registrations, this is stored with a UTC offset so it aligns with the "now" used
        ///     in <see cref="ElapsedTime"/>.
        ///   </para>
        ///   <para>
        ///     This dual representation is deliberate: it preserves the developer-facing view
        ///     of when the timer fired in the same coordinate system in which the timer was
        ///     registered (local clock versus UTC clock), while still representing a single
        ///     underlying instant in time.
        ///   </para>
        /// </remarks>
        protected DateTimeOffset? LastCallbackAt { [DebuggerStepThrough] get; [DebuggerStepThrough] set; }
        //------------------------------------------------------------------------

        //------------------------------------------------------------------------
        /// <summary>
        ///   Gets or sets the logical timer state.
        /// </summary>
        protected TimerState State { [DebuggerStepThrough] get; [DebuggerStepThrough] set; } = TimerState.Active;
        //------------------------------------------------------------------------

        //------------------------------------------------------------------------
        /// <summary>
        ///   Gets or sets whether day-time callbacks are enabled.
        /// </summary>
        protected bool EnabledDayTime { [DebuggerStepThrough] get; [DebuggerStepThrough] set; } = true;
        //------------------------------------------------------------------------

        //------------------------------------------------------------------------
        /// <summary>
        ///   Gets or sets whether this registration has been disposed.
        /// </summary>
        protected bool Disposed { [DebuggerStepThrough] get; [DebuggerStepThrough] set; }
        //------------------------------------------------------------------------

        //------------------------------------------------------------------------
        /// <summary>
        ///   Gets or sets whether external cancellation was requested.
        /// </summary>
        protected bool CancelRequested { [DebuggerStepThrough] get; [DebuggerStepThrough] set; }
        //------------------------------------------------------------------------

        //------------------------------------------------------------------------
        /// <summary>
        ///   Gets or sets how many callbacks are currently in flight.
        /// </summary>
        protected int CallbacksRunning { [DebuggerStepThrough] get; [DebuggerStepThrough] set; }
        //------------------------------------------------------------------------

        //------------------------------------------------------------------------
        /// <summary>
        ///   Time of day since local or UTC midnight (depending on <see cref="IsLocal"/>).
        /// </summary>
        protected TimeSpan TargetTimeOfDay { [DebuggerStepThrough] get; [DebuggerStepThrough] set; }
        //------------------------------------------------------------------------

        //------------------------------------------------------------------------
        /// <summary>
        ///   Concurrent invocation policy copied from registration options.
        /// </summary>
        private readonly ConcurrentTriggerProcessing _concurrentTriggerProcessing;

        /// <summary>
        ///   Skipped-time policy from registration options (same resolution as
        ///   <see cref="ClockDayTimeTimerRegistration"/>).
        /// </summary>
        private readonly SkippedTimeBehavior _skippedTimeBehavior;

        /// <summary>
        ///   Duplicate-trigger policy from registration options (same resolution as
        ///   <see cref="ClockDayTimeTimerRegistration"/>).
        /// </summary>
        private readonly DuplicateTimeBehavior _duplicateTimeBehavior;
        //------------------------------------------------------------------------

        //------------------------------------------------------------------------
        /// <summary>
        ///   Initializes base state for a virtual day-time timer registration.
        /// </summary>
        /// <param name="clock">The owning test clock.</param>
        /// <param name="isLocal"><c>true</c> for local time-of-day; <c>false</c> for UTC.</param>
        /// <param name="targetTimeOfDay">Time of day since midnight in the chosen basis.</param>
        /// <param name="callbackKind">Callback delegate shape.</param>
        /// <param name="callback">The callback delegate.</param>
        /// <param name="callbackState">Optional callback state.</param>
        /// <param name="options">Day-time options, or <c>null</c> for defaults.</param>
        /// <param name="cancellationToken">Token that cancels this registration.</param>
        protected VirtualDayTimeTimerBase (PrimeTestClock clock,
            bool isLocal,
            TimeSpan targetTimeOfDay,
            IntervalTimerCallbackKind callbackKind,
            Delegate callback,
            object? callbackState,
            DayTimeTimerOptions? options,
            CancellationToken cancellationToken)
        {
            Clock = clock;
            IsLocal = isLocal;
            TargetTimeOfDay = targetTimeOfDay;
            CallbackKind = callbackKind;
            Callback = callback;
            CallbackState = callbackState;
            CancellationToken = cancellationToken;
            DayTimeTimerOptions resolvedOptions = options ?? new DayTimeTimerOptions();
            _concurrentTriggerProcessing = resolvedOptions.ConcurrentTriggerProcessing;
            _skippedTimeBehavior = resolvedOptions.SkippedTimeBehavior;
            _duplicateTimeBehavior = resolvedOptions.DuplicateTimeBehavior;
            Id = Interlocked.Increment(ref _nextTimerId);
            RegisteredTime = Clock.UtcNowDateTimeOffset;
            NextDueUtc = ComputeNextDue(clock.UtcNowDateTimeOffset);

            if (cancellationToken.CanBeCanceled)
            {
                cancellationToken.Register(OnCancelRequested);
                if (cancellationToken.IsCancellationRequested)
                {
                    CancelRequested = true;
                    State = TimerState.Cancelled;
                    EnabledDayTime = false;
                }
            }
        }
        //------------------------------------------------------------------------

        //------------------------------------------------------------------------
        /// <inheritdoc />
        public int Id { [DebuggerStepThrough] get; }
        //------------------------------------------------------------------------

        //------------------------------------------------------------------------
        /// <inheritdoc />
        public DateTimeOffset RegisteredTime { [DebuggerStepThrough] get; }
        //------------------------------------------------------------------------

        //------------------------------------------------------------------------
        /// <inheritdoc />
        public bool IsTimeOfDay => true;
        //------------------------------------------------------------------------

        //------------------------------------------------------------------------
        /// <inheritdoc />
        public bool IsResetAfterCallback => false;
        //------------------------------------------------------------------------

        //------------------------------------------------------------------------
        /// <inheritdoc />
        public bool IsLocalTimeRepresentation => IsLocal;
        //------------------------------------------------------------------------

        //------------------------------------------------------------------------
        /// <inheritdoc />
        public bool IsRepeating => true;
        //------------------------------------------------------------------------

        //------------------------------------------------------------------------
        /// <inheritdoc />
        public bool IsCancelled => State == TimerState.Cancelled;
        //------------------------------------------------------------------------

        //------------------------------------------------------------------------
        /// <inheritdoc />
        public bool IsActive =>
            State != TimerState.Cancelled && State != TimerState.Disposed && EnabledDayTime;
        //------------------------------------------------------------------------

        //------------------------------------------------------------------------
        /// <inheritdoc />
        public bool CallbacksProcessing => CallbacksRunning > 0;
        //------------------------------------------------------------------------

        //------------------------------------------------------------------------
        /// <inheritdoc />
        public ConcurrentTriggerProcessing ConcurrentTriggerProcessing { [DebuggerStepThrough] get => _concurrentTriggerProcessing; }
        //------------------------------------------------------------------------

        //------------------------------------------------------------------------
        /// <inheritdoc />
        public SkippedTimeBehavior SkippedTimeBehavior { [DebuggerStepThrough] get => _skippedTimeBehavior; }
        //------------------------------------------------------------------------

        //------------------------------------------------------------------------
        /// <inheritdoc />
        public DuplicateTimeBehavior DuplicateTimeBehavior { [DebuggerStepThrough] get => _duplicateTimeBehavior; }
        //------------------------------------------------------------------------

        //------------------------------------------------------------------------
        /// <inheritdoc />
        public bool Enabled
        {
            get => EnabledDayTime && !IsCancelled && State != TimerState.Disposed;
            set
            {
                lock (Gate)
                {
                    if (Disposed || State == TimerState.Cancelled)
                        return;
                    if (value)
                    {
                        EnabledDayTime = true;
                        State = TimerState.Active;
                        NextDueUtc = ComputeNextDue(Clock.UtcNowDateTimeOffset);
                    }
                    else
                    {
                        EnabledDayTime = false;
                        State = TimerState.Disabled;
                    }
                }
            }
        }
        //------------------------------------------------------------------------

        //------------------------------------------------------------------------
        /// <inheritdoc />
        TimerState IClockTimer.State => State;
        //------------------------------------------------------------------------

        //------------------------------------------------------------------------
        /// <summary>
        ///   Returns a <see cref="DateTimeOffset"/> for <paramref name="virtualUtcInstant"/> in this registration's
        ///   coordinate system: local-offset when <see cref="IsLocal"/> (via <see cref="PrimeTestClock.ToLocalOffset"/>),
        ///   otherwise UTC-offset unchanged—so <see cref="ElapsedTime"/> and <see cref="LastCallbackAt"/> compare like
        ///   with like.
        /// </summary>
        /// <param name="virtualUtcInstant">The virtual instant (typically <see cref="PrimeTestClock.UtcNowDateTimeOffset"/>).</param>
        /// <returns>
        ///   The same instant expressed in the offset form that matches local versus UTC day-time registration.
        /// </returns>
        protected DateTimeOffset GetOffsetRepresentationForTimer (DateTimeOffset virtualUtcInstant) =>
            IsLocal ? Clock.ToLocalOffset(virtualUtcInstant) : virtualUtcInstant;
        //------------------------------------------------------------------------

        //------------------------------------------------------------------------
        /// <inheritdoc />
        public long ElapsedTime
        {
            get
            {
                lock (Gate)
                {
                    if (LastCallbackAt is not { } last)
                        return -1;
                    if (CallbacksRunning > 0)
                        return 0;
                    DateTimeOffset now = GetOffsetRepresentationForTimer(Clock.UtcNowDateTimeOffset);
                    if (last >= now)
                        return 0;
                    return (long)(now - last).TotalMilliseconds;
                }
            }
        }
        //------------------------------------------------------------------------

        //------------------------------------------------------------------------
        /// <inheritdoc />
        public long TimeUntilNextCallback
        {
            get
            {
                lock (Gate)
                {
                    if (NextDueUtc is not { } next)
                        return -1;
                    // NextDueUtc is stored as a UTC instant; use virtual UTC now for the same representation.
                    DateTimeOffset now = Clock.UtcNowDateTimeOffset;
                    if (next <= now)
                        return 0;
                    return (long)(next - now).TotalMilliseconds;
                }
            }
        }
        //------------------------------------------------------------------------

        //------------------------------------------------------------------------
        /// <summary>
        ///   Returns whether this day-time registration is due at the given virtual UTC instant.
        /// </summary>
        /// <param name="now">Current virtual UTC time from the test clock.</param>
        /// <returns>
        ///   <c>true</c> if a callback should run at <paramref name="now"/>; otherwise <c>false</c>.
        /// </returns>
        public bool IsDue (DateTimeOffset now)
        {
            lock (Gate)
            {
                if (Disposed || CancelRequested || State == TimerState.Cancelled || !EnabledDayTime)
                    return false;
                if (NextDueUtc is not { } next)
                    return false;
                return next <= now;
            }
        }
        //------------------------------------------------------------------------

        //------------------------------------------------------------------------
        /// <summary>
        ///   Runs the day-time callback when due at <paramref name="now"/>.
        /// </summary>
        /// <param name="now">Virtual UTC instant from <see cref="PrimeTestClock.Advance"/>.</param>
        public abstract void RunDueCallback (DateTimeOffset now);
        //------------------------------------------------------------------------

        //------------------------------------------------------------------------
        /// <summary>
        ///   Computes the next UTC instant when <see cref="TargetTimeOfDay"/> should fire on or after the clock&apos;s
        ///   current instant, using the same local wall-time policies as production
        ///   (<see cref="DayTimeBclLocalWallTimeScheduling"/> and the NodaTime scheduling helper in the superset assembly).
        /// </summary>
        /// <param name="now">Current virtual UTC instant (used for UTC day-time; local scheduling uses the clock&apos;s local &quot;now&quot;).</param>
        /// <returns>The next scheduled fire instant in UTC, or <c>null</c> if not applicable.</returns>
        /// <remarks>
        ///   <para>
        ///     For <see cref="IsLocal"/> on the System Clock stack, the schedule zone is
        ///     <see cref="IPrimeClock.LocalScheduleTimeZone"/> (see <see cref="PrimeTestClock.LocalScheduleTimeZone"/>),
        ///     which is not injectable on <see cref="PrimeTestClock"/>; deterministic DST edge tests should use the Noda
        ///     <see cref="PrimeTestClock"/> constructor that accepts a <c>NodaTime.DateTimeZone</c>.
        ///   </para>
        /// </remarks>
        protected DateTimeOffset? ComputeNextDue (DateTimeOffset now)
        {
            if (IsLocal)
            {
#if SYSTEMCLOCK
                TimeSpan delay = DayTimeBclLocalWallTimeScheduling.GetDelayUntilNextLocalDayTime(
                    Clock.LocalNowDateTimeOffset,
                    Clock.LocalScheduleTimeZone,
                    TimeOnly.FromTimeSpan(TargetTimeOfDay),
                    _skippedTimeBehavior,
                    _duplicateTimeBehavior);
                if (delay <= TimeSpan.Zero)
                {
                    delay = TimeSpan.FromDays(1);
                }

                DateTimeOffset scheduleBasis = Clock.LocalNowDateTimeOffset;
                return new DateTimeOffset((scheduleBasis + delay).UtcDateTime, TimeSpan.Zero);
#else
                LocalTime targetLocal = LocalTime.FromTicksSinceMidnight(TargetTimeOfDay.Ticks);
                Duration delayNoda = DayTimeNodaLocalWallTimeScheduling.GetDelayUntilNextLocalDayTime(
                    Clock.NowInstant,
                    Clock._zone,
                    targetLocal,
                    _skippedTimeBehavior,
                    _duplicateTimeBehavior);
                if (delayNoda <= Duration.Zero)
                {
                    delayNoda = Duration.FromDays(1);
                }

                Instant fireInstant = Clock.NowInstant + delayNoda;
                return new DateTimeOffset(fireInstant.ToDateTimeUtc(), TimeSpan.Zero);
#endif
            }
            else
            {
                DateTime utcDate = now.UtcDateTime;
                DateTime utcMidnight = utcDate.Date;
                DateTime nextDt = utcMidnight + TargetTimeOfDay;
                if (nextDt <= utcDate)
                    nextDt = nextDt.AddDays(1);
                return new DateTimeOffset(DateTime.SpecifyKind(nextDt, DateTimeKind.Utc), TimeSpan.Zero);
            }
        }
        //------------------------------------------------------------------------

        //------------------------------------------------------------------------
        /// <summary>
        ///   Handles cancellation: marks the registration cancelled and removes it from the clock.
        /// </summary>
        protected void OnCancelRequested ()
        {
            lock (Gate)
            {
                if (Disposed || State == TimerState.Cancelled)
                    return;
                CancelRequested = true;
                State = TimerState.Cancelled;
                EnabledDayTime = false;
            }

            Clock.RemoveDayTimeTimer(this);
        }
        //------------------------------------------------------------------------

#if NET
        /// <inheritdoc />
        public bool Change (LocalTimeOfDay newTimeOfDay)
        {
            if (!IsLocal)
                return false;
            lock (Gate)
            {
                if (Disposed || State == TimerState.Cancelled)
                    return false;
                TargetTimeOfDay = newTimeOfDay.Value.ToTimeSpan();
                if (Enabled)
                    NextDueUtc = ComputeNextDue(Clock.UtcNowDateTimeOffset);
                return true;
            }
        }

        /// <inheritdoc />
        public bool Change (UtcTimeOfDay newTimeOfDay)
        {
            if (IsLocal)
                return false;
            lock (Gate)
            {
                if (Disposed || State == TimerState.Cancelled)
                    return false;
                TargetTimeOfDay = newTimeOfDay.Value.ToTimeSpan();
                if (Enabled)
                    NextDueUtc = ComputeNextDue(Clock.UtcNowDateTimeOffset);
                return true;
            }
        }
#endif

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
                Enabled = false;
            }

            Clock.RemoveDayTimeTimer(this);
        }
        //------------------------------------------------------------------------

        //------------------------------------------------------------------------
        /// <inheritdoc />
        public bool Stop ()
        {
            lock (Gate)
            {
                if (!EnabledDayTime || State == TimerState.Cancelled || Disposed)
                    return false;
                EnabledDayTime = false;
                State = TimerState.Disabled;
                return true;
            }
        }
        //------------------------------------------------------------------------

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
                EnabledDayTime = true;
                State = TimerState.Active;
                NextDueUtc = ComputeNextDue(Clock.UtcNowDateTimeOffset);
                return true;
            }
        }
        //------------------------------------------------------------------------

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
                EnabledDayTime = false;
            }

            Clock.RemoveDayTimeTimer(this);
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
    ///   Virtual day-time timer that invokes the user callback when the scheduled time-of-day is reached.
    /// </summary>
    private sealed partial class VirtualDayTimeTimer : VirtualDayTimeTimerBase
    {
        //------------------------------------------------------------------------
        /// <summary>
        ///   Initializes a new instance of the <see cref="VirtualDayTimeTimer"/> class.
        /// </summary>
        /// <param name="clock">The owning test clock.</param>
        /// <param name="isLocal"><c>true</c> for local time-of-day; <c>false</c> for UTC.</param>
        /// <param name="targetTimeOfDay">Time of day since midnight in the chosen basis.</param>
        /// <param name="callbackKind">Callback delegate shape.</param>
        /// <param name="callback">The callback delegate.</param>
        /// <param name="callbackState">Optional callback state.</param>
        /// <param name="options">Day-time options, or <c>null</c> for defaults.</param>
        /// <param name="cancellationToken">Token that cancels this registration.</param>
        public VirtualDayTimeTimer (PrimeTestClock clock,
            bool isLocal,
            TimeSpan targetTimeOfDay,
            IntervalTimerCallbackKind callbackKind,
            Delegate callback,
            object? callbackState,
            DayTimeTimerOptions? options,
            CancellationToken cancellationToken)
            : base(clock, isLocal, targetTimeOfDay, callbackKind, callback, callbackState, options, cancellationToken)
        {
        }
        //------------------------------------------------------------------------

        //------------------------------------------------------------------------
        /// <inheritdoc />
        public override void RunDueCallback (DateTimeOffset now)
        {
            lock (Gate)
            {
                if (Disposed || CancelRequested || State == TimerState.Cancelled || !EnabledDayTime)
                    return;
                if (NextDueUtc is not { } next || next > now)
                    return;
                LastCallbackAt = GetOffsetRepresentationForTimer(now);
                NextDueUtc = ComputeNextDue(now);
                State = TimerState.RepeatProcessingCallback;
                CallbacksRunning++;
            }

            try
            {
                RunCallback();
            }
            finally
            {
                lock (Gate)
                {
                    CallbacksRunning--;
                    State = TimerState.RepeatCycle;
                }
            }
        }
        //------------------------------------------------------------------------

        //------------------------------------------------------------------------
        /// <summary>
        ///   Invokes the registered synchronous or async-blocking callback.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        ///   <see cref="VirtualDayTimeTimerBase.CallbackKind"/> is not supported.
        /// </exception>
        private void RunCallback ()
        {
            switch (CallbackKind)
            {
                case IntervalTimerCallbackKind.SimpleAction:
                    ((Action)Callback)();
                    break;
                case IntervalTimerCallbackKind.ContextAction:
                    ((Action<ClockTimerCallbackContext>)Callback)(new ClockTimerCallbackContext(this, CallbackState));
                    break;
                case IntervalTimerCallbackKind.ContextActionWithToken:
                    ((Action<ClockTimerCallbackContext, CancellationToken>)Callback)(new ClockTimerCallbackContext(this, CallbackState),
                        CancellationToken);
                    break;
                case IntervalTimerCallbackKind.SimpleAsync:
                    ((Func<CancellationToken, ValueTask>)Callback)(CancellationToken).AsTask().GetAwaiter().GetResult();
                    break;
                case IntervalTimerCallbackKind.ContextAsync:
                    ((Func<ClockTimerCallbackContext, CancellationToken, ValueTask>)Callback)(new ClockTimerCallbackContext(this, CallbackState),
                        CancellationToken)
                        .AsTask().GetAwaiter().GetResult();
                    break;
                default:
                    throw new InvalidOperationException($"Unsupported callback kind: {CallbackKind}");
            }
        }
        //------------------------------------------------------------------------
    }
    //============================================================================

    #endregion Nested types — Virtual day-time timer
#endif

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
        public Instant RegisteredInstant => Instant.FromDateTimeOffset(RegisteredTime);
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

    //----------------------------------------------------------------------------
    /// <summary>
    ///   Monotonically assigned registration id for virtual timers.
    /// </summary>
    private static int _nextTimerId;

#if NET10_OR_GREATER
    /// <summary>
    ///   Synchronizes virtual time, pending delays, expiries, and timer lists.
    /// </summary>
    private readonly Lock _gate = new();
#else
    /// <summary>
    ///   Synchronizes virtual time, pending delays, expiries, and timer lists.
    /// </summary>
    private readonly object _gate = new();
#endif

    /// <summary>
    ///   Whether <see cref="Start"/> is driving automatic <see cref="Advance"/> on a background thread.
    /// </summary>
    private bool _isRunning;

    /// <summary>
    ///   Background thread used when <see cref="_isRunning"/> is <c>true</c>.
    /// </summary>
    private Thread? _runThread;

    /// <summary>
    ///   Virtual time advanced per real second when running automatically.
    /// </summary>
    private TimeSpan _runRate = TimeSpan.FromSeconds(1);

    /// <summary>
    ///   Pending Sleep and DelayAsync completions ordered by due instant.
    /// </summary>
    private readonly List<PendingDelay> _pendingDelays = [];

    /// <summary>
    ///   Active time-based cancellation entries ordered by expiry instant.
    /// </summary>
    private readonly List<TimeExpiryEntry> _timeExpiryEntries = [];

    /// <summary>
    ///   Active virtual interval timer registrations.
    /// </summary>
    private readonly List<VirtualIntervalTimerBase> _intervalTimers = [];

#if NET || !SYSTEMCLOCK
    /// <summary>
    ///   Active virtual day-time timer registrations.
    /// </summary>
    private readonly List<VirtualDayTimeTimerBase> _dayTimeTimers = [];
#endif


    /// <summary>
    ///   Occurs when the clock's current time has changed.
    /// </summary>
    public event EventHandler<ClockTimeChangedEventArgs>? ClockEvents;
    //----------------------------------------------------------------------------

    //----------------------------------------------------------------------------
    /// <summary>
    ///   Converts a virtual UTC instant to local-offset representation used by local-time APIs.
    /// </summary>
    /// <param name="utcNowOffset">Virtual instant in UTC offset form.</param>
    /// <returns>Same instant in the clock's local coordinate system.</returns>
    private partial DateTimeOffset ToLocalOffset (DateTimeOffset utcNowOffset);
    //----------------------------------------------------------------------------

    //----------------------------------------------------------------------------
    /// <summary>
    ///   Returns the UTC offset for a local wall-clock calendar <see cref="DateTime"/> in the same time zone
    ///   the clock uses for <see cref="LocalNowDateTimeOffset"/>.
    /// </summary>
    /// <param name="localUnspecified">Local date and time with <see cref="DateTime.Kind"/> <see cref="DateTimeKind.Unspecified"/>.</param>
    /// <returns>The offset from UTC at that local wall time.</returns>
    private partial TimeSpan GetLocalWallClockUtcOffset (DateTime localUnspecified);
    //----------------------------------------------------------------------------

    //----------------------------------------------------------------------------
    /// <summary>
    ///   Sets the virtual UTC instant while <see cref="_gate"/> is held.
    /// </summary>
    /// <param name="utcNowOffset">The new virtual UTC time.</param>
    private partial void SetVirtualUtcNowLocked (DateTimeOffset utcNowOffset);
    //----------------------------------------------------------------------------

    //----------------------------------------------------------------------------
    /// <summary>
    ///   Reads the virtual UTC instant while <see cref="_gate"/> is held.
    /// </summary>
    /// <returns>The current virtual UTC time.</returns>
    private partial DateTimeOffset ReadVirtualUtcNowLocked ();
    //----------------------------------------------------------------------------

    //----------------------------------------------------------------------------
    /// <summary>
    ///   Adds virtual elapsed time while <see cref="_gate"/> is held.
    /// </summary>
    /// <param name="duration">Amount of virtual time to add (may be clamped by partial implementations).</param>
    private partial void AddVirtualTimeLocked (TimeSpan duration);
    //----------------------------------------------------------------------------

    //----------------------------------------------------------------------------
    /// <summary>
    ///   Raises <see cref="ClockEvents"/> after the virtual UTC instant changed.
    /// </summary>
    /// <param name="utcNowOffset">The new virtual UTC time to report.</param>
    private partial void RaiseClockEventsAfterVirtualUtcChange (DateTimeOffset utcNowDateTimeOffset);
    //----------------------------------------------------------------------------

    #region Local time-of-day scheduling

    //----------------------------------------------------------------------------
    /// <summary>
    ///   Computes the UTC instant of the next occurrence of <paramref name="targetTimeOfDay"/> on or after
    ///   <paramref name="localNow"/>, using <paramref name="getLocalWallClockUtcOffset"/> to resolve the UTC offset for
    ///   the scheduled local wall-clock instant (including across daylight saving time transitions).
    /// </summary>
    /// <remarks>
    ///   Virtual local day-time timers use <see cref="DayTimeBclLocalWallTimeScheduling"/> (System Clock) or
    ///   DayTimeNodaLocalWallTimeScheduling (superset assembly) instead of this method so skipped/duplicate policies
    ///   match production.
    /// </remarks>
    /// <param name="localNow">
    ///   Current time in the local coordinate system (same interpretation as <see cref="LocalNowDateTimeOffset"/>).
    /// </param>
    /// <param name="targetTimeOfDay">Time of day since local midnight.</param>
    /// <param name="getLocalWallClockUtcOffset">
    ///   Returns the UTC offset for a local wall-clock <see cref="DateTime"/> with kind
    ///   <see cref="DateTimeKind.Unspecified"/>.
    /// </param>
    /// <returns>The next due instant in UTC.</returns>
    internal static DateTimeOffset ComputeNextLocalTimeOfDayAsUtc (DateTimeOffset localNow, TimeSpan targetTimeOfDay,
        Func<DateTime, TimeSpan> getLocalWallClockUtcOffset)
    {
        // Base the next occurrence on the current local calendar date (local wall-clock midnight).
        DateTime todayMidnight = localNow.Date;
        DateTime nextDt = todayMidnight + targetTimeOfDay;
        // If today's target time-of-day is not strictly in the future, schedule the same clock time on the next day.
        if (nextDt <= localNow.DateTime)
            nextDt = nextDt.AddDays(1);
        // Unspecified wall-clock time is interpreted in the clock's local zone, including when that instant is
        // ambiguous (fall-back) or not otherwise representable as a single wall time (spring-forward gap).
        DateTime nextLocalUnspecified = DateTime.SpecifyKind(nextDt, DateTimeKind.Unspecified);
        // Delegate returns the UTC offset for that wall time. Callers that need option-driven DST
        // resolution use DayTimeBclLocalWallTimeScheduling / DayTimeNodaLocalWallTimeScheduling instead.
        TimeSpan nextLocalOffset = getLocalWallClockUtcOffset(nextLocalUnspecified);
        // Wall clock plus resolved offset is fixed; convert to UTC so the scheduled instant is unambiguous downstream.
        DateTimeOffset nextLocal = new(nextLocalUnspecified, nextLocalOffset);
        return nextLocal.ToUniversalTime();
    }
    //----------------------------------------------------------------------------

    #endregion Local time-of-day scheduling

    #region Private helpers

    //----------------------------------------------------------------------------
    /// <summary>
    ///   Background loop: sleeps one real second, then advances virtual time by <see cref="_runRate"/>.
    /// </summary>
    private void RunLoop ()
    {
        while (true)
        {
            Thread.Sleep(1000);
            TimeSpan toAdvance;
            lock (_gate)
            {
                if (!_isRunning)
                    return;
                toAdvance = _runRate;
            }

            Advance(toAdvance);
        }
    }
    //----------------------------------------------------------------------------

    //----------------------------------------------------------------------------
    /// <summary>
    ///   Removes an interval registration from the active list (under <see cref="_gate"/>).
    /// </summary>
    /// <param name="timer">The registration to remove.</param>
    private void RemoveIntervalTimer (VirtualIntervalTimerBase timer)
    {
        lock (_gate)
            _intervalTimers.Remove(timer);
    }
    //----------------------------------------------------------------------------

#if NET || !SYSTEMCLOCK
    //----------------------------------------------------------------------------
    /// <summary>
    ///   Removes a day-time registration from the active list (under <see cref="_gate"/>).
    /// </summary>
    /// <param name="timer">The registration to remove.</param>
    private void RemoveDayTimeTimer (VirtualDayTimeTimerBase timer)
    {
        lock (_gate)
            _dayTimeTimers.Remove(timer);
    }
    //----------------------------------------------------------------------------

    //----------------------------------------------------------------------------
    /// <summary>
    ///   Registers a local time-of-day timer and adds it to <see cref="_dayTimeTimers"/>.
    /// </summary>
    /// <param name="targetTimeOfDay">Time of day since local midnight.</param>
    /// <param name="kind">Callback shape.</param>
    /// <param name="callback">User callback.</param>
    /// <param name="state">Optional state.</param>
    /// <param name="options">Day-time options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The new registration.</returns>
    private IClockDayTimeTimer RegisterTimeOfDayLocal (TimeSpan targetTimeOfDay,
        IntervalTimerCallbackKind kind,
        Delegate callback,
        object? state,
        DayTimeTimerOptions? options,
        CancellationToken cancellationToken)
    {
        VirtualDayTimeTimerBase t = new VirtualDayTimeTimer(this,
            isLocal: true,
            targetTimeOfDay,
            kind,
            callback,
            state,
            options,
            cancellationToken);
        lock (_gate)
            _dayTimeTimers.Add(t);
        return t;
    }
    //----------------------------------------------------------------------------

    //----------------------------------------------------------------------------
    /// <summary>
    ///   Registers a UTC time-of-day timer and adds it to <see cref="_dayTimeTimers"/>.
    /// </summary>
    /// <param name="targetTimeOfDay">Time of day since UTC midnight.</param>
    /// <param name="kind">Callback shape.</param>
    /// <param name="callback">User callback.</param>
    /// <param name="state">Optional state.</param>
    /// <param name="options">Day-time options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The new registration.</returns>
    private IClockDayTimeTimer RegisterTimeOfDayUtc (TimeSpan targetTimeOfDay,
        IntervalTimerCallbackKind kind,
        Delegate callback,
        object? state,
        DayTimeTimerOptions? options,
        CancellationToken cancellationToken)
    {
        VirtualDayTimeTimerBase t = new VirtualDayTimeTimer(this,
            isLocal: false,
            targetTimeOfDay,
            kind,
            callback,
            state,
            options,
            cancellationToken);
        lock (_gate)
            _dayTimeTimers.Add(t);
        return t;
    }
    //----------------------------------------------------------------------------

#endif

    #endregion Private helpers

    #region Interface Implementations

    #region IPrimeTestClock Implementation

    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public void SetTime (DateTimeOffset utcTime)
    {
        lock (_gate)
        {
            SetVirtualUtcNowLocked(utcTime);
        }

        RaiseClockEventsAfterVirtualUtcChange(utcTime);
    }
    //----------------------------------------------------------------------------

    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public void Advance (TimeSpan duration)
    {
        if (duration < TimeSpan.Zero)
            duration = TimeSpan.Zero;

        DateTimeOffset newNow;
        List<PendingDelay>? toComplete = null;
        List<TimeExpiryEntry>? toCancel = null;
        List<VirtualIntervalTimerBase>? intervalDue = null;
#if NET || !SYSTEMCLOCK
        List<VirtualDayTimeTimerBase>? dayTimeDue = null;
#endif

        lock (_gate)
        {
            AddVirtualTimeLocked(duration);
            newNow = ReadVirtualUtcNowLocked();

            foreach (PendingDelay pd in _pendingDelays)
            {
                if (pd.DueUtc <= newNow)
                {
                    toComplete ??= [];
                    toComplete.Add(pd);
                }
            }

            if (toComplete != null)
            {
                foreach (PendingDelay pd in toComplete)
                    _pendingDelays.Remove(pd);
            }

            foreach (TimeExpiryEntry tee in _timeExpiryEntries)
            {
                if (tee.ExpireUtc <= newNow)
                {
                    toCancel ??= [];
                    toCancel.Add(tee);
                }
            }

            if (toCancel != null)
            {
                foreach (TimeExpiryEntry tee in toCancel)
                    _timeExpiryEntries.Remove(tee);
            }

            foreach (VirtualIntervalTimerBase t in _intervalTimers)
            {
                if (t.IsDue(newNow))
                {
                    intervalDue ??= [];
                    intervalDue.Add(t);
                }
            }

#if NET || !SYSTEMCLOCK
            foreach (VirtualDayTimeTimerBase t in _dayTimeTimers)
            {
                if (t.IsDue(newNow))
                {
                    dayTimeDue ??= [];
                    dayTimeDue.Add(t);
                }
            }
#endif
        }

        if (toComplete != null)
        {
            foreach (PendingDelay pd in toComplete)
                pd.Complete();
        }

        if (toCancel != null)
        {
            foreach (TimeExpiryEntry tee in toCancel)
                tee.Cancel();
        }

        while (intervalDue is { Count: > 0 })
        {
            foreach (VirtualIntervalTimerBase t in intervalDue)
                t.RunDueCallback(newNow);

            intervalDue = null;
            lock (_gate)
            {
                foreach (VirtualIntervalTimerBase t in _intervalTimers)
                {
                    if (t.IsDue(newNow))
                    {
                        intervalDue ??= [];
                        intervalDue.Add(t);
                    }
                }
            }
        }

#if NET || !SYSTEMCLOCK
        while (dayTimeDue is { Count: > 0 })
        {
            foreach (VirtualDayTimeTimerBase t in dayTimeDue)
                t.RunDueCallback(newNow);

            dayTimeDue = null;
            lock (_gate)
            {
                foreach (VirtualDayTimeTimerBase t in _dayTimeTimers)
                {
                    if (t.IsDue(newNow))
                    {
                        dayTimeDue ??= [];
                        dayTimeDue.Add(t);
                    }
                }
            }
        }
#endif

        RaiseClockEventsAfterVirtualUtcChange(newNow);
    }
    //----------------------------------------------------------------------------

    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public void RunFor (TimeSpan duration)
    {
        Advance(duration);
    }
    //----------------------------------------------------------------------------

    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public void Start (TimeSpan? rate = null)
    {
        lock (_gate)
        {
            if (_isRunning)
                return;
            _isRunning = true;
            _runRate = rate ?? TimeSpan.FromSeconds(1);
        }

        _runThread = new Thread(RunLoop)
        {
            IsBackground = true
        };
        _runThread.Start();
    }
    //----------------------------------------------------------------------------

    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public bool Stop ()
    {
        lock (_gate)
        {
            if (!_isRunning)
                return false;
            _isRunning = false;
        }

        _runThread?.Join(TimeSpan.FromSeconds(5));
        _runThread = null;
        return true;
    }
    //----------------------------------------------------------------------------

    #endregion IPrimeTestClock Implementation

    #region IPrimeTestTime Implementation

    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public bool IsRunning
    {
        get
        {
            lock (_gate)
                return _isRunning;
        }
    }
    //----------------------------------------------------------------------------

    #endregion IPrimeTestTime Implementation

#if SYSTEMCLOCK
    #region IPrimeClock Implementation — Now

    /// <inheritdoc />
    public DateTimeOffset LocalNowDateTimeOffset
    {
        get
        {
            lock (_gate)
                return ToLocalOffset(ReadVirtualUtcNowLocked());
        }
    }

    /// <inheritdoc />
    public DateTimeOffset UtcNowDateTimeOffset
    {
        get
        {
            lock (_gate)
                return ReadVirtualUtcNowLocked();
        }
    }

    /// <inheritdoc />
    public DateTime LocalNowDateTime
    {
        get
        {
            lock (_gate)
                return ToLocalOffset(ReadVirtualUtcNowLocked()).DateTime;
        }
    }

    /// <inheritdoc />
    public DateTime UtcNowDateTime
    {
        get
        {
            lock (_gate)
                return ReadVirtualUtcNowLocked().UtcDateTime;
        }
    }

#if NET
    /// <inheritdoc />
    public TimeOnly LocalNowTimeOnly
    {
        get
        {
            lock (_gate)
                return TimeOnly.FromDateTime(ToLocalOffset(ReadVirtualUtcNowLocked()).DateTime);
        }
    }

    /// <inheritdoc />
    public TimeOnly UtcNowTimeOnly
    {
        get
        {
            lock (_gate)
                return TimeOnly.FromDateTime(ReadVirtualUtcNowLocked().UtcDateTime);
        }
    }

    /// <inheritdoc />
    public DateOnly LocalNowDateOnly
    {
        get
        {
            lock (_gate)
                return DateOnly.FromDateTime(ToLocalOffset(ReadVirtualUtcNowLocked()).DateTime);
        }
    }

    /// <inheritdoc />
    public DateOnly UtcNowDateOnly
    {
        get
        {
            lock (_gate)
                return DateOnly.FromDateTime(ReadVirtualUtcNowLocked().UtcDateTime);
        }
    }
#endif

    #endregion IPrimeClock Implementation — Now
#endif

    #region IPrimeTime Implementation — Delays

    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public void Sleep (TimeSpan sleepTime)
    {
        if (sleepTime <= TimeSpan.Zero)
            return;

        TaskCompletionSource<bool> taskCompletionSource = new(TaskCreationOptions.RunContinuationsAsynchronously);

        lock (_gate)
        {
            DateTimeOffset dueUtc = ReadVirtualUtcNowLocked() + sleepTime;
            _pendingDelays.Add(new PendingDelay(dueUtc, taskCompletionSource));
        }

        taskCompletionSource.Task.GetAwaiter().GetResult();
    }
    //----------------------------------------------------------------------------

    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public void Sleep (int sleepMilliseconds)
    {
        Sleep(TimeSpan.FromMilliseconds(sleepMilliseconds));
    }
    //----------------------------------------------------------------------------

    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public Task DelayAsync (TimeSpan delayTime)
    {
        return DelayAsync(delayTime, CancellationToken.None);
    }
    //----------------------------------------------------------------------------

    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public Task DelayAsync (int millisecondsDelay)
    {
        return DelayAsync(millisecondsDelay, CancellationToken.None);
    }
    //----------------------------------------------------------------------------

    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public Task DelayAsync (TimeSpan delayTime, CancellationToken cancellationToken)
    {
        if (delayTime <= TimeSpan.Zero)
            return Task.CompletedTask;

        TaskCompletionSource<bool> taskCompletionSource = new(TaskCreationOptions.RunContinuationsAsynchronously);

        lock (_gate)
        {
            DateTimeOffset dueUtc = ReadVirtualUtcNowLocked() + delayTime;
            _pendingDelays.Add(new PendingDelay(dueUtc, taskCompletionSource));
        }

        if (cancellationToken.CanBeCanceled)
        {
            CancellationTokenRegistration cancellationRegistration = cancellationToken.Register(() =>
            {
                lock (_gate)
                {
                    if (_pendingDelays.RemoveAll(pendingDelay => pendingDelay.TaskCompletionSource == taskCompletionSource) > 0)
                        taskCompletionSource.TrySetCanceled(cancellationToken);
                }
            });

            _ = taskCompletionSource.Task.ContinueWith((_, state) => ((CancellationTokenRegistration)state!).Dispose(),
                cancellationRegistration,
                CancellationToken.None,
                TaskContinuationOptions.None,
                TaskScheduler.Default);
        }

        return taskCompletionSource.Task;
    }
    //----------------------------------------------------------------------------

    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public Task DelayAsync (int millisecondsDelay, CancellationToken cancellationToken)
    {
        return DelayAsync(TimeSpan.FromMilliseconds(millisecondsDelay), cancellationToken);
    }
    //----------------------------------------------------------------------------

    #endregion IPrimeTime Implementation — Delays

    #region IPrimeTime Implementation — Time cancellation

    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public TimeCancellationTokenSource GetTimeCancellationToken (TimeSpan cancelTime)
    {
        CancellationTokenSource cts = new();
        TimeCancellationTokenSource wrapper = new(cts);

        lock (_gate)
        {
            DateTimeOffset expireUtc = ReadVirtualUtcNowLocked() + cancelTime;
            _timeExpiryEntries.Add(new TimeExpiryEntry(expireUtc, wrapper));
        }

        return wrapper;
    }
    //----------------------------------------------------------------------------

    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public TimeCancellationTokenSource GetTimeCancellationToken (int cancelMilliseconds)
    {
        return GetTimeCancellationToken(TimeSpan.FromMilliseconds(cancelMilliseconds));
    }
    //----------------------------------------------------------------------------

    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public TimeCancellationTokenSource LinkTimeCancellationToken (int cancelMilliseconds,
        CancellationToken token1,
        CancellationToken token2)
    {
        CancellationTokenSource timeCts = new();
        CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(timeCts.Token, token1, token2);
        TimeCancellationTokenSource wrapper = new(linked, [timeCts]);

        lock (_gate)
        {
            DateTimeOffset expireUtc = ReadVirtualUtcNowLocked() + TimeSpan.FromMilliseconds(cancelMilliseconds);
            _timeExpiryEntries.Add(new TimeExpiryEntry(expireUtc, wrapper, timeCts));
        }

        return wrapper;
    }
    //----------------------------------------------------------------------------

    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public TimeCancellationTokenSource LinkTimeCancellationToken (TimeSpan cancelTime,
        CancellationToken token1,
        CancellationToken token2)
    {
        CancellationTokenSource timeCts = new();
        CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(timeCts.Token, token1, token2);
        TimeCancellationTokenSource wrapper = new(linked, [timeCts]);

        lock (_gate)
        {
            DateTimeOffset expireUtc = ReadVirtualUtcNowLocked() + cancelTime;
            _timeExpiryEntries.Add(new TimeExpiryEntry(expireUtc, wrapper, timeCts));
        }

        return wrapper;
    }
    //----------------------------------------------------------------------------

    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public TimeCancellationTokenSource LinkTimeCancellationToken (TimeSpan cancelTime, CancellationToken cancellationToken)
    {
        CancellationTokenSource timeCts = new();
        CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(timeCts.Token, cancellationToken);
        TimeCancellationTokenSource wrapper = new(linked, [timeCts]);

        lock (_gate)
        {
            DateTimeOffset expireUtc = ReadVirtualUtcNowLocked() + cancelTime;
            _timeExpiryEntries.Add(new TimeExpiryEntry(expireUtc, wrapper, timeCts));
        }

        return wrapper;
    }
    //----------------------------------------------------------------------------

    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public TimeCancellationTokenSource LinkTimeCancellationToken (int cancelMilliseconds, CancellationToken cancellationToken)
    {
        return LinkTimeCancellationToken(TimeSpan.FromMilliseconds(cancelMilliseconds), cancellationToken);
    }
    //----------------------------------------------------------------------------

    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public TimeCancellationTokenSource LinkTimeCancellationToken (TimeSpan cancelTime,
        params CancellationToken[] cancellationTokens)
    {
        CancellationTokenSource timeCts = new();
        CancellationToken[] all = new CancellationToken[cancellationTokens.Length + 1];
        all[0] = timeCts.Token;
        Array.Copy(cancellationTokens, 0, all, 1, cancellationTokens.Length);
        CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(all);
        TimeCancellationTokenSource wrapper = new(linked, [timeCts]);

        lock (_gate)
        {
            DateTimeOffset expireUtc = ReadVirtualUtcNowLocked() + cancelTime;
            _timeExpiryEntries.Add(new TimeExpiryEntry(expireUtc, wrapper, timeCts));
        }

        return wrapper;
    }
    //----------------------------------------------------------------------------

    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public TimeCancellationTokenSource LinkTimeCancellationToken (int cancelMilliseconds,
        params CancellationToken[] cancellationTokens)
    {
        return LinkTimeCancellationToken(TimeSpan.FromMilliseconds(cancelMilliseconds), cancellationTokens);
    }
    //----------------------------------------------------------------------------

    #endregion IPrimeTime Implementation — Time cancellation

    #region IPrimeClock Implementation — Interval timers

    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public IClockIntervalTimer RegisterTimer (TimeSpan callbackTime,
        TimeSpan repeatInterval,
        Action<ClockTimerCallbackContext> callback,
        CancellationToken cancellationToken,
        object? state = null,
        IntervalTimerOptions? timerOptions = null)
    {
        VirtualIntervalTimerBase intervalTimer = new VirtualIntervalTimer(this,
            callbackTime,
            repeatInterval,
            IntervalTimerCallbackKind.ContextAction,
            callback,
            state,
            timerOptions,
            cancellationToken);
        lock (_gate)
            _intervalTimers.Add(intervalTimer);
        return intervalTimer;
    }
    //----------------------------------------------------------------------------

    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public IClockIntervalTimer RegisterAsyncTimer (TimeSpan callbackTime,
        TimeSpan repeatInterval,
        Func<ClockTimerCallbackContext, CancellationToken, ValueTask> callback,
        CancellationToken cancellationToken,
        object? state = null,
        IntervalTimerOptions? timerOptions = null)
    {
        VirtualIntervalTimerBase intervalTimer = new VirtualIntervalTimer(this,
            callbackTime,
            repeatInterval,
            IntervalTimerCallbackKind.ContextAsync,
            callback,
            state,
            timerOptions,
            cancellationToken);
        lock (_gate)
            _intervalTimers.Add(intervalTimer);
        return intervalTimer;
    }
    //----------------------------------------------------------------------------

    #endregion IPrimeClock Implementation — Interval timers

#if NET
    #region IPrimeClock Implementation — Day-time timers

    /// <inheritdoc />
    public IClockDayTimeTimer RegisterTimeOfDay (LocalTimeOfDay timeOfDay,
        Action<ClockTimerCallbackContext> callback,
        CancellationToken cancellationToken,
        object? state = null,
        DayTimeTimerOptions? timerOptions = null) =>
        RegisterTimeOfDayLocal(timeOfDay.Value.ToTimeSpan(), IntervalTimerCallbackKind.ContextAction, callback, state, timerOptions, cancellationToken);

    /// <inheritdoc />
    public IClockDayTimeTimer RegisterAsyncTimeOfDay (LocalTimeOfDay timeOfDay,
        Func<ClockTimerCallbackContext, CancellationToken, ValueTask> callback,
        CancellationToken cancellationToken,
        object? state = null,
        DayTimeTimerOptions? timerOptions = null) =>
        RegisterTimeOfDayLocal(timeOfDay.Value.ToTimeSpan(), IntervalTimerCallbackKind.ContextAsync, callback, state, timerOptions, cancellationToken);

    /// <inheritdoc />
    public IClockDayTimeTimer RegisterTimeOfDay (UtcTimeOfDay timeOfDay,
        Action<ClockTimerCallbackContext> callback,
        CancellationToken cancellationToken,
        object? state = null,
        DayTimeTimerOptions? timerOptions = null) =>
        RegisterTimeOfDayUtc(timeOfDay.Value.ToTimeSpan(), IntervalTimerCallbackKind.ContextAction, callback, state, timerOptions, cancellationToken);

    /// <inheritdoc />
    public IClockDayTimeTimer RegisterAsyncTimeOfDay (UtcTimeOfDay timeOfDay,
        Func<ClockTimerCallbackContext, CancellationToken, ValueTask> callback,
        CancellationToken cancellationToken,
        object? state = null,
        DayTimeTimerOptions? timerOptions = null) =>
        RegisterTimeOfDayUtc(timeOfDay.Value.ToTimeSpan(), IntervalTimerCallbackKind.ContextAsync, callback, state, timerOptions, cancellationToken);

    #endregion IPrimeClock Implementation — Day-time timers
#endif
    #endregion Interface Implementations
}
//################################################################################


