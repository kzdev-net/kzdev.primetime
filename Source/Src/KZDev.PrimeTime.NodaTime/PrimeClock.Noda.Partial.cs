// Copyright (c) Kevin Zehrer. All rights reserved.
// This file is part of the PrimeTime project.

using NodaTime;

namespace KZDev.PrimeTime;

/// <content>
///   NodaTime-specific partial of <see cref="PrimeClock"/>: BCL <see cref="IPrimeTime"/> forwards,
///   <see cref="TimeSpan"/> timer overloads, and day-time registration using
///   <see cref="PrimeClockDayTimeTimerRegistration"/>. Lives in <c>KZDev.PrimeTime.NodaTime</c>
///   only (not Common.Shared), so shared sources stay BCL-only aside from namespace conditionals.
/// </content>
internal sealed partial class PrimeClock
{
    #region Interface Implementations

    #region IPrimeTime Implementation

    /// <inheritdoc />
    public void Sleep (TimeSpan sleepTime) => Sleep(Duration.FromTimeSpan(sleepTime));

    /// <inheritdoc />
    public void Sleep (int sleepMilliseconds) => Sleep(Duration.FromMilliseconds(sleepMilliseconds));

    /// <inheritdoc />
    public Task DelayAsync (TimeSpan delayTime) => DelayAsync(Duration.FromTimeSpan(delayTime));

    /// <inheritdoc />
    public Task DelayAsync (int millisecondsDelay) => DelayAsync(Duration.FromMilliseconds(millisecondsDelay));

    /// <inheritdoc />
    public Task DelayAsync (TimeSpan delayTime, CancellationToken cancellationToken) =>
        DelayAsync(Duration.FromTimeSpan(delayTime), cancellationToken);

    /// <inheritdoc />
    public Task DelayAsync (int millisecondsDelay, CancellationToken cancellationToken) =>
        DelayAsync(Duration.FromMilliseconds(millisecondsDelay), cancellationToken);

    /// <inheritdoc />
    public TimeCancellationTokenSource GetTimeCancellationToken (TimeSpan cancelTime) =>
        GetTimeCancellationToken(Duration.FromTimeSpan(cancelTime));

    /// <inheritdoc />
    public TimeCancellationTokenSource GetTimeCancellationToken (int cancelMilliseconds) =>
        GetTimeCancellationToken(Duration.FromMilliseconds(cancelMilliseconds));

    /// <inheritdoc />
    public TimeCancellationTokenSource LinkTimeCancellationToken (TimeSpan cancelTime, CancellationToken cancellationToken) =>
        LinkTimeCancellationToken(Duration.FromTimeSpan(cancelTime), cancellationToken);

    /// <inheritdoc />
    public TimeCancellationTokenSource LinkTimeCancellationToken (int cancelMilliseconds, CancellationToken cancellationToken) =>
        LinkTimeCancellationToken(Duration.FromMilliseconds(cancelMilliseconds), cancellationToken);

    /// <inheritdoc />
    public TimeCancellationTokenSource LinkTimeCancellationToken (int cancelMilliseconds, CancellationToken token1,
        CancellationToken token2) =>
        LinkTimeCancellationToken(Duration.FromMilliseconds(cancelMilliseconds), token1, token2);

    /// <inheritdoc />
    public TimeCancellationTokenSource LinkTimeCancellationToken (TimeSpan cancelTime, CancellationToken token1,
        CancellationToken token2) =>
        LinkTimeCancellationToken(Duration.FromTimeSpan(cancelTime), token1, token2);

    /// <inheritdoc />
    public TimeCancellationTokenSource LinkTimeCancellationToken (TimeSpan cancelTime,
        params CancellationToken[] cancellationTokens) =>
        LinkTimeCancellationToken(Duration.FromTimeSpan(cancelTime), cancellationTokens);

    /// <inheritdoc />
    public TimeCancellationTokenSource LinkTimeCancellationToken (int cancelMilliseconds,
        params CancellationToken[] cancellationTokens) =>
        LinkTimeCancellationToken(Duration.FromMilliseconds(cancelMilliseconds), cancellationTokens);

    #endregion IPrimeTime Implementation

    #region IPrimeClock — Interval timers (BCL TimeSpan overloads)

    /// <inheritdoc />
    public IClockIntervalTimer RegisterTimer (TimeSpan callbackTime,
        Action callback,
        CancellationToken cancellationToken,
        bool repeat = false,
        IntervalTimerOptions? timerOptions = null) =>
        RegisterTimer(Duration.FromTimeSpan(callbackTime), callback, cancellationToken, repeat, timerOptions);

    /// <inheritdoc />
    public IClockIntervalTimer RegisterTimer (TimeSpan callbackTime,
        Action<ClockTimerCallbackContext> callback,
        CancellationToken cancellationToken,
        object? state = null,
        bool repeat = false,
        IntervalTimerOptions? timerOptions = null) =>
        RegisterTimer(Duration.FromTimeSpan(callbackTime), callback, cancellationToken, state, repeat, timerOptions);

    /// <inheritdoc />
    public IClockIntervalTimer RegisterTimer (TimeSpan callbackTime,
        Action<ClockTimerCallbackContext, CancellationToken> callback,
        CancellationToken cancellationToken,
        object? state = null,
        bool repeat = false,
        IntervalTimerOptions? timerOptions = null) =>
        RegisterTimer(Duration.FromTimeSpan(callbackTime), callback, cancellationToken, state, repeat, timerOptions);

    /// <inheritdoc />
    public IClockIntervalTimer RegisterAsyncTimer (TimeSpan callbackTime,
        Func<CancellationToken, ValueTask> callback,
        CancellationToken cancellationToken,
        bool repeat = false,
        IntervalTimerOptions? timerOptions = null) =>
        RegisterAsyncTimer(Duration.FromTimeSpan(callbackTime), callback, cancellationToken, repeat, timerOptions);

    /// <inheritdoc />
    public IClockIntervalTimer RegisterAsyncTimer (TimeSpan callbackTime,
        Func<ClockTimerCallbackContext, CancellationToken, ValueTask> callback,
        CancellationToken cancellationToken,
        object? state = null,
        bool repeat = false,
        IntervalTimerOptions? timerOptions = null) =>
        RegisterAsyncTimer(Duration.FromTimeSpan(callbackTime), callback, cancellationToken, state, repeat, timerOptions);

    /// <inheritdoc />
    public IClockIntervalTimer RegisterTimer (TimeSpan callbackTime,
        TimeSpan repeatInterval,
        Action callback,
        CancellationToken cancellationToken,
        IntervalTimerOptions? timerOptions = null) =>
        RegisterTimer(Duration.FromTimeSpan(callbackTime),
            Duration.FromTimeSpan(repeatInterval),
            callback,
            cancellationToken,
            timerOptions);

    /// <inheritdoc />
    public IClockIntervalTimer RegisterTimer (TimeSpan callbackTime,
        TimeSpan repeatInterval,
        Action<ClockTimerCallbackContext> callback,
        CancellationToken cancellationToken,
        object? state = null,
        IntervalTimerOptions? timerOptions = null) =>
        RegisterTimer(Duration.FromTimeSpan(callbackTime),
            Duration.FromTimeSpan(repeatInterval),
            callback,
            cancellationToken,
            state,
            timerOptions);

    /// <inheritdoc />
    public IClockIntervalTimer RegisterTimer (TimeSpan callbackTime,
        TimeSpan repeatInterval,
        Action<ClockTimerCallbackContext, CancellationToken> callback,
        CancellationToken cancellationToken,
        object? state = null,
        IntervalTimerOptions? timerOptions = null) =>
        RegisterTimer(Duration.FromTimeSpan(callbackTime),
            Duration.FromTimeSpan(repeatInterval),
            callback,
            cancellationToken,
            state,
            timerOptions);

    /// <inheritdoc />
    public IClockIntervalTimer RegisterAsyncTimer (TimeSpan callbackTime,
        TimeSpan repeatInterval,
        Func<CancellationToken, ValueTask> callback,
        CancellationToken cancellationToken,
        IntervalTimerOptions? timerOptions = null) =>
        RegisterAsyncTimer(Duration.FromTimeSpan(callbackTime),
            Duration.FromTimeSpan(repeatInterval),
            callback,
            cancellationToken,
            timerOptions);

    /// <inheritdoc />
    public IClockIntervalTimer RegisterAsyncTimer (TimeSpan callbackTime,
        TimeSpan repeatInterval,
        Func<ClockTimerCallbackContext, CancellationToken, ValueTask> callback,
        CancellationToken cancellationToken,
        object? state = null,
        IntervalTimerOptions? timerOptions = null) =>
        RegisterAsyncTimer(Duration.FromTimeSpan(callbackTime),
            Duration.FromTimeSpan(repeatInterval),
            callback,
            cancellationToken,
            state,
            timerOptions);

    #endregion IPrimeClock — Interval timers (BCL TimeSpan overloads)

    #region IPrimeClock — Time-of-day timers (RegisterTimeOfDay)

#if NET
    /// <summary>
    ///   Converts a <see cref="TimeOnly"/> (wall-clock) to <see cref="LocalTime"/> for Noda day-time scheduling.
    /// </summary>
    private static LocalTime TimeOnlyToLocalTime (TimeOnly t) =>
        new(t.Hour, t.Minute, t.Second, t.Millisecond);

    /// <inheritdoc />
    public IClockDayTimeTimer RegisterTimeOfDay (LocalTime timeOfDay,
        Action callback,
        CancellationToken cancellationToken,
        DayTimeTimerOptions? timerOptions = null) =>
        new PrimeClockDayTimeTimerRegistration(this,
            timeOfDay,
            IntervalTimerCallbackKind.SimpleAction,
            callback,
            null,
            timerOptions,
            cancellationToken);

    /// <inheritdoc />
    public IClockDayTimeTimer RegisterTimeOfDay (LocalTime timeOfDay,
        Action<ClockTimerCallbackContext> callback,
        CancellationToken cancellationToken,
        object? state = null,
        DayTimeTimerOptions? timerOptions = null) =>
        new PrimeClockDayTimeTimerRegistration(this,
            timeOfDay,
            IntervalTimerCallbackKind.ContextAction,
            callback,
            state,
            timerOptions,
            cancellationToken);

    /// <inheritdoc />
    public IClockDayTimeTimer RegisterTimeOfDay (LocalTime timeOfDay,
        Action<ClockTimerCallbackContext, CancellationToken> callback,
        CancellationToken cancellationToken,
        object? state = null,
        DayTimeTimerOptions? timerOptions = null) =>
        new PrimeClockDayTimeTimerRegistration(this,
            timeOfDay,
            IntervalTimerCallbackKind.ContextActionWithToken,
            callback,
            state,
            timerOptions,
            cancellationToken);

    /// <inheritdoc />
    public IClockDayTimeTimer RegisterAsyncTimeOfDay (LocalTime timeOfDay,
        Func<CancellationToken, ValueTask> callback,
        CancellationToken cancellationToken,
        DayTimeTimerOptions? timerOptions = null) =>
        new PrimeClockDayTimeTimerRegistration(this,
            timeOfDay,
            IntervalTimerCallbackKind.SimpleAsync,
            callback,
            null,
            timerOptions,
            cancellationToken);

    /// <inheritdoc />
    public IClockDayTimeTimer RegisterAsyncTimeOfDay (LocalTime timeOfDay,
        Func<ClockTimerCallbackContext, CancellationToken, ValueTask> callback,
        CancellationToken cancellationToken,
        object? state = null,
        DayTimeTimerOptions? timerOptions = null) =>
        new PrimeClockDayTimeTimerRegistration(this,
            timeOfDay,
            IntervalTimerCallbackKind.ContextAsync,
            callback,
            state,
            timerOptions,
            cancellationToken);

    /// <inheritdoc />
    public IClockDayTimeTimer RegisterTimeOfDay (LocalTimeOfDay timeOfDay,
        Action callback,
        CancellationToken cancellationToken,
        DayTimeTimerOptions? timerOptions = null) =>
        RegisterTimeOfDay(TimeOnlyToLocalTime(timeOfDay.Value), callback, cancellationToken, timerOptions);

    /// <inheritdoc />
    public IClockDayTimeTimer RegisterTimeOfDay (LocalTimeOfDay timeOfDay,
        Action<ClockTimerCallbackContext> callback,
        CancellationToken cancellationToken,
        object? state = null,
        DayTimeTimerOptions? timerOptions = null) =>
        RegisterTimeOfDay(TimeOnlyToLocalTime(timeOfDay.Value), callback, cancellationToken, state, timerOptions);

    /// <inheritdoc />
    public IClockDayTimeTimer RegisterTimeOfDay (LocalTimeOfDay timeOfDay,
        Action<ClockTimerCallbackContext, CancellationToken> callback,
        CancellationToken cancellationToken,
        object? state = null,
        DayTimeTimerOptions? timerOptions = null) =>
        RegisterTimeOfDay(TimeOnlyToLocalTime(timeOfDay.Value), callback, cancellationToken, state, timerOptions);

    /// <inheritdoc />
    public IClockDayTimeTimer RegisterAsyncTimeOfDay (LocalTimeOfDay timeOfDay,
        Func<CancellationToken, ValueTask> callback,
        CancellationToken cancellationToken,
        DayTimeTimerOptions? timerOptions = null) =>
        RegisterAsyncTimeOfDay(TimeOnlyToLocalTime(timeOfDay.Value), callback, cancellationToken, timerOptions);

    /// <inheritdoc />
    public IClockDayTimeTimer RegisterAsyncTimeOfDay (LocalTimeOfDay timeOfDay,
        Func<ClockTimerCallbackContext, CancellationToken, ValueTask> callback,
        CancellationToken cancellationToken,
        object? state = null,
        DayTimeTimerOptions? timerOptions = null) =>
        RegisterAsyncTimeOfDay(TimeOnlyToLocalTime(timeOfDay.Value), callback, cancellationToken, state, timerOptions);

    /// <inheritdoc />
    public IClockDayTimeTimer RegisterTimeOfDay (UtcTimeOfDay timeOfDay,
        Action callback,
        CancellationToken cancellationToken,
        DayTimeTimerOptions? timerOptions = null) =>
        new PrimeClockDayTimeTimerRegistration(this,
            TimeOnlyToLocalTime(timeOfDay.Value),
            IntervalTimerCallbackKind.SimpleAction,
            callback,
            null,
            timerOptions,
            cancellationToken,
            utcTimeOfDaySchedule: true);

    /// <inheritdoc />
    public IClockDayTimeTimer RegisterTimeOfDay (UtcTimeOfDay timeOfDay,
        Action<ClockTimerCallbackContext> callback,
        CancellationToken cancellationToken,
        object? state = null,
        DayTimeTimerOptions? timerOptions = null) =>
        new PrimeClockDayTimeTimerRegistration(this,
            TimeOnlyToLocalTime(timeOfDay.Value),
            IntervalTimerCallbackKind.ContextAction,
            callback,
            state,
            timerOptions,
            cancellationToken,
            utcTimeOfDaySchedule: true);

    /// <inheritdoc />
    public IClockDayTimeTimer RegisterTimeOfDay (UtcTimeOfDay timeOfDay,
        Action<ClockTimerCallbackContext, CancellationToken> callback,
        CancellationToken cancellationToken,
        object? state = null,
        DayTimeTimerOptions? timerOptions = null) =>
        new PrimeClockDayTimeTimerRegistration(this,
            TimeOnlyToLocalTime(timeOfDay.Value),
            IntervalTimerCallbackKind.ContextActionWithToken,
            callback,
            state,
            timerOptions,
            cancellationToken,
            utcTimeOfDaySchedule: true);

    /// <inheritdoc />
    public IClockDayTimeTimer RegisterAsyncTimeOfDay (UtcTimeOfDay timeOfDay,
        Func<CancellationToken, ValueTask> callback,
        CancellationToken cancellationToken,
        DayTimeTimerOptions? timerOptions = null) =>
        new PrimeClockDayTimeTimerRegistration(this,
            TimeOnlyToLocalTime(timeOfDay.Value),
            IntervalTimerCallbackKind.SimpleAsync,
            callback,
            null,
            timerOptions,
            cancellationToken,
            utcTimeOfDaySchedule: true);

    /// <inheritdoc />
    public IClockDayTimeTimer RegisterAsyncTimeOfDay (UtcTimeOfDay timeOfDay,
        Func<ClockTimerCallbackContext, CancellationToken, ValueTask> callback,
        CancellationToken cancellationToken,
        object? state = null,
        DayTimeTimerOptions? timerOptions = null) =>
        new PrimeClockDayTimeTimerRegistration(this,
            TimeOnlyToLocalTime(timeOfDay.Value),
            IntervalTimerCallbackKind.ContextAsync,
            callback,
            state,
            timerOptions,
            cancellationToken,
            utcTimeOfDaySchedule: true);
#else
    /// <inheritdoc />
    public IClockDayTimeTimer RegisterTimeOfDay (LocalTime timeOfDay,
        Action callback,
        CancellationToken cancellationToken,
        DayTimeTimerOptions? timerOptions = null) =>
        new PrimeClockDayTimeTimerRegistration(this,
            timeOfDay,
            IntervalTimerCallbackKind.SimpleAction,
            callback,
            null,
            timerOptions,
            cancellationToken);

    /// <inheritdoc />
    public IClockDayTimeTimer RegisterTimeOfDay (LocalTime timeOfDay,
        Action<ClockTimerCallbackContext> callback,
        CancellationToken cancellationToken,
        object? state = null,
        DayTimeTimerOptions? timerOptions = null) =>
        new PrimeClockDayTimeTimerRegistration(this,
            timeOfDay,
            IntervalTimerCallbackKind.ContextAction,
            callback,
            state,
            timerOptions,
            cancellationToken);

    /// <inheritdoc />
    public IClockDayTimeTimer RegisterTimeOfDay (LocalTime timeOfDay,
        Action<ClockTimerCallbackContext, CancellationToken> callback,
        CancellationToken cancellationToken,
        object? state = null,
        DayTimeTimerOptions? timerOptions = null) =>
        new PrimeClockDayTimeTimerRegistration(this,
            timeOfDay,
            IntervalTimerCallbackKind.ContextActionWithToken,
            callback,
            state,
            timerOptions,
            cancellationToken);

    /// <inheritdoc />
    public IClockDayTimeTimer RegisterAsyncTimeOfDay (LocalTime timeOfDay,
        Func<CancellationToken, ValueTask> callback,
        CancellationToken cancellationToken,
        DayTimeTimerOptions? timerOptions = null) =>
        new PrimeClockDayTimeTimerRegistration(this,
            timeOfDay,
            IntervalTimerCallbackKind.SimpleAsync,
            callback,
            null,
            timerOptions,
            cancellationToken);

    /// <inheritdoc />
    public IClockDayTimeTimer RegisterAsyncTimeOfDay (LocalTime timeOfDay,
        Func<ClockTimerCallbackContext, CancellationToken, ValueTask> callback,
        CancellationToken cancellationToken,
        object? state = null,
        DayTimeTimerOptions? timerOptions = null) =>
        new PrimeClockDayTimeTimerRegistration(this,
            timeOfDay,
            IntervalTimerCallbackKind.ContextAsync,
            callback,
            state,
            timerOptions,
            cancellationToken);
#endif

    #endregion IPrimeClock — Time-of-day timers (RegisterTimeOfDay)

    #endregion Interface Implementations
}
