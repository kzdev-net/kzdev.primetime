// Copyright (c) Kevin Zehrer
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

using KZDev.SystemClock.PrimeTime;
using KZDev.SystemClock.PrimeTime.Testing;

namespace KZDev.SystemClock.PrimeTime.Testing.Examples;

/// <summary>
/// Demonstrates deterministic persistence and schedule-zone conversion patterns with the BCL
/// <see cref="IPrimeTestClock"/>.
/// </summary>
public sealed class UsingPersistenceAndConversionExamples
{
    #region Snippet
    /// <summary>
    /// Verifies schedule-zone <see cref="DateTimeOffset"/> projection matches
    /// <see cref="TimeZoneInfo.ConvertTime(DateTimeOffset, TimeZoneInfo)"/> for the clock zone.
    /// </summary>
    [Fact]
    public void PersistedUtc_ToScheduleZone_MatchesTimeZoneInfoConvertTime ()
    {
        DateTimeOffset persistedUtc = new DateTimeOffset(2025, 7, 4, 15, 30, 0, TimeSpan.Zero);
        IPrimeTestClock clock = new PrimeTestClock(persistedUtc);
        IPrimeTime time = clock;

        DateTimeOffset projected = time.ToScheduleDateTimeOffset(persistedUtc);
        DateTimeOffset expected = TimeZoneInfo.ConvertTime(persistedUtc, clock.LocalScheduleTimeZone);
        projected.Should().Be(expected);
    }

    /// <summary>
    /// Verifies <see cref="DateOnly"/> and <see cref="TimeOnly"/> extracted from a persisted instant
    /// recombine to the schedule-local wall clock.
    /// </summary>
    [Fact]
    public void PersistedUtc_ToScheduleDateAndTime_RecombinesToWallDateTime ()
    {
        DateTimeOffset persistedUtc = new DateTimeOffset(2025, 7, 4, 15, 30, 0, TimeSpan.Zero);
        IPrimeTestClock clock = new PrimeTestClock(persistedUtc);
        IPrimeTime time = clock;

        DateTimeOffset inZone = time.ToScheduleDateTimeOffset(persistedUtc);
        DateOnly date = time.ToScheduleDateOnly(persistedUtc);
        TimeOnly timeOfDay = time.ToScheduleTimeOnly(persistedUtc);
        DateTime wall = date.ToDateTime(timeOfDay);
        wall.Should().Be(inZone.DateTime);
    }
    #endregion Snippet
}
