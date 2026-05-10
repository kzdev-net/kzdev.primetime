// Copyright (c) Kevin Zehrer
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

using NodaTime;

namespace KZDev.PrimeTime.Testing.Examples;

/// <summary>
/// Demonstrates deterministic persistence and schedule-zone conversion patterns with
/// <see cref="IPrimeTestClock"/>.
/// </summary>
public sealed class UsingPersistenceAndConversionExamples
{
    #region Snippet
    /// <summary>
    /// Verifies a fixed <see cref="Instant"/> projects into the virtual clock zone the same way as
    /// an in-memory <see cref="ZonedDateTime"/> conversion.
    /// </summary>
    [Fact]
    public void PersistedInstant_ToScheduleZone_MatchesInZoneProjection ()
    {
        DateTimeZone zone = DateTimeZoneProviders.Tzdb["America/New_York"];
        Instant persisted = Instant.FromUtc(2025, 7, 4, 15, 30, 0);
        IPrimeTestClock clock = new PrimeTestClock(persisted, zone);
        IPrimeTime time = clock;

        LocalDate expectedDate = persisted.InZone(zone).Date;
        time.ToScheduleLocalDate(persisted).Should().Be(expectedDate);

        LocalTime wallTime = time.ToScheduleLocalTime(persisted);
        LocalTimeOfDay stored = PrimeTimeOfDayConversion.ToLocalTimeOfDay(wallTime);
        PrimeTimeOfDayConversion.ToLocalTime(stored).Should().Be(wallTime);
    }

    /// <summary>
    /// Verifies <see cref="NodaDurationBclConversion.ToTimeSpanForDelay"/> clamps durations beyond
    /// BCL <see cref="TimeSpan"/> range to <see cref="TimeSpan.MaxValue"/>.
    /// </summary>
    [Fact]
    public void LargeDuration_ToTimeSpanForDelay_ClampsToMaxValue ()
    {
        Duration beyondBclRange = Duration.FromTimeSpan(TimeSpan.MaxValue) + Duration.FromSeconds(1);
        NodaDurationBclConversion.ToTimeSpanForDelay(beyondBclRange).Should().Be(TimeSpan.MaxValue);
    }
    #endregion Snippet
}
