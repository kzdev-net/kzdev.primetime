// Copyright (c) Kevin Zehrer
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;

using AwesomeAssertions;

#if NET
using Microsoft.Extensions.Time.Testing;
#endif

namespace KZDev.SystemClock.PrimeTime.UnitTests;

//################################################################################
/// <summary>
///   Validates schedule-zone projection extensions for <see cref="IPrimeTime"/> in the System Clock stack.
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
    }

    /// <summary>
    ///   Verifies a non-clock <see cref="IPrimeTime"/> throws <see cref="ArgumentException"/>.
    /// </summary>
    [Fact]
    public void NonClock_ToScheduleDateTimeOffset_ThrowsArgumentException ()
    {
        IPrimeTime time = new NonClockPrimeTime();
        DateTimeOffset instant = new(2025, 1, 1, 12, 0, 0, TimeSpan.Zero);
        Action act = () => _ = time.ToScheduleDateTimeOffset(instant);
        act.Should().Throw<ArgumentException>().WithParameterName("time");
    }

    /// <summary>
    ///   Verifies <see langword="null"/> receiver throws <see cref="ArgumentNullException"/>.
    /// </summary>
    [Fact]
    public void NullTime_ToScheduleDateTimeOffset_ThrowsArgumentNullException ()
    {
        IPrimeTime? time = null;
        DateTimeOffset instant = new(2025, 1, 1, 12, 0, 0, TimeSpan.Zero);
        Action act = () => _ = time!.ToScheduleDateTimeOffset(instant);
        act.Should().Throw<ArgumentNullException>().WithParameterName("time");
    }

    /// <summary>
    ///   Verifies <see cref="DateTimeKind.Utc"/> is required for the <see cref="DateTime"/> overload.
    /// </summary>
    [Fact]
    public void UtcDateTime_UnspecifiedKind_ToScheduleDateTimeOffset_ThrowsArgumentException ()
    {
        IPrimeTime time = new PrimeClock(TimeProvider.System);
        DateTime notUtc = DateTime.SpecifyKind(new DateTime(2025, 1, 1, 12, 0, 0), DateTimeKind.Unspecified);
        Action act = () => _ = time.ToScheduleDateTimeOffset(notUtc);
        act.Should().Throw<ArgumentException>().WithParameterName("utcDateTime");
    }

    /// <summary>
    ///   Verifies <see cref="DateTimeKind.Local"/> is rejected for the UTC <see cref="DateTime"/> overload.
    /// </summary>
    [Fact]
    public void UtcDateTime_LocalKind_ToScheduleDateTimeOffset_ThrowsArgumentException ()
    {
        IPrimeTime time = new PrimeClock(TimeProvider.System);
        DateTime localKind = DateTime.SpecifyKind(new DateTime(2025, 1, 1, 12, 0, 0), DateTimeKind.Local);
        Action act = () => _ = time.ToScheduleDateTimeOffset(localKind);
        act.Should().Throw<ArgumentException>().WithParameterName("utcDateTime");
    }

    /// <summary>
    ///   Verifies schedule <see cref="DateTimeOffset"/> conversion maps the same absolute instant consistently for
    ///   differing offset representations.
    /// </summary>
    [Fact]
    public void PrimeClock_SystemProvider_DateTimeOffsetsSameInstant_ToScheduleDateTimeOffset_AreEqual ()
    {
        IPrimeTime time = new PrimeClock(TimeProvider.System);
        DateTimeOffset utc = new(2025, 6, 1, 16, 0, 0, TimeSpan.Zero);
        DateTimeOffset withOffset = new(2025, 6, 1, 18, 0, 0, TimeSpan.FromHours(2));
        withOffset.UtcDateTime.Should().Be(utc.UtcDateTime);
        DateTimeOffset a = time.ToScheduleDateTimeOffset(utc);
        DateTimeOffset b = time.ToScheduleDateTimeOffset(withOffset);
        a.Should().Be(b);
    }

    /// <summary>
    ///   Verifies schedule-local wall <see cref="DateTime"/> matches the local component of the schedule
    ///   <see cref="DateTimeOffset"/> for the same absolute instant.
    /// </summary>
    [Fact]
    public void PrimeClock_SystemProvider_ToScheduleLocalWallDateTime_MatchesInZoneOffsetDateTime ()
    {
        IPrimeTime time = new PrimeClock(TimeProvider.System);
        DateTimeOffset instant = new(2025, 6, 1, 16, 0, 0, TimeSpan.Zero);
        DateTimeOffset inZone = time.ToScheduleDateTimeOffset(instant);
        DateTime wall = time.ToScheduleLocalWallDateTime(instant);
        wall.Should().Be(inZone.DateTime);
        wall.Kind.Should().Be(DateTimeKind.Unspecified);
    }

    /// <summary>
    ///   Verifies non-clock receivers fail for schedule-local wall <see cref="DateTime"/> conversion from
    ///   <see cref="DateTimeOffset"/>.
    /// </summary>
    [Fact]
    public void NonClock_ToScheduleLocalWallDateTime_ThrowsArgumentException ()
    {
        IPrimeTime time = new NonClockPrimeTime();
        DateTimeOffset instant = new(2025, 1, 1, 12, 0, 0, TimeSpan.Zero);
        Action act = () => _ = time.ToScheduleLocalWallDateTime(instant);
        act.Should().Throw<ArgumentException>().WithParameterName("time");
    }

#if NET
    /// <summary>
    ///   Verifies schedule <see cref="DateTimeOffset"/> matches <see cref="TimeZoneInfo.ConvertTime(DateTimeOffset, TimeZoneInfo)"/>.
    /// </summary>
    [Fact]
    public void PrimeClock_FakeProvider_ToScheduleDateTimeOffset_MatchesConvertTime ()
    {
        DateTimeOffset utcInstant = new(2025, 6, 1, 16, 0, 0, TimeSpan.Zero);
        FakeTimeProvider fake = new(utcInstant);
        TimeZoneInfo zone = TimeZoneInfo.CreateCustomTimeZone("KZDevSchedule+03", TimeSpan.FromHours(3), null, null);
        fake.SetLocalTimeZone(zone);
        IPrimeTime time = new PrimeClock(fake);
        DateTimeOffset expected = TimeZoneInfo.ConvertTime(utcInstant, zone);
        DateTimeOffset actual = time.ToScheduleDateTimeOffset(utcInstant);
        actual.Should().Be(expected);
    }

    /// <summary>
    ///   Verifies UTC <see cref="DateTime"/> overload matches the <see cref="DateTimeOffset"/> overload.
    /// </summary>
    [Fact]
    public void PrimeClock_FakeProvider_UtcDateTime_ToSchedule_MatchesOffsetOverload ()
    {
        DateTimeOffset utcInstant = new(2025, 6, 1, 16, 0, 0, TimeSpan.Zero);
        FakeTimeProvider fake = new(utcInstant);
        TimeZoneInfo zone = TimeZoneInfo.CreateCustomTimeZone("KZDevSchedule+03", TimeSpan.FromHours(3), null, null);
        fake.SetLocalTimeZone(zone);
        IPrimeTime time = new PrimeClock(fake);
        DateTime utcDateTime = utcInstant.UtcDateTime;
        DateTimeOffset fromOffsetOverload = time.ToScheduleDateTimeOffset(utcInstant);
        DateTimeOffset fromDateTimeOverload = time.ToScheduleDateTimeOffset(utcDateTime);
        fromDateTimeOverload.Should().Be(fromOffsetOverload);
    }

    /// <summary>
    ///   Verifies <see cref="PrimeTimeScheduleZoneExtensions.ToScheduleDateOnly"/> matches wall date in the schedule zone.
    /// </summary>
    [Fact]
    public void PrimeClock_FakeProvider_ToScheduleDateOnly_MatchesWallDate ()
    {
        DateTimeOffset utcInstant = new(2025, 6, 1, 4, 30, 0, TimeSpan.Zero);
        FakeTimeProvider fake = new(utcInstant);
        TimeZoneInfo zone = TimeZoneInfo.CreateCustomTimeZone("KZDevSchedule-04", TimeSpan.FromHours(-4), null, null);
        fake.SetLocalTimeZone(zone);
        IPrimeTime time = new PrimeClock(fake);
        DateTimeOffset inZone = TimeZoneInfo.ConvertTime(utcInstant, zone);
        DateOnly expected = DateOnly.FromDateTime(inZone.DateTime);
        DateOnly actual = time.ToScheduleDateOnly(utcInstant);
        actual.Should().Be(expected);
    }

    /// <summary>
    ///   Verifies <see cref="PrimeTimeScheduleZoneExtensions.ToScheduleTimeOnly"/> matches wall time in the schedule zone.
    /// </summary>
    [Fact]
    public void PrimeClock_FakeProvider_ToScheduleTimeOnly_MatchesWallTime ()
    {
        DateTimeOffset utcInstant = new(2025, 6, 1, 4, 30, 0, TimeSpan.Zero);
        FakeTimeProvider fake = new(utcInstant);
        TimeZoneInfo zone = TimeZoneInfo.CreateCustomTimeZone("KZDevSchedule-04", TimeSpan.FromHours(-4), null, null);
        fake.SetLocalTimeZone(zone);
        IPrimeTime time = new PrimeClock(fake);
        DateTimeOffset inZone = TimeZoneInfo.ConvertTime(utcInstant, zone);
        TimeOnly expected = TimeOnly.FromDateTime(inZone.DateTime);
        TimeOnly actual = time.ToScheduleTimeOnly(utcInstant);
        actual.Should().Be(expected);
    }

    /// <summary>
    ///   Verifies UTC <see cref="DateTime"/> wall projection matches conversion from the same instant as
    ///   <see cref="DateTimeOffset"/>.
    /// </summary>
    [Fact]
    public void PrimeClock_FakeProvider_UtcDateTime_ToScheduleLocalWallDateTime_MatchesOffsetOverload ()
    {
        DateTimeOffset utcInstant = new(2025, 6, 1, 16, 0, 0, TimeSpan.Zero);
        FakeTimeProvider fake = new(utcInstant);
        TimeZoneInfo zone = TimeZoneInfo.CreateCustomTimeZone("KZDevSchedule+03b", TimeSpan.FromHours(3), null, null);
        fake.SetLocalTimeZone(zone);
        IPrimeTime time = new PrimeClock(fake);
        DateTime utcDateTime = utcInstant.UtcDateTime;
        DateTime fromOffsetOverload = time.ToScheduleLocalWallDateTime(utcInstant);
        DateTime fromDateTimeOverload = time.ToScheduleLocalWallDateTime(utcDateTime);
        fromDateTimeOverload.Should().Be(fromOffsetOverload);
    }
#endif
}
//################################################################################
