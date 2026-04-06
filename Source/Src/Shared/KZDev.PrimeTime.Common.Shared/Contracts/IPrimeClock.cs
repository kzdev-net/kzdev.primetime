// Copyright (c) Kevin Zehrer. All rights reserved.
// This file is part of the PrimeTime project.

using System.Diagnostics;

#if SYSTEMCLOCK
namespace KZDev.SystemClock.PrimeTime;
#else
namespace KZDev.PrimeTime;
#endif

//################################################################################
/// <summary>
///   Extends <see cref="IPrimeTime"/> with BCL-based projection members for current time.
/// </summary>
public partial interface IPrimeClock : IPrimeTime
{
    #region IPrimeClock - Now (BCL)

    //----------------------------------------------------------------------------
    /// <summary>
    ///   Gets the current local date and time as a <see cref="DateTimeOffset"/>.
    /// </summary>
    DateTimeOffset LocalNowDateTimeOffset { [DebuggerStepThrough] get; }
    //----------------------------------------------------------------------------

    //----------------------------------------------------------------------------
    /// <summary>
    ///   Gets the current UTC date and time as a <see cref="DateTimeOffset"/>.
    /// </summary>
    DateTimeOffset UtcNowDateTimeOffset { [DebuggerStepThrough] get; }
    //----------------------------------------------------------------------------

    //----------------------------------------------------------------------------
    /// <summary>
    ///   Gets the current local date and time as a <see cref="DateTime"/>.
    /// </summary>
    DateTime LocalNowDateTime { [DebuggerStepThrough] get; }
    //----------------------------------------------------------------------------

    //----------------------------------------------------------------------------
    /// <summary>
    ///   Gets the current UTC date and time as a <see cref="DateTime"/>.
    /// </summary>
    DateTime UtcNowDateTime { [DebuggerStepThrough] get; }
    //----------------------------------------------------------------------------

#if NET
    //----------------------------------------------------------------------------
    /// <summary>
    ///   Gets the current local time-of-day as <see cref="TimeOnly"/>.
    /// </summary>
    TimeOnly LocalNowTimeOnly { [DebuggerStepThrough] get; }
    //----------------------------------------------------------------------------
    /// <summary>
    ///   Gets the current UTC time-of-day as <see cref="TimeOnly"/>.
    /// </summary>
    TimeOnly UtcNowTimeOnly { [DebuggerStepThrough] get; }
    //----------------------------------------------------------------------------
    /// <summary>
    ///   Gets the current local date as <see cref="DateOnly"/>.
    /// </summary>
    DateOnly LocalNowDateOnly { [DebuggerStepThrough] get; }
    //----------------------------------------------------------------------------
    /// <summary>
    ///   Gets the current UTC date as <see cref="DateOnly"/>.
    /// </summary>
    DateOnly UtcNowDateOnly { [DebuggerStepThrough] get; }
    //----------------------------------------------------------------------------
#endif

    #endregion IPrimeClock - Now (BCL)

    #region IPrimeClock — Interval timers (RegisterTimer)

    //----------------------------------------------------------------------------
    /// <summary>
    ///   Registers an interval timer with an initial delay, a repeat interval, and a synchronous callback that receives
    ///   timer context. Other <c>RegisterTimer</c> overloads are provided as extension methods.
    /// </summary>
    /// <param name="callbackTime">Time until the first callback.</param>
    /// <param name="repeatInterval">Interval for subsequent callbacks; <see cref="Timeout.InfiniteTimeSpan"/> for one-shot.</param>
    /// <param name="callback">The callback invoked when the timer fires; receives timer context.</param>
    /// <param name="cancellationToken">
    ///   Token that participates in cancelling the timer registration; pass <see cref="CancellationToken.None"/> when
    ///   no external cancellation is required.
    /// </param>
    /// <param name="state">Optional state passed to the callback via <see cref="ClockTimerCallbackContext"/>.</param>
    /// <param name="timerOptions">Optional timer options.</param>
    /// <returns>An <see cref="IClockIntervalTimer"/> to monitor or change the timer.</returns>
    IClockIntervalTimer RegisterTimer (TimeSpan callbackTime,
        TimeSpan repeatInterval,
        Action<ClockTimerCallbackContext> callback,
        CancellationToken cancellationToken,
        object? state = null,
        IntervalTimerOptions? timerOptions = null);
    //----------------------------------------------------------------------------
    /// <summary>
    ///   Registers an interval timer with an initial delay, a repeat interval, and an asynchronous callback that receives
    ///   timer context and cancellation token. Other <c>RegisterAsyncTimer</c> overloads are provided as extension methods.
    /// </summary>
    /// <param name="callbackTime">Time until the first callback.</param>
    /// <param name="repeatInterval">Interval for subsequent callbacks; <see cref="Timeout.InfiniteTimeSpan"/> for one-shot.</param>
    /// <param name="callback">The async callback; receives timer context and a cancellation token.</param>
    /// <param name="cancellationToken">
    ///   Token that participates in cancelling the timer registration and is passed to the callback; use
    ///   <see cref="CancellationToken.None"/> when no external cancellation is required.
    /// </param>
    /// <param name="state">Optional state passed to the callback via <see cref="ClockTimerCallbackContext"/>.</param>
    /// <param name="timerOptions">Optional timer options.</param>
    /// <returns>An <see cref="IClockIntervalTimer"/> to monitor or change the timer.</returns>
    IClockIntervalTimer RegisterAsyncTimer (TimeSpan callbackTime,
        TimeSpan repeatInterval,
        Func<ClockTimerCallbackContext, CancellationToken, ValueTask> callback,
        CancellationToken cancellationToken,
        object? state = null,
        IntervalTimerOptions? timerOptions = null);
    //----------------------------------------------------------------------------

    #endregion IPrimeClock — Interval timers (RegisterTimer)

#if NET
    #region IPrimeClock — Time-of-day timers (RegisterTimeOfDay)

    //----------------------------------------------------------------------------
    /// <summary>
    ///   Registers a local time-of-day timer with a synchronous callback that receives timer context and optional state.
    ///   Other <c>RegisterTimeOfDay</c> overloads for <see cref="LocalTimeOfDay"/> are provided as extension methods.
    /// </summary>
    /// <param name="timeOfDay">The local time of day at which to fire.</param>
    /// <param name="callback">The callback invoked when the timer fires; receives timer context.</param>
    /// <param name="cancellationToken">
    ///   Token that participates in cancelling the timer registration; pass <see cref="CancellationToken.None"/> when
    ///   no external cancellation is required.
    /// </param>
    /// <param name="state">Optional state passed to the callback via <see cref="ClockTimerCallbackContext"/>.</param>
    /// <param name="timerOptions">Optional day-time timer options.</param>
    /// <returns>An <see cref="IClockDayTimeTimer"/> to monitor or change the timer.</returns>
    IClockDayTimeTimer RegisterTimeOfDay (LocalTimeOfDay timeOfDay,
        Action<ClockTimerCallbackContext> callback,
        CancellationToken cancellationToken,
        object? state = null,
        DayTimeTimerOptions? timerOptions = null);
    //----------------------------------------------------------------------------
    /// <summary>
    ///   Registers a local time-of-day timer with an asynchronous callback that receives timer context and cancellation token.
    ///   Other <c>RegisterAsyncTimeOfDay</c> overloads for <see cref="LocalTimeOfDay"/> are provided as extension methods.
    /// </summary>
    /// <param name="timeOfDay">The local time of day at which to fire.</param>
    /// <param name="callback">The async callback; receives timer context and a cancellation token.</param>
    /// <param name="cancellationToken">
    ///   Token that participates in cancelling the timer registration and is passed to the callback; use
    ///   <see cref="CancellationToken.None"/> when no external cancellation is required.
    /// </param>
    /// <param name="state">Optional state passed to the callback via <see cref="ClockTimerCallbackContext"/>.</param>
    /// <param name="timerOptions">Optional day-time timer options.</param>
    /// <returns>An <see cref="IClockDayTimeTimer"/> to monitor or change the timer.</returns>
    IClockDayTimeTimer RegisterAsyncTimeOfDay (LocalTimeOfDay timeOfDay,
        Func<ClockTimerCallbackContext, CancellationToken, ValueTask> callback,
        CancellationToken cancellationToken,
        object? state = null,
        DayTimeTimerOptions? timerOptions = null);
    //----------------------------------------------------------------------------
    /// <summary>
    ///   Registers a UTC time-of-day timer with a synchronous callback that receives timer context and optional state.
    ///   Other <c>RegisterTimeOfDay</c> overloads for <see cref="UtcTimeOfDay"/> are provided as extension methods.
    /// </summary>
    /// <param name="timeOfDay">The UTC time of day at which to fire.</param>
    /// <param name="callback">The callback invoked when the timer fires; receives timer context.</param>
    /// <param name="cancellationToken">
    ///   Token that participates in cancelling the timer registration; pass <see cref="CancellationToken.None"/> when
    ///   no external cancellation is required.
    /// </param>
    /// <param name="state">Optional state passed to the callback via <see cref="ClockTimerCallbackContext"/>.</param>
    /// <param name="timerOptions">Optional day-time timer options.</param>
    /// <returns>An <see cref="IClockDayTimeTimer"/> to monitor or change the timer.</returns>
    IClockDayTimeTimer RegisterTimeOfDay (UtcTimeOfDay timeOfDay,
        Action<ClockTimerCallbackContext> callback,
        CancellationToken cancellationToken,
        object? state = null,
        DayTimeTimerOptions? timerOptions = null);
    //----------------------------------------------------------------------------
    /// <summary>
    ///   Registers a UTC time-of-day timer with an asynchronous callback that receives timer context and cancellation token.
    ///   Other <c>RegisterAsyncTimeOfDay</c> overloads for <see cref="UtcTimeOfDay"/> are provided as extension methods.
    /// </summary>
    /// <param name="timeOfDay">The UTC time of day at which to fire.</param>
    /// <param name="callback">The async callback; receives timer context and a cancellation token.</param>
    /// <param name="cancellationToken">
    ///   Token that participates in cancelling the timer registration and is passed to the callback; use
    ///   <see cref="CancellationToken.None"/> when no external cancellation is required.
    /// </param>
    /// <param name="state">Optional state passed to the callback via <see cref="ClockTimerCallbackContext"/>.</param>
    /// <param name="timerOptions">Optional day-time timer options.</param>
    /// <returns>An <see cref="IClockDayTimeTimer"/> to monitor or change the timer.</returns>
    IClockDayTimeTimer RegisterAsyncTimeOfDay (UtcTimeOfDay timeOfDay,
        Func<ClockTimerCallbackContext, CancellationToken, ValueTask> callback,
        CancellationToken cancellationToken,
        object? state = null,
        DayTimeTimerOptions? timerOptions = null);
    //----------------------------------------------------------------------------

    #endregion IPrimeClock — Time-of-day timers (RegisterTimeOfDay)
#endif
}
//################################################################################
