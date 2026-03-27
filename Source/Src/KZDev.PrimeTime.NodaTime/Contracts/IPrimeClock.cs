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
public interface IPrimeClock : IPrimeTime
{
    #region IPrimeClock — Now (instant and zoned date and time)

    //--------------------------------------------------------------------------------
    /// <summary>
    ///   Gets the current instant on the global timeline (UTC).
    /// </summary>
    Instant Instant { [DebuggerStepThrough] get; }
    //--------------------------------------------------------------------------------
    /// <summary>
    ///   Gets the current local date and time in the system default time zone as a
    ///   <see cref="LocalDateTime"/> (no time zone information).
    /// </summary>
    LocalDateTime LocalNow { [DebuggerStepThrough] get; }
    //--------------------------------------------------------------------------------
    /// <summary>
    ///   Gets the current date and time in UTC as a <see cref="ZonedDateTime"/>.
    /// </summary>
    ZonedDateTime UtcNow { [DebuggerStepThrough] get; }
    //--------------------------------------------------------------------------------
    /// <summary>
    ///   Gets the current date and time in the system default time zone as a
    ///   <see cref="ZonedDateTime"/>.
    /// </summary>
    ZonedDateTime LocalZonedNow { [DebuggerStepThrough] get; }
    //--------------------------------------------------------------------------------
    /// <summary>
    ///   Gets the current date and time in UTC as a <see cref="ZonedDateTime"/>.
    ///   Equivalent to <see cref="UtcNow"/> for symmetry with <see cref="LocalZonedNow"/>.
    /// </summary>
    ZonedDateTime UtcZonedNow { [DebuggerStepThrough] get; }
    //--------------------------------------------------------------------------------

    #endregion IPrimeClock — Now (instant and zoned date and time)

    #region IPrimeClock — Now (time-only and date-only)

    //--------------------------------------------------------------------------------
    /// <summary>
    ///   Gets the current local time of day (no date component) in the system default zone.
    /// </summary>
    LocalTime LocalNowTime { [DebuggerStepThrough] get; }
    //--------------------------------------------------------------------------------
    /// <summary>
    ///   Gets the current UTC time of day (no date component).
    /// </summary>
    LocalTime UtcNowTime { [DebuggerStepThrough] get; }
    //--------------------------------------------------------------------------------
    /// <summary>
    ///   Gets the current local date (no time component) in the system default zone.
    /// </summary>
    LocalDate LocalNowDate { [DebuggerStepThrough] get; }
    //--------------------------------------------------------------------------------
    /// <summary>
    ///   Gets the current UTC date (no time component).
    /// </summary>
    LocalDate UtcNowDate { [DebuggerStepThrough] get; }
    //--------------------------------------------------------------------------------

    #endregion IPrimeClock — Now (time-only and date-only)

    #region IPrimeClock — Delays (Duration)

    //--------------------------------------------------------------------------------
    /// <summary>
    ///   Suspends the current thread for the specified <see cref="Duration"/>.
    /// </summary>
    /// <param name="duration">
    ///   The amount of time for which the thread is suspended. If zero or negative,
    ///   the thread relinquishes the remainder of its time slice (or returns immediately).
    /// </param>
    /// <remarks>
    ///   Implementations may clamp values: zero or negative is treated as zero; durations
    ///   greater than <see cref="TimeSpan.MaxValue"/> are clamped to
    ///   <see cref="TimeSpan.MaxValue"/> for the underlying sleep.
    /// </remarks>
    void Sleep (Duration duration);
    //--------------------------------------------------------------------------------
    /// <summary>
    ///   Asynchronously completes after the specified <see cref="Duration"/>.
    /// </summary>
    /// <param name="duration">
    ///   The duration to wait before the operation completes.
    /// </param>
    /// <remarks>
    ///   Implementations may clamp values: zero or negative is treated as zero; durations
    ///   greater than <see cref="TimeSpan.MaxValue"/> are clamped to
    ///   <see cref="TimeSpan.MaxValue"/> for the underlying delay.
    /// </remarks>
    Task DelayAsync (Duration duration);
    //--------------------------------------------------------------------------------
    /// <summary>
    ///   Asynchronously completes after the specified <see cref="Duration"/>, or ends early when
    ///   <paramref name="cancellationToken"/> is cancelled.
    /// </summary>
    /// <param name="duration">
    ///   The duration to wait before the operation completes.
    /// </param>
    /// <param name="cancellationToken">
    ///   A cancellation token to observe while waiting for the task to complete.
    /// </param>
    /// <remarks>
    ///   Implementations may clamp values: zero or negative is treated as zero; durations
    ///   greater than <see cref="TimeSpan.MaxValue"/> are clamped to
    ///   <see cref="TimeSpan.MaxValue"/> for the underlying delay.
    /// </remarks>
    /// <exception cref="OperationCanceledException">
    ///   The operation was canceled via <paramref name="cancellationToken"/>.
    /// </exception>
    Task DelayAsync (Duration duration, CancellationToken cancellationToken);
    //--------------------------------------------------------------------------------

    #endregion IPrimeClock — Delays (Duration)

    #region IPrimeClock — Time cancellation (Duration)

    //--------------------------------------------------------------------------------
    /// <summary>
    ///   Returns a disposable wrapper whose token expires after the specified
    ///   <see cref="Duration"/>. The caller is responsible for disposing the returned
    ///   instance when no longer needed.
    /// </summary>
    /// <param name="cancelAfter">
    ///   The duration to wait before cancelling the token.
    /// </param>
    /// <returns>
    ///   A <see cref="TimeCancellationTokenSource"/> that will cancel after the specified
    ///   duration. Caller must dispose.
    /// </returns>
    /// <remarks>
    ///   Implementations may clamp values: zero or negative is treated as zero; durations
    ///   greater than <see cref="int.MaxValue"/> milliseconds are clamped to that value,
    ///   which is the maximum delay supported by <see cref="CancellationTokenSource"/>.
    /// </remarks>
    TimeCancellationTokenSource GetTimeCancellationToken (Duration cancelAfter);
    //--------------------------------------------------------------------------------
    /// <summary>
    ///   Returns a disposable wrapper whose token is cancelled when either the specified
    ///   duration elapses or the given cancellation token is cancelled. The caller is
    ///   responsible for disposing the returned instance when no longer needed.
    /// </summary>
    /// <param name="cancelAfter">
    ///   The duration to wait before cancelling the token.
    /// </param>
    /// <param name="cancellationToken">
    ///   The cancellation token to link.
    /// </param>
    /// <returns>
    ///   A <see cref="TimeCancellationTokenSource"/>. Caller must dispose.
    /// </returns>
    /// <remarks>
    ///   Implementations may clamp values: zero or negative is treated as zero; durations
    ///   greater than <see cref="int.MaxValue"/> milliseconds are clamped to that value,
    ///   which is the maximum delay supported by <see cref="CancellationTokenSource"/>.
    /// </remarks>
    TimeCancellationTokenSource LinkTimeCancellationToken (Duration cancelAfter, CancellationToken cancellationToken);
    //--------------------------------------------------------------------------------
    /// <summary>
    ///   Returns a disposable wrapper whose token is cancelled when the specified
    ///   duration elapses or any of the given tokens is cancelled. The caller is
    ///   responsible for disposing the returned instance when no longer needed.
    /// </summary>
    /// <param name="cancelAfter">
    ///   The duration to wait before cancelling the token.
    /// </param>
    /// <param name="token1">
    ///   The first cancellation token to link.
    /// </param>
    /// <param name="token2">
    ///   The second cancellation token to link.
    /// </param>
    /// <returns>
    ///   A <see cref="TimeCancellationTokenSource"/>. Caller must dispose.
    /// </returns>
    /// <remarks>
    ///   Implementations may clamp values: zero or negative is treated as zero; durations
    ///   greater than <see cref="int.MaxValue"/> milliseconds are clamped to that value,
    ///   which is the maximum delay supported by <see cref="CancellationTokenSource"/>.
    /// </remarks>
    TimeCancellationTokenSource LinkTimeCancellationToken (Duration cancelAfter, CancellationToken token1,
        CancellationToken token2);
    //--------------------------------------------------------------------------------
    /// <summary>
    ///   Returns a disposable wrapper whose token is cancelled when the specified
    ///   duration elapses or any of the given tokens is cancelled. The caller is
    ///   responsible for disposing the returned instance when no longer needed.
    /// </summary>
    /// <param name="cancelAfter">
    ///   The duration to wait before cancelling the token.
    /// </param>
    /// <param name="cancellationTokens">
    ///   The cancellation tokens to link.
    /// </param>
    /// <returns>
    ///   A <see cref="TimeCancellationTokenSource"/>. Caller must dispose.
    /// </returns>
    /// <remarks>
    ///   Implementations may clamp values: zero or negative is treated as zero; durations
    ///   greater than <see cref="int.MaxValue"/> milliseconds are clamped to that value,
    ///   which is the maximum delay supported by <see cref="CancellationTokenSource"/>.
    /// </remarks>
    TimeCancellationTokenSource LinkTimeCancellationToken (Duration cancelAfter,
        params CancellationToken[] cancellationTokens);
    //--------------------------------------------------------------------------------

    #endregion IPrimeClock — Time cancellation (Duration)

    #region IPrimeClock — Interval timers (RegisterTimer)

    //--------------------------------------------------------------------------------
    /// <summary>
    ///   Registers a one-shot or repeating interval timer with a synchronous callback.
    /// </summary>
    /// <param name="callbackTime">Duration until the first callback.</param>
    /// <param name="callback">The callback to run when the timer fires.</param>
    /// <param name="repeat">If <c>true</c>, repeat using <paramref name="callbackTime"/> as the interval.</param>
    /// <param name="timerOptions">Optional timer options (e.g. reset-after-callback, execution context).</param>
    /// <param name="cancellationToken">
    ///   Token that participates in cancelling the timer registration; pass <see cref="CancellationToken.None"/> when
    ///   no external cancellation is required.
    /// </param>
    /// <returns>An <see cref="IClockIntervalTimer"/> to monitor or change the timer.</returns>
    IClockIntervalTimer RegisterTimer (Duration callbackTime,
        Action callback,
        CancellationToken cancellationToken,
        bool repeat = false,
        IntervalTimerOptions? timerOptions = null);
    //--------------------------------------------------------------------------------
    /// <summary>
    ///   Registers a one-shot or repeating interval timer with a synchronous callback that receives context and state.
    /// </summary>
    /// <param name="callbackTime">Duration until the first callback.</param>
    /// <param name="callback">The callback invoked when the timer fires; receives timer context.</param>
    /// <param name="cancellationToken">
    ///   Token that participates in cancelling the timer registration; pass <see cref="CancellationToken.None"/> when
    ///   no external cancellation is required.
    /// </param>
    /// <param name="state">Optional state passed to the callback via <see cref="PrimeClockTimerCallbackContext"/>.</param>
    /// <param name="repeat">If <c>true</c>, repeat using <paramref name="callbackTime"/> as the interval.</param>
    /// <param name="timerOptions">Optional timer options (e.g. reset-after-callback, execution context).</param>
    /// <returns>An <see cref="IClockIntervalTimer"/> to monitor or change the timer.</returns>
    IClockIntervalTimer RegisterTimer (Duration callbackTime,
        Action<PrimeClockTimerCallbackContext> callback,
        CancellationToken cancellationToken,
        object? state = null,
        bool repeat = false,
        IntervalTimerOptions? timerOptions = null);
    //--------------------------------------------------------------------------------
    /// <summary>
    ///   Registers a one-shot or repeating interval timer with a synchronous callback that receives context and cancellation token.
    /// </summary>
    /// <param name="callbackTime">Duration until the first callback.</param>
    /// <param name="callback">The callback invoked when the timer fires; receives timer context and a cancellation token.</param>
    /// <param name="cancellationToken">
    ///   Token that participates in cancelling the timer registration; pass <see cref="CancellationToken.None"/> when
    ///   no external cancellation is required.
    /// </param>
    /// <param name="state">Optional state passed to the callback via <see cref="PrimeClockTimerCallbackContext"/>.</param>
    /// <param name="repeat">If <c>true</c>, repeat using <paramref name="callbackTime"/> as the interval.</param>
    /// <param name="timerOptions">Optional timer options (e.g. reset-after-callback, execution context).</param>
    /// <returns>An <see cref="IClockIntervalTimer"/> to monitor or change the timer.</returns>
    IClockIntervalTimer RegisterTimer (Duration callbackTime,
        Action<PrimeClockTimerCallbackContext, CancellationToken> callback,
        CancellationToken cancellationToken,
        object? state = null,
        bool repeat = false,
        IntervalTimerOptions? timerOptions = null);
    //--------------------------------------------------------------------------------
    /// <summary>
    ///   Registers a one-shot or repeating interval timer with an asynchronous callback.
    /// </summary>
    /// <param name="callbackTime">Duration until the first callback.</param>
    /// <param name="callback">The async callback (returns <see cref="ValueTask"/>).</param>
    /// <param name="repeat">If <c>true</c>, repeat using <paramref name="callbackTime"/> as the interval.</param>
    /// <param name="timerOptions">Optional timer options.</param>
    /// <param name="cancellationToken">
    ///   Token that participates in cancelling the timer registration and is passed to the callback; use
    ///   <see cref="CancellationToken.None"/> when no external cancellation is required.
    /// </param>
    /// <returns>An <see cref="IClockIntervalTimer"/> to monitor or change the timer.</returns>
    IClockIntervalTimer RegisterAsyncTimer (Duration callbackTime,
        Func<CancellationToken, ValueTask> callback,
        CancellationToken cancellationToken,
        bool repeat = false,
        IntervalTimerOptions? timerOptions = null);
    //--------------------------------------------------------------------------------
    /// <summary>
    ///   Registers a one-shot or repeating interval timer with an asynchronous callback that receives context and cancellation token.
    /// </summary>
    /// <param name="callbackTime">Duration until the first callback.</param>
    /// <param name="callback">The async callback; receives timer context and a cancellation token.</param>
    /// <param name="cancellationToken">
    ///   Token that participates in cancelling the timer registration and is passed to the callback; use
    ///   <see cref="CancellationToken.None"/> when no external cancellation is required.
    /// </param>
    /// <param name="state">Optional state passed to the callback via <see cref="PrimeClockTimerCallbackContext"/>.</param>
    /// <param name="repeat">If <c>true</c>, repeat using <paramref name="callbackTime"/> as the interval.</param>
    /// <param name="timerOptions">Optional timer options.</param>
    /// <returns>An <see cref="IClockIntervalTimer"/> to monitor or change the timer.</returns>
    IClockIntervalTimer RegisterAsyncTimer (Duration callbackTime,
        Func<PrimeClockTimerCallbackContext, CancellationToken, ValueTask> callback,
        CancellationToken cancellationToken,
        object? state = null,
        bool repeat = false,
        IntervalTimerOptions? timerOptions = null);
    //--------------------------------------------------------------------------------
    /// <summary>
    ///   Registers an interval timer with an initial delay and a separate repeat interval (sync callback).
    /// </summary>
    /// <param name="callbackTime">Duration until the first callback.</param>
    /// <param name="repeatInterval">Interval for subsequent callbacks; use a non-positive duration for one-shot.</param>
    /// <param name="callback">The callback to run when the timer fires.</param>
    /// <param name="timerOptions">Optional timer options.</param>
    /// <param name="cancellationToken">
    ///   Token that participates in cancelling the timer registration; pass <see cref="CancellationToken.None"/> when
    ///   no external cancellation is required.
    /// </param>
    /// <returns>An <see cref="IClockIntervalTimer"/> to monitor or change the timer.</returns>
    IClockIntervalTimer RegisterTimer (Duration callbackTime,
        Duration repeatInterval,
        Action callback,
        CancellationToken cancellationToken,
        IntervalTimerOptions? timerOptions = null);
    //--------------------------------------------------------------------------------
    /// <summary>
    ///   Registers an interval timer with an initial delay and a separate repeat interval (sync callback with context).
    /// </summary>
    /// <param name="callbackTime">Duration until the first callback.</param>
    /// <param name="repeatInterval">Interval for subsequent callbacks; use a non-positive duration for one-shot.</param>
    /// <param name="callback">The callback invoked when the timer fires; receives timer context.</param>
    /// <param name="cancellationToken">
    ///   Token that participates in cancelling the timer registration; pass <see cref="CancellationToken.None"/> when
    ///   no external cancellation is required.
    /// </param>
    /// <param name="state">Optional state passed to the callback via <see cref="PrimeClockTimerCallbackContext"/>.</param>
    /// <param name="timerOptions">Optional timer options.</param>
    /// <returns>An <see cref="IClockIntervalTimer"/> to monitor or change the timer.</returns>
    IClockIntervalTimer RegisterTimer (Duration callbackTime,
        Duration repeatInterval,
        Action<PrimeClockTimerCallbackContext> callback,
        CancellationToken cancellationToken,
        object? state = null,
        IntervalTimerOptions? timerOptions = null);
    //--------------------------------------------------------------------------------
    /// <summary>
    ///   Registers an interval timer with an initial delay and a separate repeat interval (sync callback with context and token).
    /// </summary>
    /// <param name="callbackTime">Duration until the first callback.</param>
    /// <param name="repeatInterval">Interval for subsequent callbacks; use a non-positive duration for one-shot.</param>
    /// <param name="callback">The callback invoked when the timer fires; receives timer context and a cancellation token.</param>
    /// <param name="cancellationToken">
    ///   Token that participates in cancelling the timer registration; pass <see cref="CancellationToken.None"/> when
    ///   no external cancellation is required.
    /// </param>
    /// <param name="state">Optional state passed to the callback via <see cref="PrimeClockTimerCallbackContext"/>.</param>
    /// <param name="timerOptions">Optional timer options.</param>
    /// <returns>An <see cref="IClockIntervalTimer"/> to monitor or change the timer.</returns>
    IClockIntervalTimer RegisterTimer (Duration callbackTime,
        Duration repeatInterval,
        Action<PrimeClockTimerCallbackContext, CancellationToken> callback,
        CancellationToken cancellationToken,
        object? state = null,
        IntervalTimerOptions? timerOptions = null);
    //--------------------------------------------------------------------------------
    /// <summary>
    ///   Registers an interval timer with an initial delay and a separate repeat interval (async callback).
    /// </summary>
    /// <param name="callbackTime">Duration until the first callback.</param>
    /// <param name="repeatInterval">Interval for subsequent callbacks; use a non-positive duration for one-shot.</param>
    /// <param name="callback">The async callback (returns <see cref="ValueTask"/>).</param>
    /// <param name="cancellationToken">
    ///   Token that participates in cancelling the timer registration and is passed to the callback; use
    ///   <see cref="CancellationToken.None"/> when no external cancellation is required.
    /// </param>
    /// <param name="timerOptions">Optional timer options.</param>
    /// <returns>An <see cref="IClockIntervalTimer"/> to monitor or change the timer.</returns>
    IClockIntervalTimer RegisterAsyncTimer (Duration callbackTime,
        Duration repeatInterval,
        Func<CancellationToken, ValueTask> callback,
        CancellationToken cancellationToken,
        IntervalTimerOptions? timerOptions = null);
    //--------------------------------------------------------------------------------
    /// <summary>
    ///   Registers an interval timer with an initial delay and a separate repeat interval (async callback with context).
    /// </summary>
    /// <param name="callbackTime">Duration until the first callback.</param>
    /// <param name="repeatInterval">Interval for subsequent callbacks; use a non-positive duration for one-shot.</param>
    /// <param name="callback">The async callback; receives timer context and a cancellation token.</param>
    /// <param name="cancellationToken">
    ///   Token that participates in cancelling the timer registration and is passed to the callback; use
    ///   <see cref="CancellationToken.None"/> when no external cancellation is required.
    /// </param>
    /// <param name="state">Optional state passed to the callback via <see cref="PrimeClockTimerCallbackContext"/>.</param>
    /// <param name="timerOptions">Optional timer options.</param>
    /// <returns>An <see cref="IClockIntervalTimer"/> to monitor or change the timer.</returns>
    IClockIntervalTimer RegisterAsyncTimer (Duration callbackTime,
        Duration repeatInterval,
        Func<PrimeClockTimerCallbackContext, CancellationToken, ValueTask> callback,
        CancellationToken cancellationToken,
        object? state = null,
        IntervalTimerOptions? timerOptions = null);
    //--------------------------------------------------------------------------------

    #endregion IPrimeClock — Interval timers (RegisterTimer)

    #region IPrimeClock — Time-of-day timers (RegisterTimeOfDay)

#if NET
    //--------------------------------------------------------------------------------
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
    /// <returns>
    ///   An <see cref="IClockDayTimeTimer"/> supporting change operations via NodaTime
    ///   <see cref="LocalTime"/> and BCL <see cref="LocalTimeOfDay"/>.
    /// </returns>
    IClockDayTimeTimer RegisterTimeOfDay (LocalTime timeOfDay,
        Action callback,
        CancellationToken cancellationToken,
        DayTimeTimerOptions? timerOptions = null);
    //--------------------------------------------------------------------------------
    /// <summary>
    ///   Registers a time-of-day timer with a callback that receives context and state.
    /// </summary>
    /// <param name="timeOfDay">The local time of day at which to fire.</param>
    /// <param name="callback">The callback invoked when the timer fires; receives timer context.</param>
    /// <param name="cancellationToken">
    ///   Token that participates in cancelling the timer registration; pass <see cref="CancellationToken.None"/> when
    ///   no external cancellation is required.
    /// </param>
    /// <param name="state">Optional state passed to the callback via <see cref="PrimeClockTimerCallbackContext"/>.</param>
    /// <param name="timerOptions">Optional day-time timer options.</param>
    /// <returns>An <see cref="IClockDayTimeTimer"/> supporting NodaTime <see cref="LocalTime"/> change overloads.</returns>
    IClockDayTimeTimer RegisterTimeOfDay (LocalTime timeOfDay,
        Action<PrimeClockTimerCallbackContext> callback,
        CancellationToken cancellationToken,
        object? state = null,
        DayTimeTimerOptions? timerOptions = null);
    //--------------------------------------------------------------------------------
    /// <summary>
    ///   Registers a time-of-day timer with a callback that receives context and cancellation token.
    /// </summary>
    /// <param name="timeOfDay">The local time of day at which to fire.</param>
    /// <param name="callback">The callback invoked when the timer fires; receives timer context and a cancellation token.</param>
    /// <param name="cancellationToken">
    ///   Token that participates in cancelling the timer registration; pass <see cref="CancellationToken.None"/> when
    ///   no external cancellation is required.
    /// </param>
    /// <param name="state">Optional state passed to the callback via <see cref="PrimeClockTimerCallbackContext"/>.</param>
    /// <param name="timerOptions">Optional day-time timer options.</param>
    /// <returns>An <see cref="IClockDayTimeTimer"/> supporting NodaTime <see cref="LocalTime"/> change overloads.</returns>
    IClockDayTimeTimer RegisterTimeOfDay (LocalTime timeOfDay,
        Action<PrimeClockTimerCallbackContext, CancellationToken> callback,
        CancellationToken cancellationToken,
        object? state = null,
        DayTimeTimerOptions? timerOptions = null);
    //--------------------------------------------------------------------------------
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
    /// <returns>An <see cref="IClockDayTimeTimer"/> supporting NodaTime <see cref="LocalTime"/> change overloads.</returns>
    IClockDayTimeTimer RegisterAsyncTimeOfDay (LocalTime timeOfDay,
        Func<CancellationToken, ValueTask> callback,
        CancellationToken cancellationToken,
        DayTimeTimerOptions? timerOptions = null);
    //--------------------------------------------------------------------------------
    /// <summary>
    ///   Registers a time-of-day timer with an asynchronous callback that receives context and cancellation token.
    /// </summary>
    /// <param name="timeOfDay">The local time of day at which to fire.</param>
    /// <param name="callback">The async callback; receives timer context and a cancellation token.</param>
    /// <param name="cancellationToken">
    ///   Token that participates in cancelling the timer registration and is passed to the callback; use
    ///   <see cref="CancellationToken.None"/> when no external cancellation is required.
    /// </param>
    /// <param name="state">Optional state passed to the callback via <see cref="PrimeClockTimerCallbackContext"/>.</param>
    /// <param name="timerOptions">Optional day-time timer options.</param>
    /// <returns>An <see cref="IClockDayTimeTimer"/> supporting NodaTime <see cref="LocalTime"/> change overloads.</returns>
    IClockDayTimeTimer RegisterAsyncTimeOfDay (LocalTime timeOfDay,
        Func<PrimeClockTimerCallbackContext, CancellationToken, ValueTask> callback,
        CancellationToken cancellationToken,
        object? state = null,
        DayTimeTimerOptions? timerOptions = null);
    //--------------------------------------------------------------------------------
#else
    //--------------------------------------------------------------------------------
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
    /// <returns>An <see cref="IClockDayTimeTimer"/> handle.</returns>
    IClockDayTimeTimer RegisterTimeOfDay (LocalTime timeOfDay,
        Action callback,
        CancellationToken cancellationToken,
        DayTimeTimerOptions? timerOptions = null);
    //--------------------------------------------------------------------------------
    /// <summary>
    ///   Registers a time-of-day timer with a callback that receives context and state.
    /// </summary>
    IClockDayTimeTimer RegisterTimeOfDay (LocalTime timeOfDay,
        Action<PrimeClockTimerCallbackContext> callback,
        CancellationToken cancellationToken,
        object? state = null,
        DayTimeTimerOptions? timerOptions = null);
    //--------------------------------------------------------------------------------
    /// <summary>
    ///   Registers a time-of-day timer with a callback that receives context and cancellation token.
    /// </summary>
    IClockDayTimeTimer RegisterTimeOfDay (LocalTime timeOfDay,
        Action<PrimeClockTimerCallbackContext, CancellationToken> callback,
        CancellationToken cancellationToken,
        object? state = null,
        DayTimeTimerOptions? timerOptions = null);
    //--------------------------------------------------------------------------------
    /// <summary>
    ///   Registers a time-of-day timer with an asynchronous callback.
    /// </summary>
    IClockDayTimeTimer RegisterAsyncTimeOfDay (LocalTime timeOfDay,
        Func<CancellationToken, ValueTask> callback,
        CancellationToken cancellationToken,
        DayTimeTimerOptions? timerOptions = null);
    //--------------------------------------------------------------------------------
    /// <summary>
    ///   Registers a time-of-day timer with an asynchronous callback that receives context and cancellation token.
    /// </summary>
    IClockDayTimeTimer RegisterAsyncTimeOfDay (LocalTime timeOfDay,
        Func<PrimeClockTimerCallbackContext, CancellationToken, ValueTask> callback,
        CancellationToken cancellationToken,
        object? state = null,
        DayTimeTimerOptions? timerOptions = null);
    //--------------------------------------------------------------------------------
#endif

    #endregion IPrimeClock — Time-of-day timers (RegisterTimeOfDay)
}
//################################################################################
