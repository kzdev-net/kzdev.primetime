// Copyright (c) Kevin Zehrer. All rights reserved.
// This file is part of the PrimeTime project.

using System;

using NodaTime;

namespace KZDev.PrimeTime;

//################################################################################
/// <summary>
///   Centralizes <see cref="Duration"/> to BCL <see cref="TimeSpan"/> conversions for the Noda stack
///   so delays, cancellation timers, interval registration, and virtual clock advances stay aligned with
///   <see cref="TimeSpan"/>-based <see cref="IPrimeClock"/> members and avoid <see cref="OverflowException"/>.
/// </summary>
internal static class NodaDurationBclConversions
{
    //----------------------------------------------------------------------------
    /// <summary>
    ///   Largest <see cref="Duration"/> that maps to <see cref="TimeSpan.MaxValue"/> for BCL delay and sleep APIs.
    /// </summary>
    private static readonly Duration MaxDurationForDelay = Duration.FromTimeSpan(TimeSpan.MaxValue);

    /// <summary>
    ///   Largest <see cref="Duration"/> that maps to a span of <c>int.MaxValue</c> milliseconds for
    ///   <see cref="CancellationTokenSource"/> timer construction.
    /// </summary>
    private static readonly Duration MaxDurationForCancellationToken = Duration.FromMilliseconds(int.MaxValue);
    //----------------------------------------------------------------------------

    //----------------------------------------------------------------------------
    /// <summary>
    ///   Converts a <see cref="Duration"/> to <see cref="TimeSpan"/> for BCL delay APIs
    ///   (<see cref="Thread.Sleep(TimeSpan)"/>, <see cref="Task.Delay(TimeSpan)"/>, and related).
    /// </summary>
    /// <param name="duration">
    ///   The duration to convert.
    /// </param>
    /// <returns>
    ///   <see cref="TimeSpan.Zero"/> when <paramref name="duration"/> is zero or negative,
    ///   <see cref="TimeSpan.MaxValue"/> when it exceeds the BCL representable range, otherwise the equivalent span.
    /// </returns>
    /// <exception cref="OverflowException">
    ///   Thrown by <see cref="Duration.ToTimeSpan"/> when <paramref name="duration"/> is not representable as a
    ///   <see cref="TimeSpan"/> in the branch that calls it.
    /// </exception>
    internal static TimeSpan ToTimeSpanForDelay (Duration duration)
    {
        if (duration <= Duration.Zero)
        {
            return TimeSpan.Zero;
        }

        if (duration >= MaxDurationForDelay)
        {
            return TimeSpan.MaxValue;
        }

        return duration.ToTimeSpan();
    }
    //----------------------------------------------------------------------------

    //----------------------------------------------------------------------------
    /// <summary>
    ///   Converts a <see cref="Duration"/> to <see cref="TimeSpan"/> for <see cref="CancellationTokenSource"/>
    ///   timer limits.
    /// </summary>
    /// <param name="duration">
    ///   The duration to convert.
    /// </param>
    /// <returns>
    ///   <see cref="TimeSpan.Zero"/> when <paramref name="duration"/> is zero or negative; otherwise a span capped at
    ///   <c>int.MaxValue</c> milliseconds when the duration exceeds that limit.
    /// </returns>
    /// <exception cref="OverflowException">
    ///   Thrown by <see cref="Duration.ToTimeSpan"/> when <paramref name="duration"/> is not representable as a
    ///   <see cref="TimeSpan"/> in the branch that calls it.
    /// </exception>
    internal static TimeSpan ToTimeSpanForCancellationToken (Duration duration)
    {
        if (duration <= Duration.Zero)
        {
            return TimeSpan.Zero;
        }

        if (duration >= MaxDurationForCancellationToken)
        {
            return TimeSpan.FromMilliseconds(int.MaxValue);
        }

        return duration.ToTimeSpan();
    }
    //----------------------------------------------------------------------------

    //----------------------------------------------------------------------------
    /// <summary>
    ///   Converts a <see cref="Duration"/> to <see cref="TimeSpan"/> for interval timer registration, timer
    ///   <c>Change</c> operations, and virtual clock deltas so behavior matches <see cref="IPrimeClock"/>
    ///   <see cref="TimeSpan"/>-based timer registration overloads.
    /// </summary>
    /// <param name="duration">
    ///   The duration to convert (initial delay, repeat interval, or virtual time delta).
    /// </param>
    /// <returns>
    ///   <see cref="TimeSpan.MaxValue"/> when <paramref name="duration"/> is strictly positive and does not fit in a
    ///   <see cref="TimeSpan"/>; otherwise the result of <see cref="Duration.ToTimeSpan"/>.
    /// </returns>
    /// <remarks>
    ///   Unlike <see cref="ToTimeSpanForDelay"/> and <see cref="ToTimeSpanForCancellationToken"/>,
    ///   zero and negative durations are not mapped to <see cref="TimeSpan.Zero"/>; they use
    ///   <see cref="Duration.ToTimeSpan"/> so Noda <see cref="Duration"/> overloads stay aligned with BCL
    ///   <see cref="TimeSpan"/> values passed into shared timer and virtual-time registration (including
    ///   non-positive and sentinel spans such as <see cref="Timeout.InfiniteTimeSpan"/>).
    /// </remarks>
    /// <exception cref="OverflowException">
    ///   Thrown by <see cref="Duration.ToTimeSpan"/> when <paramref name="duration"/> is not representable as a
    ///   <see cref="TimeSpan"/>.
    /// </exception>
    internal static TimeSpan ToTimeSpanForTimerInterval (Duration duration)
    {
        if (duration > Duration.Zero && duration >= MaxDurationForDelay)
        {
            return TimeSpan.MaxValue;
        }

        return duration.ToTimeSpan();
    }
    //----------------------------------------------------------------------------
}
//################################################################################
