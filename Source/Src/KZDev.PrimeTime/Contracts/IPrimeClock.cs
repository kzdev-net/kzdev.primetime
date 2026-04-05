using System.Diagnostics;

using NodaTime;

namespace KZDev.PrimeTime;

//################################################################################
/// <summary>
///   Extends <see cref="IPrimeTime"/> with NodaTime-based "now" APIs so callers can obtain
///   current time from the clock abstraction using <see cref="Instant"/>,
///   <see cref="LocalDateTime"/>, <see cref="ZonedDateTime"/>, <see cref="LocalTime"/>,
///   and <see cref="LocalDate"/>.
/// </summary>
public partial interface IPrimeClock
{
    #region IPrimeClock — Now (instant and zoned date and time)

    //----------------------------------------------------------------------------
    /// <summary>
    ///   Gets the current instant on the global timeline (UTC).
    /// </summary>
    Instant NowInstant { [DebuggerStepThrough] get; }
    //----------------------------------------------------------------------------
    /// <summary>
    ///   Gets the current local date and time in the system default time zone as a
    ///   <see cref="LocalDateTime"/> (no time zone information).
    /// </summary>
    LocalDateTime LocalNow { [DebuggerStepThrough] get; }
    //----------------------------------------------------------------------------
    /// <summary>
    ///   Gets the current date and time in UTC as a <see cref="ZonedDateTime"/>.
    /// </summary>
    ZonedDateTime UtcNow { [DebuggerStepThrough] get; }
    //----------------------------------------------------------------------------
    /// <summary>
    ///   Gets the current date and time in the system default time zone as a
    ///   <see cref="ZonedDateTime"/>.
    /// </summary>
    ZonedDateTime LocalZonedNow { [DebuggerStepThrough] get; }
    //----------------------------------------------------------------------------
    /// <summary>
    ///   Gets the current date and time in UTC as a <see cref="ZonedDateTime"/>.
    ///   Equivalent to <see cref="UtcNow"/> for symmetry with <see cref="LocalZonedNow"/>.
    /// </summary>
    ZonedDateTime UtcZonedNow { [DebuggerStepThrough] get; }
    //----------------------------------------------------------------------------

    #endregion IPrimeClock — Now (instant and zoned date and time)

    #region IPrimeClock — Now (time-only and date-only)

    //----------------------------------------------------------------------------
    /// <summary>
    ///   Gets the current local time of day (no date component) in the system default zone.
    /// </summary>
    LocalTime LocalNowTime { [DebuggerStepThrough] get; }
    //----------------------------------------------------------------------------
    /// <summary>
    ///   Gets the current UTC time of day (no date component).
    /// </summary>
    LocalTime UtcNowTime { [DebuggerStepThrough] get; }
    //----------------------------------------------------------------------------
    /// <summary>
    ///   Gets the current local date (no time component) in the system default zone.
    /// </summary>
    LocalDate LocalNowDate { [DebuggerStepThrough] get; }
    //----------------------------------------------------------------------------
    /// <summary>
    ///   Gets the current UTC date (no time component).
    /// </summary>
    LocalDate UtcNowDate { [DebuggerStepThrough] get; }
    //----------------------------------------------------------------------------

    #endregion IPrimeClock — Now (time-only and date-only)

    #region IPrimeClock — Interval timers (RegisterTimer)

    //----------------------------------------------------------------------------
    /// <summary>
    ///   Registers an interval timer with an initial delay, a repeat interval, and a synchronous callback that receives
    ///   timer context. Other <see cref="Duration"/>-based <c>RegisterTimer</c> overloads are extension methods.
    /// </summary>
    /// <param name="callbackTime">Duration until the first callback.</param>
    /// <param name="repeatInterval">Interval for subsequent callbacks; use a non-positive duration for one-shot.</param>
    /// <param name="callback">The callback invoked when the timer fires; receives timer context.</param>
    /// <param name="cancellationToken">
    ///   Token that participates in cancelling the timer registration; pass <see cref="CancellationToken.None"/> when
    ///   no external cancellation is required.
    /// </param>
    /// <param name="state">Optional state passed to the callback via <see cref="ClockTimerCallbackContext"/>.</param>
    /// <param name="timerOptions">Optional timer options.</param>
    /// <returns>An <see cref="IClockIntervalTimer"/> to monitor or change the timer.</returns>
    IClockIntervalTimer RegisterTimer (Duration callbackTime,
        Duration repeatInterval,
        Action<ClockTimerCallbackContext> callback,
        CancellationToken cancellationToken,
        object? state = null,
        IntervalTimerOptions? timerOptions = null);
    //----------------------------------------------------------------------------
    /// <summary>
    ///   Registers an interval timer with an initial delay, a repeat interval, and an asynchronous callback that receives
    ///   timer context and cancellation token. Other <see cref="Duration"/>-based <c>RegisterAsyncTimer</c> overloads are extension methods.
    /// </summary>
    /// <param name="callbackTime">Duration until the first callback.</param>
    /// <param name="repeatInterval">Interval for subsequent callbacks; use a non-positive duration for one-shot.</param>
    /// <param name="callback">The async callback; receives timer context and a cancellation token.</param>
    /// <param name="cancellationToken">
    ///   Token that participates in cancelling the timer registration and is passed to the callback; use
    ///   <see cref="CancellationToken.None"/> when no external cancellation is required.
    /// </param>
    /// <param name="state">Optional state passed to the callback via <see cref="ClockTimerCallbackContext"/>.</param>
    /// <param name="timerOptions">Optional timer options.</param>
    /// <returns>An <see cref="IClockIntervalTimer"/> to monitor or change the timer.</returns>
    IClockIntervalTimer RegisterAsyncTimer (Duration callbackTime,
        Duration repeatInterval,
        Func<ClockTimerCallbackContext, CancellationToken, ValueTask> callback,
        CancellationToken cancellationToken,
        object? state = null,
        IntervalTimerOptions? timerOptions = null);
    //----------------------------------------------------------------------------

    #endregion IPrimeClock — Interval timers (RegisterTimer)

    #region IPrimeClock — Time-of-day timers (RegisterTimeOfDay)

    //----------------------------------------------------------------------------
    /// <summary>
    ///   Registers a time-of-day timer with a callback that receives context and state.
    ///   Other <see cref="LocalTime"/> <c>RegisterTimeOfDay</c> overloads are extension methods.
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
    IClockDayTimeTimer RegisterTimeOfDay (LocalTime timeOfDay,
        Action<ClockTimerCallbackContext> callback,
        CancellationToken cancellationToken,
        object? state = null,
        DayTimeTimerOptions? timerOptions = null);
    //----------------------------------------------------------------------------
    /// <summary>
    ///   Registers a time-of-day timer with an asynchronous callback that receives context and cancellation token.
    ///   Other <see cref="LocalTime"/> <c>RegisterAsyncTimeOfDay</c> overloads are extension methods.
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
    IClockDayTimeTimer RegisterAsyncTimeOfDay (LocalTime timeOfDay,
        Func<ClockTimerCallbackContext, CancellationToken, ValueTask> callback,
        CancellationToken cancellationToken,
        object? state = null,
        DayTimeTimerOptions? timerOptions = null);
    //----------------------------------------------------------------------------

    #endregion IPrimeClock — Time-of-day timers (RegisterTimeOfDay)
}
//################################################################################

