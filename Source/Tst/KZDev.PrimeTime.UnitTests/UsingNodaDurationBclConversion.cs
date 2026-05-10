// Copyright (c) Kevin Zehrer
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;

using AwesomeAssertions;

using NodaTime;

namespace KZDev.PrimeTime.UnitTests;

//################################################################################
/// <summary>
///   Validates Noda <see cref="Duration"/> to BCL conversion helpers used for superset alignment with
///   <see cref="TimeSpan"/>-based <see cref="IPrimeClock"/> timer and delay semantics.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed class UsingNodaDurationBclConversion
{
    //----------------------------------------------------------------------------
    /// <summary>
    ///   Duration strictly larger than <see cref="TimeSpan.MaxValue"/> in the Noda ordering used by
    ///   <see cref="NodaDurationBclConversion"/>.
    /// </summary>
    private static readonly Duration DurationStrictlyBeyondTimeSpanMaxValue =
        Duration.FromTimeSpan(TimeSpan.MaxValue) + Duration.FromMilliseconds(1);
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies that <see cref="NodaDurationBclConversion.ToTimeSpanForDelay"/> maps zero and negative
    ///   <see cref="Duration"/> values to <see cref="TimeSpan.Zero"/> for BCL delay semantics.
    /// </summary>
    [Fact]
    public void ToTimeSpanForDelay_WhenNonPositive_ReturnsZero ()
    {
        NodaDurationBclConversion.ToTimeSpanForDelay(Duration.Zero).Should().Be(TimeSpan.Zero);
        NodaDurationBclConversion.ToTimeSpanForDelay(Duration.FromMilliseconds(-1)).Should().Be(TimeSpan.Zero);
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies that <see cref="NodaDurationBclConversion.ToTimeSpanForDelay"/> clamps durations above
    ///   <see cref="TimeSpan.MaxValue"/> to <see cref="TimeSpan.MaxValue"/>.
    /// </summary>
    [Fact]
    public void ToTimeSpanForDelay_WhenExceedsTimeSpanMaxValue_ReturnsTimeSpanMaxValue ()
    {
        NodaDurationBclConversion.ToTimeSpanForDelay(DurationStrictlyBeyondTimeSpanMaxValue).Should().Be(TimeSpan.MaxValue);
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies that <see cref="NodaDurationBclConversion.ToTimeSpanForCancellationToken"/> caps huge
    ///   durations at <see cref="int.MaxValue"/> milliseconds as a <see cref="TimeSpan"/>.
    /// </summary>
    [Fact]
    public void ToTimeSpanForCancellationToken_WhenExceedsIntMillisCap_ReturnsIntMaxValueAsTimeSpan ()
    {
        Duration huge = Duration.FromDays(300_000);
        TimeSpan ts = NodaDurationBclConversion.ToTimeSpanForCancellationToken(huge);
        ts.Should().Be(TimeSpan.FromMilliseconds(int.MaxValue));
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies that <see cref="NodaDurationBclConversion.ToTimeSpanForCancellationToken"/> maps zero and negative
    ///   values to <see cref="TimeSpan.Zero"/>.
    /// </summary>
    [Fact]
    public void ToTimeSpanForCancellationToken_WhenNonPositive_ReturnsZero ()
    {
        NodaDurationBclConversion.ToTimeSpanForCancellationToken(Duration.Zero).Should().Be(TimeSpan.Zero);
        NodaDurationBclConversion.ToTimeSpanForCancellationToken(Duration.FromMilliseconds(-1)).Should().Be(TimeSpan.Zero);
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies that <see cref="NodaDurationBclConversion.ToTimeSpanForTimerInterval"/> clamps positive
    ///   durations above <see cref="TimeSpan.MaxValue"/> to <see cref="TimeSpan.MaxValue"/>.
    /// </summary>
    [Fact]
    public void ToTimeSpanForTimerInterval_WhenPositiveAndBeyondTimeSpanMaxValue_ReturnsTimeSpanMaxValue ()
    {
        NodaDurationBclConversion.ToTimeSpanForTimerInterval(DurationStrictlyBeyondTimeSpanMaxValue).Should().Be(
            TimeSpan.MaxValue);
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies that <see cref="NodaDurationBclConversion.ToTimeSpanForTimerInterval"/> clamps
    ///   <see cref="Duration.MaxValue"/> to <see cref="TimeSpan.MaxValue"/>.
    /// </summary>
    [Fact]
    public void ToTimeSpanForTimerInterval_WhenDurationMaxValue_ReturnsTimeSpanMaxValue ()
    {
        NodaDurationBclConversion.ToTimeSpanForTimerInterval(Duration.MaxValue).Should().Be(TimeSpan.MaxValue);
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies that <see cref="NodaDurationBclConversion.ToTimeSpanForTimerInterval"/> preserves
    ///   <see cref="Timeout.InfiniteTimeSpan"/> for one-shot timer due-time registration.
    /// </summary>
    [Fact]
    public void ToTimeSpanForTimerInterval_WhenInfiniteDueTime_PreservesInfiniteTimeSpan ()
    {
        Duration infiniteDueTime = Duration.FromTimeSpan(Timeout.InfiniteTimeSpan);
        NodaDurationBclConversion.ToTimeSpanForTimerInterval(infiniteDueTime).Should().Be(Timeout.InfiniteTimeSpan);
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Documents intentional difference from <see cref="NodaDurationBclConversion.ToTimeSpanForDelay"/>: timer paths
    ///   pass zero and negative <see cref="Duration"/> through <see cref="Duration.ToTimeSpan"/> instead of coercing
    ///   to <see cref="TimeSpan.Zero"/> so they match BCL <see cref="TimeSpan"/> timer registration semantics.
    /// </summary>
    [Fact]
    public void ToTimeSpanForTimerInterval_WhenZeroOrNegative_MatchesDurationToTimeSpan_NotDelayCoercion ()
    {
        Duration zero = Duration.Zero;
        NodaDurationBclConversion.ToTimeSpanForTimerInterval(zero).Should().Be(zero.ToTimeSpan());
        NodaDurationBclConversion.ToTimeSpanForTimerInterval(zero).Should().Be(TimeSpan.Zero);

        Duration negative = Duration.FromMilliseconds(-10);
        NodaDurationBclConversion.ToTimeSpanForTimerInterval(negative).Should().Be(negative.ToTimeSpan());
        NodaDurationBclConversion.ToTimeSpanForDelay(negative).Should().Be(TimeSpan.Zero,
            "non-positive durations coerce to zero for delay APIs only");
    }
    //----------------------------------------------------------------------------
}
//################################################################################
