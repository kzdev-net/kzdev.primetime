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

    /// <summary>
    ///   Verifies <see cref="PrimeTimeScheduleZoneExtensions.ToScheduleDateOnly(ZonedDateTime)"/> uses only the
    ///   instant, not the source zone's calendar date.
    /// </summary>
    [Fact]
    public void PrimeTestClock_ZonedTokyo_ToScheduleDateOnly_MatchesScheduleZoneDate ()
    {
        Instant instant = Instant.FromUtc(2025, 6, 1, 15, 0, 0);
        DateTimeZone scheduleZone = DateTimeZoneProviders.Tzdb["America/New_York"];
        IPrimeTime time = new PrimeTestClock(instant, scheduleZone);
        DateTimeZone tokyo = DateTimeZoneProviders.Tzdb["Asia/Tokyo"];
        ZonedDateTime tokyoZoned = instant.InZone(tokyo);
        DateOnly expected = time.ToScheduleDateOnly(instant);
        DateOnly actual = time.ToScheduleDateOnly(tokyoZoned);
        actual.Should().Be(expected);
    }

    /// <summary>
    ///   Verifies <see cref="PrimeTimeScheduleZoneExtensions.ToScheduleTimeOnly(ZonedDateTime)"/> matches the
    ///   schedule zone wall time for the instant.
    /// </summary>
    [Fact]
    public void PrimeTestClock_ZonedUtc_ToScheduleTimeOnly_MatchesScheduleWall ()
    {
        Instant instant = Instant.FromUtc(2025, 6, 1, 4, 30, 0);
        DateTimeZone scheduleZone = DateTimeZoneProviders.Tzdb["America/New_York"];
        IPrimeTime time = new PrimeTestClock(instant, scheduleZone);
        ZonedDateTime utcZoned = instant.InUtc();
        TimeOnly expected = time.ToScheduleTimeOnly(instant);
        TimeOnly actual = time.ToScheduleTimeOnly(utcZoned);
        actual.Should().Be(expected);
    }
}
//################################################################################

#endif
