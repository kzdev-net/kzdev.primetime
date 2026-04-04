// Copyright (c) Kevin Zehrer. All rights reserved.
// This file is part of the PrimeTime project.

using NodaTime;

namespace KZDev.PrimeTime;

//################################################################################
/// <content>
///   NodaTime-specific partial of <see cref="PrimeClock"/>: additive <see cref="LocalTime"/> and BCL
///   time-of-day registration types, plus <see cref="ClockDayTimeTimerRegistration"/>. Shared BCL and
///   <see cref="Duration"/> members on <see cref="PrimeClock"/> use <see cref="NodaDurationBclConversions"/> so
///   <see cref="Duration"/> maps to the same <see cref="TimeSpan"/> timer semantics as the SystemClock stack.
/// </content>
internal sealed partial class PrimeClock
{
#if NET
    /// <summary>
    ///   Converts a <see cref="TimeOnly"/> wall-clock value to <see cref="LocalTime"/> for Noda day-time scheduling.
    /// </summary>
    /// <param name="t">The wall-clock time of day.</param>
    /// <returns>
    ///   The equivalent Noda <see cref="LocalTime"/>.
    /// </returns>
    private static LocalTime TimeOnlyToLocalTime (TimeOnly t) =>
        new(t.Hour, t.Minute, t.Second, t.Millisecond);

#endif

    #region Interface Implementations

    #region IPrimeClock — Time-of-day timers (RegisterTimeOfDay)

#if NET
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
        new ClockDayTimeTimerRegistration(this,
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
        new ClockDayTimeTimerRegistration(this,
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
        new ClockDayTimeTimerRegistration(this,
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
        new ClockDayTimeTimerRegistration(this,
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
        new ClockDayTimeTimerRegistration(this,
            TimeOnlyToLocalTime(timeOfDay.Value),
            IntervalTimerCallbackKind.ContextAsync,
            callback,
            state,
            timerOptions,
            cancellationToken,
            utcTimeOfDaySchedule: true);

    /// <inheritdoc />
    public IClockDayTimeTimer RegisterTimeOfDay (LocalTime timeOfDay,
        Action callback,
        CancellationToken cancellationToken,
        DayTimeTimerOptions? timerOptions = null) =>
        new ClockDayTimeTimerRegistration(this,
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
        new ClockDayTimeTimerRegistration(this,
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
        new ClockDayTimeTimerRegistration(this,
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
        new ClockDayTimeTimerRegistration(this,
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
        new ClockDayTimeTimerRegistration(this,
            timeOfDay,
            IntervalTimerCallbackKind.ContextAsync,
            callback,
            state,
            timerOptions,
            cancellationToken);
#else
    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public IClockDayTimeTimer RegisterTimeOfDay (LocalTime timeOfDay,
        Action callback,
        CancellationToken cancellationToken,
        DayTimeTimerOptions? timerOptions = null) =>
        new ClockDayTimeTimerRegistration(this,
            timeOfDay,
            IntervalTimerCallbackKind.SimpleAction,
            callback,
            null,
            timerOptions,
            cancellationToken);
    //----------------------------------------------------------------------------

    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public IClockDayTimeTimer RegisterTimeOfDay (LocalTime timeOfDay,
        Action<ClockTimerCallbackContext> callback,
        CancellationToken cancellationToken,
        object? state = null,
        DayTimeTimerOptions? timerOptions = null) =>
        new ClockDayTimeTimerRegistration(this,
            timeOfDay,
            IntervalTimerCallbackKind.ContextAction,
            callback,
            state,
            timerOptions,
            cancellationToken);
    //----------------------------------------------------------------------------

    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public IClockDayTimeTimer RegisterTimeOfDay (LocalTime timeOfDay,
        Action<ClockTimerCallbackContext, CancellationToken> callback,
        CancellationToken cancellationToken,
        object? state = null,
        DayTimeTimerOptions? timerOptions = null) =>
        new ClockDayTimeTimerRegistration(this,
            timeOfDay,
            IntervalTimerCallbackKind.ContextActionWithToken,
            callback,
            state,
            timerOptions,
            cancellationToken);
    //----------------------------------------------------------------------------

    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public IClockDayTimeTimer RegisterAsyncTimeOfDay (LocalTime timeOfDay,
        Func<CancellationToken, ValueTask> callback,
        CancellationToken cancellationToken,
        DayTimeTimerOptions? timerOptions = null) =>
        new ClockDayTimeTimerRegistration(this,
            timeOfDay,
            IntervalTimerCallbackKind.SimpleAsync,
            callback,
            null,
            timerOptions,
            cancellationToken);
    //----------------------------------------------------------------------------

    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public IClockDayTimeTimer RegisterAsyncTimeOfDay (LocalTime timeOfDay,
        Func<ClockTimerCallbackContext, CancellationToken, ValueTask> callback,
        CancellationToken cancellationToken,
        object? state = null,
        DayTimeTimerOptions? timerOptions = null) =>
        new ClockDayTimeTimerRegistration(this,
            timeOfDay,
            IntervalTimerCallbackKind.ContextAsync,
            callback,
            state,
            timerOptions,
            cancellationToken);
    //----------------------------------------------------------------------------
#endif

    #endregion IPrimeClock — Time-of-day timers (RegisterTimeOfDay)

    #endregion Interface Implementations
}
//################################################################################
