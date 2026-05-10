// Copyright (c) Kevin Zehrer
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;

using AwesomeAssertions;

using KZDev.PrimeTime.Testing;

using NodaTime;

namespace KZDev.PrimeTime.UnitTests;

//################################################################################
/// <summary>
///   Validates schedule-zone projection extensions for <see cref="IPrimeTime"/> in the Noda stack.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed class UsingPrimeTimeScheduleZoneExtensions
{
    /// <summary>
    ///   Minimal <see cref="IPrimeTime"/> that does not implement <see cref="IPrimeClock"/>.
    /// </summary>
    private sealed class NonClockPrimeTime : IPrimeTime
    {
        private static NotSupportedException Stub () => new();

        /// <inheritdoc />
        public void Sleep (TimeSpan sleepTime) => throw Stub();

        /// <inheritdoc />
        public void Sleep (int sleepMilliseconds) => throw Stub();

        /// <inheritdoc />
        public Task DelayAsync (TimeSpan delayTime) => throw Stub();

        /// <inheritdoc />
        public Task DelayAsync (int millisecondsDelay) => throw Stub();

        /// <inheritdoc />
        public Task DelayAsync (TimeSpan delayTime, CancellationToken cancellationToken) => throw Stub();

        /// <inheritdoc />
        public Task DelayAsync (int millisecondsDelay, CancellationToken cancellationToken) => throw Stub();

        /// <inheritdoc />
        public TimeCancellationTokenSource GetTimeCancellationToken (TimeSpan cancelTime) => throw Stub();

        /// <inheritdoc />
        public TimeCancellationTokenSource GetTimeCancellationToken (int cancelMilliseconds) => throw Stub();

        /// <inheritdoc />
        public TimeCancellationTokenSource LinkTimeCancellationToken (int cancelMilliseconds,
            CancellationToken token1, CancellationToken token2) => throw Stub();

        /// <inheritdoc />
        public TimeCancellationTokenSource LinkTimeCancellationToken (TimeSpan cancelTime,
            CancellationToken token1, CancellationToken token2) => throw Stub();

        /// <inheritdoc />
        public TimeCancellationTokenSource LinkTimeCancellationToken (TimeSpan cancelTime,
            CancellationToken cancellationToken) => throw Stub();

        /// <inheritdoc />
        public TimeCancellationTokenSource LinkTimeCancellationToken (int cancelMilliseconds,
            CancellationToken cancellationToken) => throw Stub();

        /// <inheritdoc />
        public TimeCancellationTokenSource LinkTimeCancellationToken (TimeSpan cancelTime,
            params CancellationToken[] cancellationTokens) => throw Stub();

        /// <inheritdoc />
        public TimeCancellationTokenSource LinkTimeCancellationToken (int cancelMilliseconds,
            params CancellationToken[] cancellationTokens) => throw Stub();

        /// <inheritdoc />
        public void Sleep (Duration duration) => throw Stub();

        /// <inheritdoc />
        public Task DelayAsync (Duration duration) => throw Stub();

        /// <inheritdoc />
        public Task DelayAsync (Duration duration, CancellationToken cancellationToken) => throw Stub();

        /// <inheritdoc />
        public TimeCancellationTokenSource GetTimeCancellationToken (Duration cancelAfter) => throw Stub();

        /// <inheritdoc />
        public TimeCancellationTokenSource LinkTimeCancellationToken (Duration cancelAfter,
            CancellationToken cancellationToken) => throw Stub();

        /// <inheritdoc />
        public TimeCancellationTokenSource LinkTimeCancellationToken (Duration cancelAfter,
            CancellationToken firstCancellationToken, CancellationToken secondCancellationToken) => throw Stub();

        /// <inheritdoc />
        public TimeCancellationTokenSource LinkTimeCancellationToken (Duration cancelAfter,
            params CancellationToken[] cancellationTokens) => throw Stub();
    }

    /// <summary>
    ///   Verifies <see cref="PrimeTimeScheduleZoneExtensions.ToScheduleZonedDateTime"/> for an
    ///   <see cref="Instant"/> matches Noda <see cref="Instant.InZone"/> for the clock's zone.
    /// </summary>
    [Fact]
    public void PrimeTestClock_Instant_ToScheduleZonedDateTime_MatchesInZoneOfClock ()
    {
        Instant instant = Instant.FromUtc(2025, 6, 1, 16, 0, 0);
        DateTimeZone zone = DateTimeZoneProviders.Tzdb["America/New_York"];
        IPrimeTime time = new PrimeTestClock(instant, zone);
        ZonedDateTime expected = instant.InZone(zone);
        ZonedDateTime actual = time.ToScheduleZonedDateTime(instant);
        actual.Should().Be(expected);
    }

    /// <summary>
    ///   Verifies zoned overloads use <see cref="ZonedDateTime.ToInstant"/> so the source zone does not affect the result.
    /// </summary>
    [Fact]
    public void PrimeTestClock_ZonedInUtc_ToScheduleLocalDateTime_MatchesScheduleZoneWall ()
    {
        Instant instant = Instant.FromUtc(2025, 6, 1, 16, 0, 0);
        DateTimeZone zone = DateTimeZoneProviders.Tzdb["America/New_York"];
        IPrimeTime time = new PrimeTestClock(instant, zone);
        ZonedDateTime utcZoned = instant.InUtc();
        LocalDateTime expected = instant.InZone(zone).LocalDateTime;
        LocalDateTime actual = time.ToScheduleLocalDateTime(utcZoned);
        actual.Should().Be(expected);
    }

    /// <summary>
    ///   Verifies <see cref="PrimeTimeScheduleZoneExtensions.ToScheduleDateTimeOffset"/> for an
    ///   <see cref="Instant"/> matches <see cref="ZonedDateTime.ToDateTimeOffset"/>.
    /// </summary>
    [Fact]
    public void PrimeTestClock_Instant_ToScheduleDateTimeOffset_MatchesNodaConversion ()
    {
        Instant instant = Instant.FromUtc(2025, 6, 1, 16, 0, 0);
        DateTimeZone zone = DateTimeZoneProviders.Tzdb["America/New_York"];
        IPrimeTime time = new PrimeTestClock(instant, zone);
        DateTimeOffset expected = instant.InZone(zone).ToDateTimeOffset();
        DateTimeOffset actual = time.ToScheduleDateTimeOffset(instant);
        actual.Should().Be(expected);
    }

    /// <summary>
    ///   Verifies <see cref="PrimeTimeScheduleZoneExtensions.ToScheduleLocalDate"/> matches the calendar date in the
    ///   schedule zone.
    /// </summary>
    [Fact]
    public void PrimeTestClock_Instant_ToScheduleLocalDate_MatchesInZoneDate ()
    {
        Instant instant = Instant.FromUtc(2025, 6, 1, 4, 30, 0);
        DateTimeZone zone = DateTimeZoneProviders.Tzdb["America/New_York"];
        IPrimeTime time = new PrimeTestClock(instant, zone);
        LocalDate expected = instant.InZone(zone).Date;
        LocalDate actual = time.ToScheduleLocalDate(instant);
        actual.Should().Be(expected);
    }

    /// <summary>
    ///   Verifies <see cref="PrimeTimeScheduleZoneExtensions.ToScheduleLocalTime"/> matches wall time in the schedule
    ///   zone.
    /// </summary>
    [Fact]
    public void PrimeTestClock_Instant_ToScheduleLocalTime_MatchesInZoneTimeOfDay ()
    {
        Instant instant = Instant.FromUtc(2025, 6, 1, 4, 30, 0);
        DateTimeZone zone = DateTimeZoneProviders.Tzdb["America/New_York"];
        IPrimeTime time = new PrimeTestClock(instant, zone);
        LocalTime expected = instant.InZone(zone).TimeOfDay;
        LocalTime actual = time.ToScheduleLocalTime(instant);
        actual.Should().Be(expected);
    }

    /// <summary>
    ///   Verifies <see cref="PrimeTimeScheduleZoneExtensions.ToScheduleLocalWallDateTime"/> matches Noda local wall
    ///   projection.
    /// </summary>
    [Fact]
    public void PrimeTestClock_Instant_ToScheduleLocalWallDateTime_MatchesLocalDateTimeToDateTimeUnspecified ()
    {
        Instant instant = Instant.FromUtc(2025, 6, 1, 4, 30, 0);
        DateTimeZone zone = DateTimeZoneProviders.Tzdb["America/New_York"];
        IPrimeTime time = new PrimeTestClock(instant, zone);
        DateTime expected = instant.InZone(zone).LocalDateTime.ToDateTimeUnspecified();
        DateTime actual = time.ToScheduleLocalWallDateTime(instant);
        actual.Should().Be(expected);
        actual.Kind.Should().Be(DateTimeKind.Unspecified);
    }

    /// <summary>
    ///   Verifies <see cref="PrimeTimeScheduleZoneExtensions.ToScheduleZonedDateTime(ZonedDateTime)"/> ignores the
    ///   source zone and uses only the instant.
    /// </summary>
    [Fact]
    public void PrimeTestClock_ZonedInTokyo_ToScheduleZonedDateTime_MatchesScheduleZoneWall ()
    {
        Instant instant = Instant.FromUtc(2025, 6, 1, 16, 0, 0);
        DateTimeZone scheduleZone = DateTimeZoneProviders.Tzdb["America/New_York"];
        IPrimeTime time = new PrimeTestClock(instant, scheduleZone);
        DateTimeZone tokyo = DateTimeZoneProviders.Tzdb["Asia/Tokyo"];
        ZonedDateTime tokyoZoned = instant.InZone(tokyo);
        ZonedDateTime expected = instant.InZone(scheduleZone);
        ZonedDateTime actual = time.ToScheduleZonedDateTime(tokyoZoned);
        actual.Should().Be(expected);
    }

    /// <summary>
    ///   Verifies a non-clock <see cref="IPrimeTime"/> throws <see cref="ArgumentException"/>.
    /// </summary>
    [Fact]
    public void NonClock_ToScheduleZonedDateTime_ThrowsArgumentException ()
    {
        IPrimeTime time = new NonClockPrimeTime();
        Instant instant = Instant.FromUtc(2025, 1, 1, 0, 0, 0);
        Action act = () => _ = time.ToScheduleZonedDateTime(instant);
        act.Should().Throw<ArgumentException>().WithParameterName("time");
    }

    /// <summary>
    ///   Verifies <see langword="null"/> receiver throws <see cref="ArgumentNullException"/>.
    /// </summary>
    [Fact]
    public void NullTime_ToScheduleZonedDateTime_ThrowsArgumentNullException ()
    {
        IPrimeTime? time = null;
        Instant instant = Instant.FromUtc(2025, 1, 1, 0, 0, 0);
        Action act = () => _ = time!.ToScheduleZonedDateTime(instant);
        act.Should().Throw<ArgumentNullException>().WithParameterName("time");
    }

    /// <summary>
    ///   Verifies non-clock receivers fail for <see cref="PrimeTimeScheduleZoneExtensions.ToScheduleLocalDate"/>.
    /// </summary>
    [Fact]
    public void NonClock_ToScheduleLocalDate_ThrowsArgumentException ()
    {
        IPrimeTime time = new NonClockPrimeTime();
        Instant instant = Instant.FromUtc(2025, 1, 1, 0, 0, 0);
        Action act = () => _ = time.ToScheduleLocalDate(instant);
        act.Should().Throw<ArgumentException>().WithParameterName("time");
    }
}
//################################################################################
