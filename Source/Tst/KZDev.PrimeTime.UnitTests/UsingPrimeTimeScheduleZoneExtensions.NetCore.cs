// Copyright (c) Kevin Zehrer
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

#if NET

using System.Diagnostics.CodeAnalysis;

using AwesomeAssertions;

using KZDev.PrimeTime.Testing;

using NodaTime;

namespace KZDev.PrimeTime.UnitTests;

//################################################################################
/// <summary>
///   .NET-only tests for <see cref="DateOnly"/> / <see cref="TimeOnly"/> schedule projections.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed class UsingPrimeTimeScheduleZoneExtensionsNetCore
{
    /// <summary>
    ///   Verifies <see cref="PrimeTimeScheduleZoneExtensions.ToScheduleDateOnly"/> matches the wall date in the schedule zone.
    /// </summary>
    [Fact]
    public void PrimeTestClock_Instant_ToScheduleDateOnly_MatchesWallDate ()
    {
        Instant instant = Instant.FromUtc(2025, 6, 1, 4, 30, 0);
        DateTimeZone zone = DateTimeZoneProviders.Tzdb["America/New_York"];
        IPrimeTime time = new PrimeTestClock(instant, zone);
        DateTime wall = instant.InZone(zone).LocalDateTime.ToDateTimeUnspecified();
        DateOnly expected = DateOnly.FromDateTime(wall);
        DateOnly actual = time.ToScheduleDateOnly(instant);
        actual.Should().Be(expected);
    }

    /// <summary>
    ///   Verifies <see cref="PrimeTimeScheduleZoneExtensions.ToScheduleTimeOnly"/> matches the wall time in the schedule zone.
    /// </summary>
    [Fact]
    public void PrimeTestClock_Instant_ToScheduleTimeOnly_MatchesWallTime ()
    {
        Instant instant = Instant.FromUtc(2025, 6, 1, 4, 30, 0);
        DateTimeZone zone = DateTimeZoneProviders.Tzdb["America/New_York"];
        IPrimeTime time = new PrimeTestClock(instant, zone);
        DateTime wall = instant.InZone(zone).LocalDateTime.ToDateTimeUnspecified();
        TimeOnly expected = TimeOnly.FromDateTime(wall);
        TimeOnly actual = time.ToScheduleTimeOnly(instant);
        actual.Should().Be(expected);
    }
}
//################################################################################

#endif
