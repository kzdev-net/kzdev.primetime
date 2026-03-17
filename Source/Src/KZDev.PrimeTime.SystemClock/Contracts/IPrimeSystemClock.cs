namespace KZDev.PrimeTime;

//################################################################################
    /// <summary>
    ///   Extends <see cref="IPrimeTime"/> with BCL-based "now" APIs so callers can obtain current
    ///   time from the clock abstraction using <see cref="DateTimeOffset"/> and <see cref="DateTime"/>.
    ///   On .NET 6 and later, time-only and date-only "now" members (LocalNowTime, UtcNowTime,
    ///   LocalNowDate, UtcNowDate) are also available.
    /// </summary>
public interface IPrimeSystemClock : IPrimeTime
{
    #region IPrimeSystemClock — Now (date and time)

    //--------------------------------------------------------------------------------
    /// <summary>
    ///   Gets the current local date and time as a <see cref="DateTimeOffset"/>.
    /// </summary>
    DateTimeOffset LocalNow { get; }
    //--------------------------------------------------------------------------------
    /// <summary>
    ///   Gets the current UTC date and time as a <see cref="DateTimeOffset"/>.
    /// </summary>
    DateTimeOffset UtcNow { get; }
    //--------------------------------------------------------------------------------
    /// <summary>
    ///   Gets the current local date and time as a <see cref="DateTime"/> with
    ///   <see cref="DateTime.Kind"/> equal to <see cref="DateTimeKind.Local"/>.
    /// </summary>
    DateTime LocalDateTimeNow { get; }
    //--------------------------------------------------------------------------------
    /// <summary>
    ///   Gets the current UTC date and time as a <see cref="DateTime"/> with
    ///   <see cref="DateTime.Kind"/> equal to <see cref="DateTimeKind.Utc"/>.
    /// </summary>
    DateTime UtcDateTimeNow { get; }
    //--------------------------------------------------------------------------------

    #endregion IPrimeSystemClock — Now (date and time)

#if NET
    #region IPrimeSystemClock — Now (time-only and date-only)

    //--------------------------------------------------------------------------------
    /// <summary>
    ///   Gets the current local time of day (no date component).
    /// </summary>
    TimeOnly LocalNowTime { get; }
    //--------------------------------------------------------------------------------
    /// <summary>
    ///   Gets the current UTC time of day (no date component).
    /// </summary>
    TimeOnly UtcNowTime { get; }
    //--------------------------------------------------------------------------------
    /// <summary>
    ///   Gets the current local date (no time component).
    /// </summary>
    DateOnly LocalNowDate { get; }
    //--------------------------------------------------------------------------------
    /// <summary>
    ///   Gets the current UTC date (no time component).
    /// </summary>
    DateOnly UtcNowDate { get; }
    //--------------------------------------------------------------------------------

    #endregion IPrimeSystemClock — Now (time-only and date-only)
#endif

    #region IPrimeSystemClock — Interval timers

    //--------------------------------------------------------------------------------
    /// <summary>
    ///   Registers a one-shot or repeating interval timer with a synchronous callback.
    /// </summary>
    /// <param name="callbackTime">Time until the first callback.</param>
    /// <param name="callback">The callback to run when the timer fires.</param>
    /// <param name="repeat">If <c>true</c>, repeat using <paramref name="callbackTime"/> as the interval.</param>
    /// <param name="timerOptions">Optional timer options (e.g. reset-after-callback, execution context).</param>
    /// <param name="cancellationToken">Optional token to cancel the timer.</param>
    /// <returns>An <see cref="IClockIntervalTimer"/> to monitor or change the timer.</returns>
    IClockIntervalTimer RegisterTimer (TimeSpan callbackTime, Action callback,
            bool repeat = false, IntervalTimerOptions? timerOptions = null,
            CancellationToken cancellationToken = default);
    //--------------------------------------------------------------------------------
    /// <summary>
    ///   Registers a one-shot or repeating interval timer with a synchronous callback that receives context and state.
    /// </summary>
    IClockIntervalTimer RegisterTimer (TimeSpan callbackTime,
            Action<ClockTimerCallbackContext> callback, object? state = null,
            bool repeat = false, IntervalTimerOptions? timerOptions = null,
            CancellationToken cancellationToken = default);
    //--------------------------------------------------------------------------------
    /// <summary>
    ///   Registers a one-shot or repeating interval timer with a synchronous callback that receives context and cancellation token.
    /// </summary>
    IClockIntervalTimer RegisterTimer (TimeSpan callbackTime,
            Action<ClockTimerCallbackContext, CancellationToken> callback,
            object? state = null,
            bool repeat = false,
            IntervalTimerOptions? timerOptions = null,
            CancellationToken cancellationToken = default);
    //--------------------------------------------------------------------------------
    /// <summary>
    ///   Registers a one-shot or repeating interval timer with an asynchronous callback.
    /// </summary>
    /// <param name="callbackTime">Time until the first callback.</param>
    /// <param name="callback">The async callback (returns <see cref="ValueTask"/>).</param>
    /// <param name="repeat">If <c>true</c>, repeat using <paramref name="callbackTime"/> as the interval.</param>
    /// <param name="timerOptions">Optional timer options.</param>
    /// <param name="cancellationToken">Optional token to cancel the timer; also passed to the callback.</param>
    /// <returns>An <see cref="IClockIntervalTimer"/> to monitor or change the timer.</returns>
    IClockIntervalTimer RegisterAsyncTimer (TimeSpan callbackTime,
            Func<CancellationToken, ValueTask> callback,
            bool repeat = false,
            IntervalTimerOptions? timerOptions = null,
            CancellationToken cancellationToken = default);
    //--------------------------------------------------------------------------------
    /// <summary>
    ///   Registers a one-shot or repeating interval timer with an asynchronous callback that receives context and cancellation token.
    /// </summary>
    IClockIntervalTimer RegisterAsyncTimer (TimeSpan callbackTime,
            Func<ClockTimerCallbackContext, CancellationToken, ValueTask> callback,
            object? state = null,
            bool repeat = false,
            IntervalTimerOptions? timerOptions = null,
            CancellationToken cancellationToken = default);
    //--------------------------------------------------------------------------------
    /// <summary>
    ///   Registers an interval timer with an initial delay and a separate repeat interval (sync callback).
    /// </summary>
    /// <param name="callbackTime">Time until the first callback.</param>
    /// <param name="repeatInterval">Interval for subsequent callbacks; <see cref="Timeout.InfiniteTimeSpan"/> for one-shot.</param>
    /// <param name="callback">The callback to run when the timer fires.</param>
    /// <param name="timerOptions">Optional timer options.</param>
    /// <param name="cancellationToken">Optional token to cancel the timer.</param>
    IClockIntervalTimer RegisterTimer (TimeSpan callbackTime,
        TimeSpan repeatInterval,
            Action callback,
            IntervalTimerOptions? timerOptions = null,
            CancellationToken cancellationToken = default);
    //--------------------------------------------------------------------------------
    /// <summary>
    ///   Registers an interval timer with an initial delay and a separate repeat interval (sync callback with context).
    /// </summary>
    IClockIntervalTimer RegisterTimer (TimeSpan callbackTime,
        TimeSpan repeatInterval,
            Action<ClockTimerCallbackContext> callback,
            object? state = null,
            IntervalTimerOptions? timerOptions = null,
            CancellationToken cancellationToken = default);
    //--------------------------------------------------------------------------------
    /// <summary>
    ///   Registers an interval timer with an initial delay and a separate repeat interval (sync callback with context and token).
    /// </summary>
    IClockIntervalTimer RegisterTimer (TimeSpan callbackTime,
        TimeSpan repeatInterval,
            Action<ClockTimerCallbackContext, CancellationToken> callback,
            object? state = null,
            IntervalTimerOptions? timerOptions = null,
            CancellationToken cancellationToken = default);
    //--------------------------------------------------------------------------------
    /// <summary>
    ///   Registers an interval timer with an initial delay and a separate repeat interval (async callback).
    /// </summary>
    IClockIntervalTimer RegisterAsyncTimer (TimeSpan callbackTime,
        TimeSpan repeatInterval,
            Func<CancellationToken, ValueTask> callback,
            IntervalTimerOptions? timerOptions = null,
            CancellationToken cancellationToken = default);
    //--------------------------------------------------------------------------------
    /// <summary>
    ///   Registers an interval timer with an initial delay and a separate repeat interval (async callback with context).
    /// </summary>
    IClockIntervalTimer RegisterAsyncTimer (TimeSpan callbackTime,
        TimeSpan repeatInterval,
            Func<ClockTimerCallbackContext, CancellationToken, ValueTask> callback,
            object? state = null,
            IntervalTimerOptions? timerOptions = null,
            CancellationToken cancellationToken = default);
    //--------------------------------------------------------------------------------

    #endregion IPrimeSystemClock — Interval timers

#if NET
    #region IPrimeSystemClock — Day-time timers

    //--------------------------------------------------------------------------------
    /// <summary>
    ///   Registers a time-of-day timer that fires at the given local time each day (sync callback).
    /// </summary>
    /// <param name="timeOfDay">The local time of day at which to fire.</param>
    /// <param name="callback">The callback to run when the timer fires.</param>
    /// <param name="timerOptions">Optional day-time timer options.</param>
    /// <param name="cancellationToken">Optional token to cancel the timer.</param>
    /// <returns>An <see cref="IClockDayTimeTimer"/> to monitor or change the timer.</returns>
    IClockDayTimeTimer RegisterTimeOfDay (LocalTimeOfDay timeOfDay,
            Action callback,
            DayTimeTimerOptions? timerOptions = null,
            CancellationToken cancellationToken = default);
    //--------------------------------------------------------------------------------
    /// <summary>
    ///   Registers a time-of-day timer with a callback that receives context and state.
    /// </summary>
    IClockDayTimeTimer RegisterTimeOfDay (LocalTimeOfDay timeOfDay,
            Action<ClockTimerCallbackContext> callback,
            object? state = null,
            DayTimeTimerOptions? timerOptions = null,
            CancellationToken cancellationToken = default);
    //--------------------------------------------------------------------------------
    /// <summary>
    ///   Registers a time-of-day timer with a callback that receives context and cancellation token.
    /// </summary>
    IClockDayTimeTimer RegisterTimeOfDay (LocalTimeOfDay timeOfDay,
            Action<ClockTimerCallbackContext, CancellationToken> callback,
            object? state = null,
            DayTimeTimerOptions? timerOptions = null,
            CancellationToken cancellationToken = default);
    //--------------------------------------------------------------------------------
    /// <summary>
    ///   Registers a time-of-day timer with an asynchronous callback.
    /// </summary>
    IClockDayTimeTimer RegisterAsyncTimeOfDay (LocalTimeOfDay timeOfDay,
            Func<CancellationToken, ValueTask> callback,
            DayTimeTimerOptions? timerOptions = null,
            CancellationToken cancellationToken = default);
    //--------------------------------------------------------------------------------
    /// <summary>
    ///   Registers a time-of-day timer with an asynchronous callback that receives context and cancellation token.
    /// </summary>
    IClockDayTimeTimer RegisterAsyncTimeOfDay (LocalTimeOfDay timeOfDay,
            Func<ClockTimerCallbackContext, CancellationToken, ValueTask> callback,
            object? state = null,
            DayTimeTimerOptions? timerOptions = null,
            CancellationToken cancellationToken = default);
    //--------------------------------------------------------------------------------
    /// <summary>
    ///   Registers a time-of-day timer that fires at the given UTC time each day (sync callback).
    /// </summary>
    IClockDayTimeTimer RegisterTimeOfDay (UtcTimeOfDay timeOfDay,
            Action callback,
            DayTimeTimerOptions? timerOptions = null,
            CancellationToken cancellationToken = default);
    //--------------------------------------------------------------------------------
    /// <summary>
    ///   Registers a UTC time-of-day timer with a callback that receives context and state.
    /// </summary>
    IClockDayTimeTimer RegisterTimeOfDay (UtcTimeOfDay timeOfDay,
            Action<ClockTimerCallbackContext> callback,
            object? state = null,
            DayTimeTimerOptions? timerOptions = null,
            CancellationToken cancellationToken = default);
    //--------------------------------------------------------------------------------
    /// <summary>
    ///   Registers a UTC time-of-day timer with a callback that receives context and cancellation token.
    /// </summary>
    IClockDayTimeTimer RegisterTimeOfDay (UtcTimeOfDay timeOfDay,
            Action<ClockTimerCallbackContext, CancellationToken> callback,
            object? state = null,
            DayTimeTimerOptions? timerOptions = null,
            CancellationToken cancellationToken = default);
    //--------------------------------------------------------------------------------
    /// <summary>
    ///   Registers a UTC time-of-day timer with an asynchronous callback.
    /// </summary>
    IClockDayTimeTimer RegisterAsyncTimeOfDay (UtcTimeOfDay timeOfDay,
            Func<CancellationToken, ValueTask> callback,
            DayTimeTimerOptions? timerOptions = null,
            CancellationToken cancellationToken = default);
    //--------------------------------------------------------------------------------
    /// <summary>
    ///   Registers a UTC time-of-day timer with an asynchronous callback that receives context and cancellation token.
    /// </summary>
    IClockDayTimeTimer RegisterAsyncTimeOfDay (UtcTimeOfDay timeOfDay,
            Func<ClockTimerCallbackContext, CancellationToken, ValueTask> callback,
            object? state = null,
            DayTimeTimerOptions? timerOptions = null,
            CancellationToken cancellationToken = default);
    //--------------------------------------------------------------------------------

    #endregion IPrimeSystemClock — Day-time timers
#endif
}
//################################################################################
