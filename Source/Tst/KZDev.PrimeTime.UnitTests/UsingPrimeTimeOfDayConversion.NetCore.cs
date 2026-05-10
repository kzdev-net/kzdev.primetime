// Copyright (c) Kevin Zehrer
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

#if NET

using System.Diagnostics.CodeAnalysis;

using AwesomeAssertions;

using NodaTime;

namespace KZDev.PrimeTime.UnitTests;

//################################################################################
/// <summary>
///   Validates <see cref="PrimeTimeOfDayConversion"/> round-trips for BCL time-of-day wrappers and
///   <see cref="TimeOnly"/>.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed class UsingPrimeTimeOfDayConversion
{
    /// <summary>
    ///   Verifies <see cref="PrimeTimeOfDayConversion.ToLocalTime(LocalTimeOfDay)"/> and
    ///   <see cref="PrimeTimeOfDayConversion.ToLocalTimeOfDay"/> preserve sub-second tick resolution.
    /// </summary>
    [Fact]
    public void LocalTimeOfDay_ToLocalTime_ToLocalTimeOfDay_RoundTripsTicks ()
    {
        LocalTimeOfDay original = new(new TimeOnly(14, 30, 45, 123));
        LocalTime noda = PrimeTimeOfDayConversion.ToLocalTime(original);
        PrimeTimeOfDayConversion.ToLocalTimeOfDay(noda).Should().Be(original);
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies <see cref="PrimeTimeOfDayConversion.ToLocalTime(UtcTimeOfDay)"/> and
    ///   <see cref="PrimeTimeOfDayConversion.ToUtcTimeOfDay"/> preserve wall-clock ticks.
    /// </summary>
    [Fact]
    public void UtcTimeOfDay_ToLocalTime_ToUtcTimeOfDay_RoundTripsTicks ()
    {
        UtcTimeOfDay original = new(new TimeOnly(2, 15, 0, 456));
        LocalTime noda = PrimeTimeOfDayConversion.ToLocalTime(original);
        PrimeTimeOfDayConversion.ToUtcTimeOfDay(noda).Should().Be(original);
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies <see cref="PrimeTimeOfDayConversion.ToLocalTime(TimeOnly)"/> matches direct
    ///   <see cref="LocalTime.FromTicksSinceMidnight"/> for the same ticks.
    /// </summary>
    [Fact]
    public void TimeOnly_ToLocalTime_MatchesFromTicksSinceMidnight ()
    {
        TimeOnly wall = new(9, 8, 7, 654);
        LocalTime expected = LocalTime.FromTicksSinceMidnight(wall.Ticks);
        PrimeTimeOfDayConversion.ToLocalTime(wall).Should().Be(expected);
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies midnight <see cref="LocalTime"/> round-trips through <see cref="LocalTimeOfDay"/>.
    /// </summary>
    [Fact]
    public void LocalTime_Midnight_ToLocalTimeOfDay_RoundTrips ()
    {
        LocalTime midnight = LocalTime.Midnight;
        LocalTimeOfDay wrapped = PrimeTimeOfDayConversion.ToLocalTimeOfDay(midnight);
        wrapped.Value.Should().Be(TimeOnly.MinValue);
        PrimeTimeOfDayConversion.ToLocalTime(wrapped).Should().Be(midnight);
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies end-of-day tick values round-trip through <see cref="LocalTimeOfDay"/>.
    /// </summary>
    [Fact]
    public void LocalTime_EndOfDay_ToLocalTimeOfDay_RoundTrips ()
    {
        LocalTime endOfDay = LocalTime.FromTicksSinceMidnight(TimeOnly.MaxValue.Ticks);
        LocalTimeOfDay wrapped = PrimeTimeOfDayConversion.ToLocalTimeOfDay(endOfDay);
        PrimeTimeOfDayConversion.ToLocalTime(wrapped).Should().Be(endOfDay);
    }
    //----------------------------------------------------------------------------
}
//################################################################################
#endif
