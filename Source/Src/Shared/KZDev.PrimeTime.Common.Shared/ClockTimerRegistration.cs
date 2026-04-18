// Copyright (c) Kevin Zehrer
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Diagnostics;

using Timer = System.Threading.Timer;

#if SYSTEMCLOCK
namespace KZDev.SystemClock.PrimeTime;
#else
namespace KZDev.PrimeTime;
#endif

//################################################################################
/// <summary>
///   Base class implementation for clock timer registrations. 
///   This is used as the base class for both time-of-day and time-interval timer registrations, 
///   and provides the common implementation of the <see cref="IClockTimer"/> interface for both types of timer registrations.
/// </summary>
internal abstract partial class ClockTimerRegistration : IClockTimer
{
    /// <summary>
    ///   Monotonic sequence source for registration identifiers.
    /// </summary>
    private static int _nextRegistrationIdentifier;

    /// <summary>
    ///   Registration for <see cref="CancellationToken"/> cancellation.
    /// </summary>
    private CancellationTokenRegistration _cancelRegistration;

#if NET10_OR_GREATER
    /// <summary>
    ///   Protects mutable registration and timer fields.
    /// </summary>
    internal Lock Gate { [DebuggerStepThrough] get; } = new();
#else
    /// <summary>
    ///   Protects mutable registration and timer fields.
    /// </summary>
    internal object Gate { [DebuggerStepThrough] get; } = new();
#endif

    /// <summary>
    ///   Clock used for scheduling and reading "now".
    /// </summary>
    internal IPrimeClock Clock { [DebuggerStepThrough] get; [DebuggerStepThrough] private set; } = null!;

    /// <summary>
    ///   When <c>true</c>, callbacks capture execution context.
    /// </summary>
    internal bool CaptureContext { [DebuggerStepThrough] get; [DebuggerStepThrough] private set; }

    /// <summary>
    ///   External cancellation token for this registration.
    /// </summary>
    internal CancellationToken CancellationToken { [DebuggerStepThrough] get; [DebuggerStepThrough] private set; }

    /// <summary>
    ///   Indicates whether the configured time of day is interpreted in UTC or local zone per day.
    /// </summary>
    internal bool UtcTimeOfDaySchedule { [DebuggerStepThrough] get; [DebuggerStepThrough] private set; }

    /// <summary>
    ///   Shape of the user callback delegate.
    /// </summary>
    internal TimerCallbackKind CallbackKind { [DebuggerStepThrough] get; [DebuggerStepThrough] private set; }

    /// <summary>
    ///   User callback delegate.
    /// </summary>
    internal Delegate Callback { [DebuggerStepThrough] get; [DebuggerStepThrough] private set; } = null!;

    /// <summary>
    ///   Optional state for context callbacks.
    /// </summary>
    internal object? CallbackState { [DebuggerStepThrough] get; [DebuggerStepThrough] private set; }

    /// <summary>
    ///   When <c>false</c>, no further callbacks are scheduled.
    /// </summary>
    internal bool InternalEnabled { [DebuggerStepThrough] get; [DebuggerStepThrough] set; } = true;

    /// <summary>
    ///   When <c>true</c>, this registration has been disposed.
    /// </summary>
    internal bool Disposed { [DebuggerStepThrough] get; [DebuggerStepThrough] set; }

    /// <summary>
    ///   When <c>true</c>, external cancellation was requested.
    /// </summary>
    internal bool CancelRequested { [DebuggerStepThrough] get; [DebuggerStepThrough] set; }

    /// <summary>
    ///   Underlying BCL one-shot timer used between callbacks.
    /// </summary>
    internal Timer? Timer { [DebuggerStepThrough] get; [DebuggerStepThrough] set; }

    /// <summary>
    ///   Number of callbacks currently executing.
    /// </summary>
    internal int CallbacksRunning { [DebuggerStepThrough] get; [DebuggerStepThrough] set; }

    //----------------------------------------------------------------------------
    /// <summary>
    ///   Captures <see cref="RegisteredTime"/> from the clock per local/UTC option (partial).
    /// </summary>
    private partial void CaptureRegisteredTime ();
    //----------------------------------------------------------------------------
    /// <summary>
    ///   Returns <see cref="RegisteredTime"/> in the registration time basis (partial).
    /// </summary>
    private partial DateTimeOffset GetRegisteredTimeOffset ();
    //----------------------------------------------------------------------------
    /// <summary>
    ///   Cancels the registration and disarms the BCL timer.
    /// </summary>
    private void OnCancelRequested ()
    {
        lock (Gate)
        {
            if (Disposed || State == TimerState.Cancelled)
                return;
            Timer?.Change(Timeout.Infinite, Timeout.Infinite);
            CancelRequested = true;
            State = TimerState.Cancelled;
            InternalEnabled = false;
        }
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
    ///   The caller must hold <see cref="Gate"/>.
    /// </remarks>
    internal bool ShouldSkipTimerWorkWhileLocked () => Disposed || CancelRequested || State == TimerState.Cancelled || !InternalEnabled;
    //----------------------------------------------------------------------------
    /// <summary>
    ///   Completes initialization shared by stack-specific constructors.
    /// </summary>
    /// <param name="clock">The clock used for scheduling and reading "now".</param>
    /// <param name="captureContext">
    ///  When <c>true</c>, callbacks capture execution context; otherwise, they do not.
    /// </param>
    /// <param name="callback">
    ///   The delegate to invoke on each tick.
    /// </param>
    /// <param name="utcTimeOfDaySchedule">
    ///   <c>true</c> for UTC calendar-day scheduling; <c>false</c> for local zone days.
    /// </param>
    /// <param name="callbackKind">The kind of callback delegate to invoke.</param>
    /// <param name="callbackState">
    ///   Optional state passed to the callback via <see cref="ClockTimerCallbackContext"/>.
    /// </param>
    /// <param name="cancellationToken">Token that cancels the registration.</param>
    internal virtual void FinishConstruction (IPrimeClock clock, bool captureContext,
        bool utcTimeOfDaySchedule, TimerCallbackKind callbackKind, Delegate callback, 
        object? callbackState, CancellationToken cancellationToken)
    {
        Clock = clock;
        CallbackKind = callbackKind;
        CallbackState = callbackState;
        Callback = callback;
        CaptureContext = captureContext;
        UtcTimeOfDaySchedule = utcTimeOfDaySchedule;
        CancellationToken = cancellationToken;

        Id = Interlocked.Increment(ref _nextRegistrationIdentifier);
        CaptureRegisteredTime();

        if (cancellationToken.CanBeCanceled)
        {
            _cancelRegistration = cancellationToken.Register(static @this => ((ClockTimerRegistration)@this!).OnCancelRequested(), this);
            if (cancellationToken.IsCancellationRequested)
            {
                CancelRequested = true;
                State = TimerState.Cancelled;
                return;
            }
        }

        State = TimerState.Active;
    }
    //----------------------------------------------------------------------------

    #region Implementation of IClockTimer

    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public abstract bool IsTimeOfDay { get; }
    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public abstract bool IsRepeating { get; }
    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public abstract bool Start ();
    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public int Id { get; private set; }
    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public DateTimeOffset RegisteredTime { [DebuggerStepThrough] get => GetRegisteredTimeOffset(); }
    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public bool IsCancelled => State == TimerState.Cancelled;
    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public bool IsLocalTimeRepresentation { [DebuggerStepThrough] get => !UtcTimeOfDaySchedule; }
    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public TimerState State { [DebuggerStepThrough] get; [DebuggerStepThrough] internal set; }
    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public bool IsActive
    {
        get
        {
            lock (Gate)
            {
                // Capture local state value
                TimerState state = State;
                return state != TimerState.Cancelled &&
                       state != TimerState.Completed &&
                       state != TimerState.Disposed;
            }
        }
    }
    //----------------------------------------------------------------------------
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
    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public bool Enabled
    {
        get
        {
            lock (Gate)
            {
                return InternalEnabled && State != TimerState.Cancelled && State != TimerState.Disposed;
            }
        }
        set
        {
            lock (Gate)
            {
                if (Disposed)
                    throw new ObjectDisposedException(nameof(IClockIntervalTimer));

                if (State == TimerState.Cancelled)
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
    public void Cancel ()
    {
        lock (Gate)
        {
            if (State == TimerState.Cancelled || Disposed)
                return;
            CancelRequested = true;
            State = TimerState.Cancelled;
            InternalEnabled = false;
            Timer?.Change(Timeout.Infinite, Timeout.Infinite);
        }
    }
    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public bool Stop ()
    {
        lock (Gate)
        {
            if (!InternalEnabled || State == TimerState.Cancelled || Disposed)
                return false;
            Timer?.Change(Timeout.Infinite, Timeout.Infinite);
            InternalEnabled = false;
            State = TimerState.Disabled;
            return true;
        }
    }
    //----------------------------------------------------------------------------

    #endregion

    #region Implementation of IDisposable

    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public void Dispose ()
    {
        Timer? timerToDispose;
        lock (Gate)
        {
            if (Disposed)
                return;
            Disposed = true;
            State = TimerState.Disposed;
            InternalEnabled = false;
            timerToDispose = Timer;
            Timer = null;
        }
        timerToDispose?.Dispose();
        _cancelRegistration.Dispose();
    }
    //----------------------------------------------------------------------------

    #endregion

    #region IAsyncDisposable Implementation

    //----------------------------------------------------------------------------
    /// <inheritdoc />
    /// <remarks>
    ///   We don't have any asynchronous cleanup to do, but for convenience we implement 
    ///   <see cref="IAsyncDisposable"/> so that callers can use async disposal patterns 
    ///   without needing to check for support for async disposal on the registration type.
    /// </remarks>
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
}
//################################################################################
