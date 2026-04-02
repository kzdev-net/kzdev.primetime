// Copyright (c) Kevin Zehrer. All rights reserved.
// This file is part of the PrimeTime project.

using System;

using AwesomeAssertions;

using KZDev.PrimeTime;

using NodaTime;

namespace KZDev.PrimeTime.NodaTime.UnitTests;

/// <summary>
///   Validates Noda <see cref="Duration"/> to BCL conversion helpers used for superset alignment with
///   <see cref="TimeSpan"/>-based <see cref="IPrimeClock"/> timer and delay semantics.
/// </summary>
public sealed class UsingNodaDurationBclConversions
{
    /// <summary>
    ///   Duration strictly larger than <see cref="TimeSpan.MaxValue"/> in the Noda ordering used by
    ///   <see cref="NodaDurationBclConversions"/>.
    /// </summary>
    private static readonly Duration DurationStrictlyBeyondTimeSpanMaxValue =
        Duration.FromTimeSpan(TimeSpan.MaxValue) + Duration.FromMilliseconds(1);

    [Fact]
    public void ToTimeSpanForDelay_WhenNonPositive_ReturnsZero ()
    {
        NodaDurationBclConversions.ToTimeSpanForDelay(Duration.Zero).Should().Be(TimeSpan.Zero);
        NodaDurationBclConversions.ToTimeSpanForDelay(Duration.FromMilliseconds(-1)).Should().Be(TimeSpan.Zero);
    }

    [Fact]
    public void ToTimeSpanForDelay_WhenExceedsTimeSpanMaxValue_ReturnsTimeSpanMaxValue ()
    {
        NodaDurationBclConversions.ToTimeSpanForDelay(DurationStrictlyBeyondTimeSpanMaxValue).Should().Be(TimeSpan.MaxValue);
    }

    [Fact]
    public void ToTimeSpanForCancellationToken_WhenExceedsIntMillisCap_ReturnsIntMaxValueAsTimeSpan ()
    {
        Duration huge = Duration.FromDays(300_000);
        TimeSpan ts = NodaDurationBclConversions.ToTimeSpanForCancellationToken(huge);
        ts.Should().Be(TimeSpan.FromMilliseconds(int.MaxValue));
    }

    [Fact]
    public void ToTimeSpanForTimerInterval_WhenPositiveAndBeyondTimeSpanMaxValue_ReturnsTimeSpanMaxValue ()
    {
        NodaDurationBclConversions.ToTimeSpanForTimerInterval(DurationStrictlyBeyondTimeSpanMaxValue).Should().Be(
            TimeSpan.MaxValue);
    }

    [Fact]
    public void ToTimeSpanForTimerInterval_WhenInfiniteDueTime_PreservesInfiniteTimeSpan ()
    {
        Duration infiniteDueTime = Duration.FromTimeSpan(Timeout.InfiniteTimeSpan);
        NodaDurationBclConversions.ToTimeSpanForTimerInterval(infiniteDueTime).Should().Be(Timeout.InfiniteTimeSpan);
    }

    /// <summary>
    ///   Documents intentional difference from <see cref="NodaDurationBclConversions.ToTimeSpanForDelay"/>: timer paths
    ///   pass zero and negative <see cref="Duration"/> through <see cref="Duration.ToTimeSpan"/> instead of coercing
    ///   to <see cref="TimeSpan.Zero"/> so they match BCL <see cref="TimeSpan"/> timer registration semantics.
    /// </summary>
    [Fact]
    public void ToTimeSpanForTimerInterval_WhenZeroOrNegative_MatchesDurationToTimeSpan_NotDelayCoercion ()
    {
        Duration zero = Duration.Zero;
        NodaDurationBclConversions.ToTimeSpanForTimerInterval(zero).Should().Be(zero.ToTimeSpan());
        NodaDurationBclConversions.ToTimeSpanForTimerInterval(zero).Should().Be(TimeSpan.Zero);

        Duration negative = Duration.FromMilliseconds(-10);
        NodaDurationBclConversions.ToTimeSpanForTimerInterval(negative).Should().Be(negative.ToTimeSpan());
        NodaDurationBclConversions.ToTimeSpanForDelay(negative).Should().Be(TimeSpan.Zero,
            "non-positive durations coerce to zero for delay APIs only");
    }
}
