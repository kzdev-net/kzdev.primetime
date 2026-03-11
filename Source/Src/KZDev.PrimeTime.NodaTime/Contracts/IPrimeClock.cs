using NodaTime;

namespace KZDev.PrimeTime
{
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
        Instant Instant { get; }
        //--------------------------------------------------------------------------------
        /// <summary>
        ///   Gets the current local date and time in the system default time zone as a
        ///   <see cref="LocalDateTime"/> (no time zone information).
        /// </summary>
        LocalDateTime LocalNow { get; }
        //--------------------------------------------------------------------------------
        /// <summary>
        ///   Gets the current date and time in UTC as a <see cref="ZonedDateTime"/>.
        /// </summary>
        ZonedDateTime UtcNow { get; }
        //--------------------------------------------------------------------------------
        /// <summary>
        ///   Gets the current date and time in the system default time zone as a
        ///   <see cref="ZonedDateTime"/>.
        /// </summary>
        ZonedDateTime LocalZonedNow { get; }
        //--------------------------------------------------------------------------------
        /// <summary>
        ///   Gets the current date and time in UTC as a <see cref="ZonedDateTime"/>.
        ///   Equivalent to <see cref="UtcNow"/> for symmetry with <see cref="LocalZonedNow"/>.
        /// </summary>
        ZonedDateTime UtcZonedNow { get; }
        //--------------------------------------------------------------------------------

        #endregion IPrimeClock — Now (instant and zoned date and time)

        #region IPrimeClock — Now (time-only and date-only)

        //--------------------------------------------------------------------------------
        /// <summary>
        ///   Gets the current local time of day (no date component) in the system default zone.
        /// </summary>
        LocalTime LocalNowTime { get; }
        //--------------------------------------------------------------------------------
        /// <summary>
        ///   Gets the current UTC time of day (no date component).
        /// </summary>
        LocalTime UtcNowTime { get; }
        //--------------------------------------------------------------------------------
        /// <summary>
        ///   Gets the current local date (no time component) in the system default zone.
        /// </summary>
        LocalDate LocalNowDate { get; }
        //--------------------------------------------------------------------------------
        /// <summary>
        ///   Gets the current UTC date (no time component).
        /// </summary>
        LocalDate UtcNowDate { get; }
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
        ///   Creates a task that completes after the specified <see cref="Duration"/>.
        /// </summary>
        /// <param name="duration">
        ///   The duration to wait before completing the returned task.
        /// </param>
        /// <returns>
        ///   A task that completes after <paramref name="duration"/>.
        /// </returns>
        /// <remarks>
        ///   Implementations may clamp values: zero or negative is treated as zero; durations
        ///   greater than <see cref="TimeSpan.MaxValue"/> are clamped to
        ///   <see cref="TimeSpan.MaxValue"/> for the underlying delay.
        /// </remarks>
        Task DelayAsync (Duration duration);
        //--------------------------------------------------------------------------------
        /// <summary>
        ///   Creates a task that completes after the specified <see cref="Duration"/>.
        /// </summary>
        /// <param name="duration">
        ///   The duration to wait before completing the returned task.
        /// </param>
        /// <param name="cancellationToken">
        ///   A cancellation token to observe while waiting for the task to complete.
        /// </param>
        /// <returns>
        ///   A task that completes after <paramref name="duration"/> or when cancelled.
        /// </returns>
        /// <remarks>
        ///   Implementations may clamp values: zero or negative is treated as zero; durations
        ///   greater than <see cref="TimeSpan.MaxValue"/> are clamped to
        ///   <see cref="TimeSpan.MaxValue"/> for the underlying delay.
        /// </remarks>
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
        TimeCancellationTokenSource LinkTimeCancellationToken (
            Duration cancelAfter, CancellationToken cancellationToken);
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
        TimeCancellationTokenSource LinkTimeCancellationToken (
            Duration cancelAfter, CancellationToken token1, CancellationToken token2);
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
        TimeCancellationTokenSource LinkTimeCancellationToken (
            Duration cancelAfter, params CancellationToken[] cancellationTokens);
        //--------------------------------------------------------------------------------

        #endregion IPrimeClock — Time cancellation (Duration)
    }
    //################################################################################
}
