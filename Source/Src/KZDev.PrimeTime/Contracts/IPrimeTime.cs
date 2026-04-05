// Copyright (c) Kevin Zehrer. All rights reserved.
// This file is part of the PrimeTime project.

using NodaTime;

namespace KZDev.PrimeTime;

//################################################################################
/// <summary>
///   Extends <see cref="IPrimeTime"/> with NodaTime-based "now" APIs so callers can obtain
///   current time from the clock abstraction using <see cref="Instant"/>,
///   <see cref="LocalDateTime"/>, <see cref="ZonedDateTime"/>, <see cref="LocalTime"/>,
///   and <see cref="LocalDate"/>.
/// </summary>
public partial interface IPrimeTime
{
    #region IPrimeTime — Delays (Duration)

    //----------------------------------------------------------------------------
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
    //----------------------------------------------------------------------------
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
    //----------------------------------------------------------------------------
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
    //----------------------------------------------------------------------------

    #endregion IPrimeTime — Delays (Duration)

    #region IPrimeTime — Time cancellation (Duration)

    //----------------------------------------------------------------------------
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
    //----------------------------------------------------------------------------
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
    //----------------------------------------------------------------------------
    /// <summary>
    ///   Returns a disposable wrapper whose token is cancelled when the specified
    ///   duration elapses or any of the given tokens is cancelled. The caller is
    ///   responsible for disposing the returned instance when no longer needed.
    /// </summary>
    /// <param name="cancelAfter">
    ///   The duration to wait before cancelling the token.
    /// </param>
    /// <param name="firstCancellationToken">
    ///   The first cancellation token to link.
    /// </param>
    /// <param name="secondCancellationToken">
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
    TimeCancellationTokenSource LinkTimeCancellationToken (Duration cancelAfter, CancellationToken firstCancellationToken,
        CancellationToken secondCancellationToken);
    //----------------------------------------------------------------------------
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
    //----------------------------------------------------------------------------

    #endregion IPrimeTime — Time cancellation (Duration)
}
//################################################################################

