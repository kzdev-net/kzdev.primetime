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
    DateTimeOffset LocalNowOffset { [DebuggerStepThrough] get; }
    //----------------------------------------------------------------------------

    //----------------------------------------------------------------------------
    /// <summary>
    ///   Gets the current UTC date and time as a <see cref="DateTimeOffset"/>.
    /// </summary>
    DateTimeOffset UtcNowOffset { [DebuggerStepThrough] get; }
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
    /// <summary>
    ///   Gets the current local time-of-day as <see cref="TimeOnly"/>.
    /// </summary>
    TimeOnly LocalNowTimeOnly { [DebuggerStepThrough] get; }

    /// <summary>
    ///   Gets the current UTC time-of-day as <see cref="TimeOnly"/>.
    /// </summary>
    TimeOnly UtcNowTimeOnly { [DebuggerStepThrough] get; }

    /// <summary>
    ///   Gets the current local date as <see cref="DateOnly"/>.
    /// </summary>
    DateOnly LocalNowDateOnly { [DebuggerStepThrough] get; }

    /// <summary>
    ///   Gets the current UTC date as <see cref="DateOnly"/>.
    /// </summary>
    DateOnly UtcNowDateOnly { [DebuggerStepThrough] get; }
#endif

    #endregion IPrimeClock - Now (BCL)

    #region IPrimeClock — Interval timers (RegisterTimer)

    //----------------------------------------------------------------------------
    /// <summary>
    ///   Registers a one-shot or repeating interval timer with a synchronous callback that receives context and state.
    /// </summary>
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
    IClockIntervalTimer RegisterTimer (TimeSpan callbackTime,
            Action<ClockTimerCallbackContext> callback,
            CancellationToken cancellationToken,
            object? state = null,
            bool repeat = false, IntervalTimerOptions? timerOptions = null);
    //----------------------------------------------------------------------------
    /// <summary>
    ///   Registers a one-shot or repeating interval timer with a synchronous callback that receives context and cancellation token.
    /// </summary>
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
    IClockIntervalTimer RegisterTimer (TimeSpan callbackTime,
            Action<ClockTimerCallbackContext, CancellationToken> callback,
            CancellationToken cancellationToken,
            object? state = null,
            bool repeat = false,
            IntervalTimerOptions? timerOptions = null);
    //----------------------------------------------------------------------------
    /// <summary>
    ///   Registers a one-shot or repeating interval timer with a synchronous callback.
    /// </summary>
    /// <param name="callbackTime">Time until the first callback.</param>
    /// <param name="callback">The callback to run when the timer fires.</param>
    /// <param name="repeat">If <c>true</c>, repeat using <paramref name="callbackTime"/> as the interval.</param>
    /// <param name="timerOptions">Optional timer options (e.g. reset-after-callback, execution context).</param>
    /// <param name="cancellationToken">
    ///   Token that participates in cancelling the timer registration; pass <see cref="CancellationToken.None"/> when
    ///   no external cancellation is required.
    /// </param>
    /// <returns>An <see cref="IClockIntervalTimer"/> to monitor or change the timer.</returns>
    IClockIntervalTimer RegisterTimer (TimeSpan callbackTime, Action callback,
            CancellationToken cancellationToken,
            bool repeat = false, IntervalTimerOptions? timerOptions = null);
    //----------------------------------------------------------------------------
    /// <summary>
    ///   Registers a one-shot or repeating interval timer with an asynchronous callback that receives context and cancellation token.
    /// </summary>
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
    IClockIntervalTimer RegisterAsyncTimer (TimeSpan callbackTime,
            Func<ClockTimerCallbackContext, CancellationToken, ValueTask> callback,
            CancellationToken cancellationToken,
            object? state = null,
            bool repeat = false,
            IntervalTimerOptions? timerOptions = null);
    //----------------------------------------------------------------------------
    /// <summary>
    ///   Registers a one-shot or repeating interval timer with an asynchronous callback.
    /// </summary>
    /// <param name="callbackTime">Time until the first callback.</param>
    /// <param name="callback">The async callback (returns <see cref="ValueTask"/>).</param>
    /// <param name="repeat">If <c>true</c>, repeat using <paramref name="callbackTime"/> as the interval.</param>
    /// <param name="timerOptions">Optional timer options.</param>
    /// <param name="cancellationToken">
    ///   Token that participates in cancelling the timer registration and is passed to the callback; use
    ///   <see cref="CancellationToken.None"/> when no external cancellation is required.
    /// </param>
    /// <returns>An <see cref="IClockIntervalTimer"/> to monitor or change the timer.</returns>
    IClockIntervalTimer RegisterAsyncTimer (TimeSpan callbackTime,
            Func<CancellationToken, ValueTask> callback,
            CancellationToken cancellationToken,
            bool repeat = false,
            IntervalTimerOptions? timerOptions = null);
    //----------------------------------------------------------------------------
    /// <summary>
    ///   Registers an interval timer with an initial delay and a separate repeat interval (sync callback with context).
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
    ///   Registers an interval timer with an initial delay and a separate repeat interval (sync callback with context and token).
    /// </summary>
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
    IClockIntervalTimer RegisterTimer (TimeSpan callbackTime,
        TimeSpan repeatInterval,
            Action<ClockTimerCallbackContext, CancellationToken> callback,
            CancellationToken cancellationToken,
            object? state = null,
            IntervalTimerOptions? timerOptions = null);
    //----------------------------------------------------------------------------
    /// <summary>
    ///   Registers an interval timer with an initial delay and a separate repeat interval (sync callback).
    /// </summary>
    /// <param name="callbackTime">Time until the first callback.</param>
    /// <param name="repeatInterval">Interval for subsequent callbacks; <see cref="Timeout.InfiniteTimeSpan"/> for one-shot.</param>
    /// <param name="callback">The callback to run when the timer fires.</param>
    /// <param name="timerOptions">Optional timer options.</param>
    /// <param name="cancellationToken">
    ///   Token that participates in cancelling the timer registration; pass <see cref="CancellationToken.None"/> when
    ///   no external cancellation is required.
    /// </param>
    /// <returns>An <see cref="IClockIntervalTimer"/> to monitor or change the timer.</returns>
    IClockIntervalTimer RegisterTimer (TimeSpan callbackTime,
        TimeSpan repeatInterval,
            Action callback,
            CancellationToken cancellationToken,
            IntervalTimerOptions? timerOptions = null);
    //----------------------------------------------------------------------------
    /// <summary>
    ///   Registers an interval timer with an initial delay and a separate repeat interval (async callback with context).
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
    /// <summary>
    ///   Registers an interval timer with an initial delay and a separate repeat interval (async callback).
    /// </summary>
    /// <param name="callbackTime">Time until the first callback.</param>
    /// <param name="repeatInterval">Interval for subsequent callbacks; <see cref="Timeout.InfiniteTimeSpan"/> for one-shot.</param>
    /// <param name="callback">The async callback (returns <see cref="ValueTask"/>).</param>
    /// <param name="cancellationToken">
    ///   Token that participates in cancelling the timer registration and is passed to the callback; use
    ///   <see cref="CancellationToken.None"/> when no external cancellation is required.
    /// </param>
    /// <param name="timerOptions">Optional timer options.</param>
    /// <returns>An <see cref="IClockIntervalTimer"/> to monitor or change the timer.</returns>
    IClockIntervalTimer RegisterAsyncTimer (TimeSpan callbackTime,
        TimeSpan repeatInterval,
            Func<CancellationToken, ValueTask> callback,
            CancellationToken cancellationToken,
            IntervalTimerOptions? timerOptions = null);
    //----------------------------------------------------------------------------

    #endregion IPrimeClock — Interval timers (RegisterTimer)

#if NET
    #region IPrimeClock — Time-of-day timers (RegisterTimeOfDay)

    /// <summary>
    ///   Registers a time-of-day timer with a callback that receives context and state.
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
    /// <summary>
    ///   Registers a time-of-day timer with a callback that receives context and cancellation token.
    /// </summary>
    /// <param name="timeOfDay">The local time of day at which to fire.</param>
    /// <param name="callback">The callback invoked when the timer fires; receives timer context and a cancellation token.</param>
    /// <param name="cancellationToken">
    ///   Token that participates in cancelling the timer registration; pass <see cref="CancellationToken.None"/> when
    ///   no external cancellation is required.
    /// </param>
    /// <param name="state">Optional state passed to the callback via <see cref="ClockTimerCallbackContext"/>.</param>
    /// <param name="timerOptions">Optional day-time timer options.</param>
    /// <returns>An <see cref="IClockDayTimeTimer"/> to monitor or change the timer.</returns>
    IClockDayTimeTimer RegisterTimeOfDay (LocalTimeOfDay timeOfDay,
            Action<ClockTimerCallbackContext, CancellationToken> callback,
            CancellationToken cancellationToken,
            object? state = null,
            DayTimeTimerOptions? timerOptions = null);
    /// <summary>
    ///   Registers a time-of-day timer with an asynchronous callback that receives context and cancellation token.
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
    /// <summary>
    ///   Registers a time-of-day timer that fires at the given local time each day (sync callback).
    /// </summary>
    /// <param name="timeOfDay">The local time of day at which to fire.</param>
    /// <param name="callback">The callback to run when the timer fires.</param>
    /// <param name="timerOptions">Optional day-time timer options.</param>
    /// <param name="cancellationToken">
    ///   Token that participates in cancelling the timer registration; pass <see cref="CancellationToken.None"/> when
    ///   no external cancellation is required.
    /// </param>
    /// <returns>An <see cref="IClockDayTimeTimer"/> to monitor or change the timer.</returns>
    IClockDayTimeTimer RegisterTimeOfDay (LocalTimeOfDay timeOfDay,
            Action callback,
            CancellationToken cancellationToken,
            DayTimeTimerOptions? timerOptions = null);
    /// <summary>
    ///   Registers a time-of-day timer with an asynchronous callback.
    /// </summary>
    /// <param name="timeOfDay">The local time of day at which to fire.</param>
    /// <param name="callback">The async callback (returns <see cref="ValueTask"/>).</param>
    /// <param name="cancellationToken">
    ///   Token that participates in cancelling the timer registration and is passed to the callback; use
    ///   <see cref="CancellationToken.None"/> when no external cancellation is required.
    /// </param>
    /// <param name="timerOptions">Optional day-time timer options.</param>
    /// <returns>An <see cref="IClockDayTimeTimer"/> to monitor or change the timer.</returns>
    IClockDayTimeTimer RegisterAsyncTimeOfDay (LocalTimeOfDay timeOfDay,
            Func<CancellationToken, ValueTask> callback,
            CancellationToken cancellationToken,
            DayTimeTimerOptions? timerOptions = null);
    /// <summary>
    ///   Registers a UTC time-of-day timer with a callback that receives context and state.
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
    /// <summary>
    ///   Registers a UTC time-of-day timer with a callback that receives context and cancellation token.
    /// </summary>
    /// <param name="timeOfDay">The UTC time of day at which to fire.</param>
    /// <param name="callback">The callback invoked when the timer fires; receives timer context and a cancellation token.</param>
    /// <param name="cancellationToken">
    ///   Token that participates in cancelling the timer registration; pass <see cref="CancellationToken.None"/> when
    ///   no external cancellation is required.
    /// </param>
    /// <param name="state">Optional state passed to the callback via <see cref="ClockTimerCallbackContext"/>.</param>
    /// <param name="timerOptions">Optional day-time timer options.</param>
    /// <returns>An <see cref="IClockDayTimeTimer"/> to monitor or change the timer.</returns>
    IClockDayTimeTimer RegisterTimeOfDay (UtcTimeOfDay timeOfDay,
            Action<ClockTimerCallbackContext, CancellationToken> callback,
            CancellationToken cancellationToken,
            object? state = null,
            DayTimeTimerOptions? timerOptions = null);
    /// <summary>
    ///   Registers a UTC time-of-day timer with an asynchronous callback that receives context and cancellation token.
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
    /// <summary>
    ///   Registers a time-of-day timer that fires at the given UTC time each day (sync callback).
    /// </summary>
    /// <param name="timeOfDay">The UTC time of day at which to fire.</param>
    /// <param name="callback">The callback to run when the timer fires.</param>
    /// <param name="cancellationToken">
    ///   Token that participates in cancelling the timer registration; pass <see cref="CancellationToken.None"/> when
    ///   no external cancellation is required.
    /// </param>
    /// <param name="timerOptions">Optional day-time timer options.</param>
    /// <returns>An <see cref="IClockDayTimeTimer"/> to monitor or change the timer.</returns>
    IClockDayTimeTimer RegisterTimeOfDay (UtcTimeOfDay timeOfDay,
            Action callback,
            CancellationToken cancellationToken,
            DayTimeTimerOptions? timerOptions = null);
    /// <summary>
    ///   Registers a UTC time-of-day timer with an asynchronous callback.
    /// </summary>
    /// <param name="timeOfDay">The UTC time of day at which to fire.</param>
    /// <param name="callback">The async callback (returns <see cref="ValueTask"/>).</param>
    /// <param name="cancellationToken">
    ///   Token that participates in cancelling the timer registration and is passed to the callback; use
    ///   <see cref="CancellationToken.None"/> when no external cancellation is required.
    /// </param>
    /// <param name="timerOptions">Optional day-time timer options.</param>
    /// <returns>An <see cref="IClockDayTimeTimer"/> to monitor or change the timer.</returns>
    IClockDayTimeTimer RegisterAsyncTimeOfDay (UtcTimeOfDay timeOfDay,
            Func<CancellationToken, ValueTask> callback,
            CancellationToken cancellationToken,
            DayTimeTimerOptions? timerOptions = null);

    #endregion IPrimeClock — Time-of-day timers (RegisterTimeOfDay)
#endif
}
//################################################################################
