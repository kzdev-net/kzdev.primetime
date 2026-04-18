// Copyright (c) Kevin Zehrer. All rights reserved.
// This file is part of the PrimeTime project.

#if SYSTEMCLOCK
namespace KZDev.SystemClock.PrimeTime;
#else
namespace KZDev.PrimeTime;
#endif

#if SYSTEMCLOCK
/// <content>
///   BCL <see cref="IPrimeTime"/> and timer registration members for <see cref="PrimeClock"/>
///   in <c>KZDev.SystemClock.PrimeTime</c> (TimeProvider-based).
/// </content>
#else
//################################################################################
/// <content>
///   BCL <see cref="IPrimeTime"/> and timer registration members for <see cref="PrimeClock"/>
///   in <c>KZDev.PrimeTime</c> (NodaTime-based).
/// </content>
#endif
internal sealed partial class PrimeClock : PrimeTimeBase, IPrimeClock
{
    #region Interface Implementations

    #region IPrimeClock — Interval timers

    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public IClockIntervalTimer RegisterTimer (TimeSpan callbackTime,
        TimeSpan repeatInterval,
        Action<ClockTimerCallbackContext> callback,
        CancellationToken cancellationToken,
        object? state = null,
        IntervalTimerOptions? timerOptions = null) =>
        new ClockIntervalTimerRegistration(this,
            callbackTime,
            repeatInterval,
            TimerCallbackKind.ContextAction,
            callback,
            state,
            timerOptions,
            cancellationToken);
    //----------------------------------------------------------------------------

    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public IClockIntervalTimer RegisterAsyncTimer (TimeSpan callbackTime,
        TimeSpan repeatInterval,
        Func<ClockTimerCallbackContext, CancellationToken, ValueTask> callback,
        CancellationToken cancellationToken,
        object? state = null,
        IntervalTimerOptions? timerOptions = null) =>
        new ClockIntervalTimerRegistration(this,
            callbackTime,
            repeatInterval,
            TimerCallbackKind.ContextAsync,
            callback,
            state,
            timerOptions,
            cancellationToken);
    //----------------------------------------------------------------------------

    #endregion IPrimeClock — Interval timers

#if NET && SYSTEMCLOCK
    #region IPrimeClock — Day-time timers

    /// <inheritdoc />
    public IClockDayTimeTimer RegisterTimeOfDay (LocalTimeOfDay timeOfDay,
        Action<ClockTimerCallbackContext> callback,
        CancellationToken cancellationToken,
        object? state = null,
        DayTimeTimerOptions? timerOptions = null) =>
        new ClockDayTimeTimerRegistration(this,
            timeOfDay,
            TimerCallbackKind.ContextAction,
            callback,
            state,
            timerOptions,
            cancellationToken);

    /// <inheritdoc />
    public IClockDayTimeTimer RegisterAsyncTimeOfDay (LocalTimeOfDay timeOfDay,
        Func<ClockTimerCallbackContext, CancellationToken, ValueTask> callback,
        CancellationToken cancellationToken,
        object? state = null,
        DayTimeTimerOptions? timerOptions = null) =>
        new ClockDayTimeTimerRegistration(this,
            timeOfDay,
            TimerCallbackKind.ContextAsync,
            callback,
            state,
            timerOptions,
            cancellationToken);

    /// <inheritdoc />
    public IClockDayTimeTimer RegisterTimeOfDay (UtcTimeOfDay timeOfDay,
        Action<ClockTimerCallbackContext> callback,
        CancellationToken cancellationToken,
        object? state = null,
        DayTimeTimerOptions? timerOptions = null) =>
        new ClockDayTimeTimerRegistration(this,
            timeOfDay,
            TimerCallbackKind.ContextAction,
            callback,
            state,
            timerOptions,
            cancellationToken);

    /// <inheritdoc />
    public IClockDayTimeTimer RegisterAsyncTimeOfDay (UtcTimeOfDay timeOfDay,
        Func<ClockTimerCallbackContext, CancellationToken, ValueTask> callback,
        CancellationToken cancellationToken,
        object? state = null,
        DayTimeTimerOptions? timerOptions = null) =>
        new ClockDayTimeTimerRegistration(this,
            timeOfDay,
            TimerCallbackKind.ContextAsync,
            callback,
            state,
            timerOptions,
            cancellationToken);

    #endregion IPrimeClock — Day-time timers
#endif

    #endregion Interface Implementations
}
//################################################################################
