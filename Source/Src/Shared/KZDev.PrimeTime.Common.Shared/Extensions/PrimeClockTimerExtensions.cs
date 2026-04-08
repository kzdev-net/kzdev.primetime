// Copyright (c) Kevin Zehrer
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

#if SYSTEMCLOCK
namespace KZDev.SystemClock.PrimeTime;
#else
namespace KZDev.PrimeTime;
#endif

//################################################################################
/// <summary>
///   Extension methods for <see cref="IPrimeClock"/> interval and (when targeting .NET with day-time APIs) 
///   time-of-day timers. These provide convenient overloads for common timer registration patterns, 
///   such as repeating timers and callbacks that do not require context or cancellation tokens. 
/// </summary>
/// <remarks>
///   <para>
///     Time-of-day extension overloads that take <c>LocalTimeOfDay</c> or <c>UtcTimeOfDay</c> are emitted
///     only under the SDK <c>NET</c> symbol (see remarks on <see cref="IPrimeClock"/>). They are not
///     available when targeting .NET Standard 2.0 or .NET Framework, even if online API reference lists them.
///   </para>
/// </remarks>
public static class PrimeClockTimerExtensions
{
    #region IPrimeClock — Interval timers (RegisterTimer)

    //----------------------------------------------------------------------------
    /// <summary>
    ///   Registers a one-shot or repeating interval timer with a synchronous callback that receives context and state.
    /// </summary>
    /// <param name="clock">The clock on which to register the timer.</param>
    /// <param name="callbackTime">Time until the first callback.</param>
    /// <param name="callback">The callback invoked when the timer fires; receives timer context.</param>
    /// <param name="cancellationToken">
    ///   Token that participates in cancelling the timer registration; pass <see cref="CancellationToken.None"/> when
    ///   no external cancellation is required.
    /// </param>
    /// <param name="state">Optional state passed to the callback via <see cref="ClockTimerCallbackContext"/>.</param>
    /// <param name="repeat">If <c>true</c>, repeat using <paramref name="callbackTime"/> as the interval.</param>
    /// <param name="timerOptions">Optional timer options (e.g. reset-after-callback, execution context).</param>
    /// <returns>An <see cref="IClockIntervalTimer"/> to monitor or change the timer.</returns>
    public static IClockIntervalTimer RegisterTimer (this IPrimeClock clock,
        TimeSpan callbackTime,
        Action<ClockTimerCallbackContext> callback,
        CancellationToken cancellationToken,
        object? state = null,
        bool repeat = false,
        IntervalTimerOptions? timerOptions = null)
    {
        TimeSpan repeatInterval = repeat ? callbackTime : Timeout.InfiniteTimeSpan;
        return clock.RegisterTimer(callbackTime, repeatInterval, callback, cancellationToken, state, timerOptions);
    }
    //----------------------------------------------------------------------------

    //----------------------------------------------------------------------------
    /// <summary>
    ///   Registers a one-shot or repeating interval timer with a synchronous callback that receives context and cancellation token.
    /// </summary>
    /// <param name="clock">The clock on which to register the timer.</param>
    /// <param name="callbackTime">Time until the first callback.</param>
    /// <param name="callback">The callback invoked when the timer fires; receives timer context and a cancellation token.</param>
    /// <param name="cancellationToken">
    ///   Token that participates in cancelling the timer registration; pass <see cref="CancellationToken.None"/> when
    ///   no external cancellation is required.
    /// </param>
    /// <param name="state">Optional state passed to the callback via <see cref="ClockTimerCallbackContext"/>.</param>
    /// <param name="repeat">If <c>true</c>, repeat using <paramref name="callbackTime"/> as the interval.</param>
    /// <param name="timerOptions">Optional timer options (e.g. reset-after-callback, execution context).</param>
    /// <returns>An <see cref="IClockIntervalTimer"/> to monitor or change the timer.</returns>
    public static IClockIntervalTimer RegisterTimer (this IPrimeClock clock,
        TimeSpan callbackTime,
        Action<ClockTimerCallbackContext, CancellationToken> callback,
        CancellationToken cancellationToken,
        object? state = null,
        bool repeat = false,
        IntervalTimerOptions? timerOptions = null)
    {
        TimeSpan repeatInterval = repeat ? callbackTime : Timeout.InfiniteTimeSpan;
        return clock.RegisterTimer(callbackTime, repeatInterval,
            context => callback(context, cancellationToken),
            cancellationToken,
            state,
            timerOptions);
    }
    //----------------------------------------------------------------------------

    //----------------------------------------------------------------------------
    /// <summary>
    ///   Registers a one-shot or repeating interval timer with a synchronous callback.
    /// </summary>
    /// <param name="clock">The clock on which to register the timer.</param>
    /// <param name="callbackTime">Time until the first callback.</param>
    /// <param name="callback">The callback to run when the timer fires.</param>
    /// <param name="cancellationToken">
    ///   Token that participates in cancelling the timer registration; pass <see cref="CancellationToken.None"/> when
    ///   no external cancellation is required.
    /// </param>
    /// <param name="repeat">If <c>true</c>, repeat using <paramref name="callbackTime"/> as the interval.</param>
    /// <param name="timerOptions">Optional timer options (e.g. reset-after-callback, execution context).</param>
    /// <returns>An <see cref="IClockIntervalTimer"/> to monitor or change the timer.</returns>
    public static IClockIntervalTimer RegisterTimer (this IPrimeClock clock,
        TimeSpan callbackTime,
        Action callback,
        CancellationToken cancellationToken,
        bool repeat = false,
        IntervalTimerOptions? timerOptions = null)
    {
        TimeSpan repeatInterval = repeat ? callbackTime : Timeout.InfiniteTimeSpan;
        return clock.RegisterTimer(callbackTime, repeatInterval,
            _ => callback(),
            cancellationToken,
            null,
            timerOptions);
    }
    //----------------------------------------------------------------------------

    //----------------------------------------------------------------------------
    /// <summary>
    ///   Registers a one-shot or repeating interval timer with an asynchronous callback that receives context and cancellation token.
    /// </summary>
    /// <param name="clock">The clock on which to register the timer.</param>
    /// <param name="callbackTime">Time until the first callback.</param>
    /// <param name="callback">The async callback; receives timer context and a cancellation token.</param>
    /// <param name="cancellationToken">
    ///   Token that participates in cancelling the timer registration and is passed to the callback; use
    ///   <see cref="CancellationToken.None"/> when no external cancellation is required.
    /// </param>
    /// <param name="state">Optional state passed to the callback via <see cref="ClockTimerCallbackContext"/>.</param>
    /// <param name="repeat">If <c>true</c>, repeat using <paramref name="callbackTime"/> as the interval.</param>
    /// <param name="timerOptions">Optional timer options.</param>
    /// <returns>An <see cref="IClockIntervalTimer"/> to monitor or change the timer.</returns>
    public static IClockIntervalTimer RegisterAsyncTimer (this IPrimeClock clock,
        TimeSpan callbackTime,
        Func<ClockTimerCallbackContext, CancellationToken, ValueTask> callback,
        CancellationToken cancellationToken,
        object? state = null,
        bool repeat = false,
        IntervalTimerOptions? timerOptions = null)
    {
        TimeSpan repeatInterval = repeat ? callbackTime : Timeout.InfiniteTimeSpan;
        return clock.RegisterAsyncTimer(callbackTime, repeatInterval, callback, cancellationToken, state, timerOptions);
    }
    //----------------------------------------------------------------------------

    //----------------------------------------------------------------------------
    /// <summary>
    ///   Registers a one-shot or repeating interval timer with an asynchronous callback.
    /// </summary>
    /// <param name="clock">The clock on which to register the timer.</param>
    /// <param name="callbackTime">Time until the first callback.</param>
    /// <param name="callback">The async callback (returns <see cref="ValueTask"/>).</param>
    /// <param name="cancellationToken">
    ///   Token that participates in cancelling the timer registration and is passed to the callback; use
    ///   <see cref="CancellationToken.None"/> when no external cancellation is required.
    /// </param>
    /// <param name="repeat">If <c>true</c>, repeat using <paramref name="callbackTime"/> as the interval.</param>
    /// <param name="timerOptions">Optional timer options.</param>
    /// <returns>An <see cref="IClockIntervalTimer"/> to monitor or change the timer.</returns>
    public static IClockIntervalTimer RegisterAsyncTimer (this IPrimeClock clock,
        TimeSpan callbackTime,
        Func<CancellationToken, ValueTask> callback,
        CancellationToken cancellationToken,
        bool repeat = false,
        IntervalTimerOptions? timerOptions = null)
    {
        TimeSpan repeatInterval = repeat ? callbackTime : Timeout.InfiniteTimeSpan;
        return clock.RegisterAsyncTimer(callbackTime, repeatInterval,
            (innerContext, innerCancellationToken) => callback(innerCancellationToken),
            cancellationToken,
            null,
            timerOptions);
    }
    //----------------------------------------------------------------------------

    //----------------------------------------------------------------------------
    /// <summary>
    ///   Registers an interval timer with an initial delay and a separate repeat interval (sync callback with context and token).
    /// </summary>
    /// <param name="clock">The clock on which to register the timer.</param>
    /// <param name="callbackTime">Time until the first callback.</param>
    /// <param name="repeatInterval">Interval for subsequent callbacks; <see cref="Timeout.InfiniteTimeSpan"/> for one-shot.</param>
    /// <param name="callback">The callback invoked when the timer fires; receives timer context and a cancellation token.</param>
    /// <param name="cancellationToken">
    ///   Token that participates in cancelling the timer registration; pass <see cref="CancellationToken.None"/> when
    ///   no external cancellation is required.
    /// </param>
    /// <param name="state">Optional state passed to the callback via <see cref="ClockTimerCallbackContext"/>.</param>
    /// <param name="timerOptions">Optional timer options.</param>
    /// <returns>An <see cref="IClockIntervalTimer"/> to monitor or change the timer.</returns>
    public static IClockIntervalTimer RegisterTimer (this IPrimeClock clock,
        TimeSpan callbackTime,
        TimeSpan repeatInterval,
        Action<ClockTimerCallbackContext, CancellationToken> callback,
        CancellationToken cancellationToken,
        object? state = null,
        IntervalTimerOptions? timerOptions = null) =>
        clock.RegisterTimer(callbackTime, repeatInterval,
            context => callback(context, cancellationToken),
            cancellationToken,
            state,
            timerOptions);
    //----------------------------------------------------------------------------

    //----------------------------------------------------------------------------
    /// <summary>
    ///   Registers an interval timer with an initial delay and a separate repeat interval (sync callback).
    /// </summary>
    /// <param name="clock">The clock on which to register the timer.</param>
    /// <param name="callbackTime">Time until the first callback.</param>
    /// <param name="repeatInterval">Interval for subsequent callbacks; <see cref="Timeout.InfiniteTimeSpan"/> for one-shot.</param>
    /// <param name="callback">The callback to run when the timer fires.</param>
    /// <param name="cancellationToken">
    ///   Token that participates in cancelling the timer registration; pass <see cref="CancellationToken.None"/> when
    ///   no external cancellation is required.
    /// </param>
    /// <param name="timerOptions">Optional timer options.</param>
    /// <returns>An <see cref="IClockIntervalTimer"/> to monitor or change the timer.</returns>
    public static IClockIntervalTimer RegisterTimer (this IPrimeClock clock,
        TimeSpan callbackTime,
        TimeSpan repeatInterval,
        Action callback,
        CancellationToken cancellationToken,
        IntervalTimerOptions? timerOptions = null) =>
        clock.RegisterTimer(callbackTime, repeatInterval,
            _ => callback(),
            cancellationToken,
            null,
            timerOptions);
    //----------------------------------------------------------------------------

    //----------------------------------------------------------------------------
    /// <summary>
    ///   Registers an interval timer with an initial delay and a separate repeat interval (async callback).
    /// </summary>
    /// <param name="clock">The clock on which to register the timer.</param>
    /// <param name="callbackTime">Time until the first callback.</param>
    /// <param name="repeatInterval">Interval for subsequent callbacks; <see cref="Timeout.InfiniteTimeSpan"/> for one-shot.</param>
    /// <param name="callback">The async callback (returns <see cref="ValueTask"/>).</param>
    /// <param name="cancellationToken">
    ///   Token that participates in cancelling the timer registration and is passed to the callback; use
    ///   <see cref="CancellationToken.None"/> when no external cancellation is required.
    /// </param>
    /// <param name="timerOptions">Optional timer options.</param>
    /// <returns>An <see cref="IClockIntervalTimer"/> to monitor or change the timer.</returns>
    public static IClockIntervalTimer RegisterAsyncTimer (this IPrimeClock clock,
        TimeSpan callbackTime,
        TimeSpan repeatInterval,
        Func<CancellationToken, ValueTask> callback,
        CancellationToken cancellationToken,
        IntervalTimerOptions? timerOptions = null) =>
        clock.RegisterAsyncTimer(callbackTime, repeatInterval,
            (innerContext, innerCancellationToken) => callback(innerCancellationToken),
            cancellationToken,
            null,
            timerOptions);
    //----------------------------------------------------------------------------

    #endregion IPrimeClock — Interval timers (RegisterTimer)

#if NET
    #region IPrimeClock — Time-of-day timers (RegisterTimeOfDay)

    //----------------------------------------------------------------------------
    /// <summary>
    ///   Registers a time-of-day timer with a callback that receives context and cancellation token.
    /// </summary>
    /// <param name="clock">The clock on which to register the timer.</param>
    /// <param name="timeOfDay">The local time of day at which to fire.</param>
    /// <param name="callback">The callback invoked when the timer fires; receives timer context and a cancellation token.</param>
    /// <param name="cancellationToken">
    ///   Token that participates in cancelling the timer registration; pass <see cref="CancellationToken.None"/> when
    ///   no external cancellation is required.
    /// </param>
    /// <param name="state">Optional state passed to the callback via <see cref="ClockTimerCallbackContext"/>.</param>
    /// <param name="timerOptions">Optional day-time timer options.</param>
    /// <returns>An <see cref="IClockDayTimeTimer"/> to monitor or change the timer.</returns>
    public static IClockDayTimeTimer RegisterTimeOfDay (this IPrimeClock clock,
        LocalTimeOfDay timeOfDay,
        Action<ClockTimerCallbackContext, CancellationToken> callback,
        CancellationToken cancellationToken,
        object? state = null,
        DayTimeTimerOptions? timerOptions = null) =>
        clock.RegisterTimeOfDay(timeOfDay,
            context => callback(context, cancellationToken),
            cancellationToken,
            state,
            timerOptions);
    //----------------------------------------------------------------------------

    //----------------------------------------------------------------------------
    /// <summary>
    ///   Registers a time-of-day timer that fires at the given local time each day (sync callback).
    /// </summary>
    /// <param name="clock">The clock on which to register the timer.</param>
    /// <param name="timeOfDay">The local time of day at which to fire.</param>
    /// <param name="callback">The callback to run when the timer fires.</param>
    /// <param name="cancellationToken">
    ///   Token that participates in cancelling the timer registration; pass <see cref="CancellationToken.None"/> when
    ///   no external cancellation is required.
    /// </param>
    /// <param name="timerOptions">Optional day-time timer options.</param>
    /// <returns>An <see cref="IClockDayTimeTimer"/> to monitor or change the timer.</returns>
    public static IClockDayTimeTimer RegisterTimeOfDay (this IPrimeClock clock,
        LocalTimeOfDay timeOfDay,
        Action callback,
        CancellationToken cancellationToken,
        DayTimeTimerOptions? timerOptions = null) =>
        clock.RegisterTimeOfDay(timeOfDay,
            _ => callback(),
            cancellationToken,
            null,
            timerOptions);
    //----------------------------------------------------------------------------

    //----------------------------------------------------------------------------
    /// <summary>
    ///   Registers a time-of-day timer with an asynchronous callback.
    /// </summary>
    /// <param name="clock">The clock on which to register the timer.</param>
    /// <param name="timeOfDay">The local time of day at which to fire.</param>
    /// <param name="callback">The async callback (returns <see cref="ValueTask"/>).</param>
    /// <param name="cancellationToken">
    ///   Token that participates in cancelling the timer registration and is passed to the callback; use
    ///   <see cref="CancellationToken.None"/> when no external cancellation is required.
    /// </param>
    /// <param name="timerOptions">Optional day-time timer options.</param>
    /// <returns>An <see cref="IClockDayTimeTimer"/> to monitor or change the timer.</returns>
    public static IClockDayTimeTimer RegisterAsyncTimeOfDay (this IPrimeClock clock,
        LocalTimeOfDay timeOfDay,
        Func<CancellationToken, ValueTask> callback,
        CancellationToken cancellationToken,
        DayTimeTimerOptions? timerOptions = null) =>
        clock.RegisterAsyncTimeOfDay(timeOfDay,
            (innerContext, innerCancellationToken) => callback(innerCancellationToken),
            cancellationToken,
            null,
            timerOptions);
    //----------------------------------------------------------------------------

    //----------------------------------------------------------------------------
    /// <summary>
    ///   Registers a UTC time-of-day timer with a callback that receives context and cancellation token.
    /// </summary>
    /// <param name="clock">The clock on which to register the timer.</param>
    /// <param name="timeOfDay">The UTC time of day at which to fire.</param>
    /// <param name="callback">The callback invoked when the timer fires; receives timer context and a cancellation token.</param>
    /// <param name="cancellationToken">
    ///   Token that participates in cancelling the timer registration; pass <see cref="CancellationToken.None"/> when
    ///   no external cancellation is required.
    /// </param>
    /// <param name="state">Optional state passed to the callback via <see cref="ClockTimerCallbackContext"/>.</param>
    /// <param name="timerOptions">Optional day-time timer options.</param>
    /// <returns>An <see cref="IClockDayTimeTimer"/> to monitor or change the timer.</returns>
    public static IClockDayTimeTimer RegisterTimeOfDay (this IPrimeClock clock,
        UtcTimeOfDay timeOfDay,
        Action<ClockTimerCallbackContext, CancellationToken> callback,
        CancellationToken cancellationToken,
        object? state = null,
        DayTimeTimerOptions? timerOptions = null) =>
        clock.RegisterTimeOfDay(timeOfDay,
            context => callback(context, cancellationToken),
            cancellationToken,
            state,
            timerOptions);
    //----------------------------------------------------------------------------

    //----------------------------------------------------------------------------
    /// <summary>
    ///   Registers a time-of-day timer that fires at the given UTC time each day (sync callback).
    /// </summary>
    /// <param name="clock">The clock on which to register the timer.</param>
    /// <param name="timeOfDay">The UTC time of day at which to fire.</param>
    /// <param name="callback">The callback to run when the timer fires.</param>
    /// <param name="cancellationToken">
    ///   Token that participates in cancelling the timer registration; pass <see cref="CancellationToken.None"/> when
    ///   no external cancellation is required.
    /// </param>
    /// <param name="timerOptions">Optional day-time timer options.</param>
    /// <returns>An <see cref="IClockDayTimeTimer"/> to monitor or change the timer.</returns>
    public static IClockDayTimeTimer RegisterTimeOfDay (this IPrimeClock clock,
        UtcTimeOfDay timeOfDay,
        Action callback,
        CancellationToken cancellationToken,
        DayTimeTimerOptions? timerOptions = null) =>
        clock.RegisterTimeOfDay(timeOfDay,
            _ => callback(),
            cancellationToken,
            null,
            timerOptions);
    //----------------------------------------------------------------------------

    //----------------------------------------------------------------------------
    /// <summary>
    ///   Registers a UTC time-of-day timer with an asynchronous callback.
    /// </summary>
    /// <param name="clock">The clock on which to register the timer.</param>
    /// <param name="timeOfDay">The UTC time of day at which to fire.</param>
    /// <param name="callback">The async callback (returns <see cref="ValueTask"/>).</param>
    /// <param name="cancellationToken">
    ///   Token that participates in cancelling the timer registration and is passed to the callback; use
    ///   <see cref="CancellationToken.None"/> when no external cancellation is required.
    /// </param>
    /// <param name="timerOptions">Optional day-time timer options.</param>
    /// <returns>An <see cref="IClockDayTimeTimer"/> to monitor or change the timer.</returns>
    public static IClockDayTimeTimer RegisterAsyncTimeOfDay (this IPrimeClock clock,
        UtcTimeOfDay timeOfDay,
        Func<CancellationToken, ValueTask> callback,
        CancellationToken cancellationToken,
        DayTimeTimerOptions? timerOptions = null) =>
        clock.RegisterAsyncTimeOfDay(timeOfDay,
            (innerContext, innerCancellationToken) => callback(innerCancellationToken),
            cancellationToken,
            null,
            timerOptions);
    //----------------------------------------------------------------------------

    #endregion IPrimeClock — Time-of-day timers (RegisterTimeOfDay)
#endif
}
//################################################################################
