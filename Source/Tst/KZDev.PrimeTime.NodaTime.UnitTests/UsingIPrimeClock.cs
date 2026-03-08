// Copyright (c) Kevin Zehrer
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using AwesomeAssertions;
using KZDev.PrimeTime.Tests;
using NodaTime;

namespace KZDev.PrimeTime.NodaTime.UnitTests;

/// <summary>
///   Unit tests for <see cref="IPrimeClock"/> and <see cref="PrimeClock"/> (Phase 3).
///   Verifies that all "now" members return values consistent with a known instant and time zone.
/// </summary>
public class UsingIPrimeClock : UnitTestBase
{
    /// <summary>
    ///   Initializes a new instance of the <see cref="UsingIPrimeClock"/> class.
    /// </summary>
    /// <param name="xUnitTestOutputHelper">
    ///   The xUnit test output helper that can be used to output test messages.
    /// </param>
    public UsingIPrimeClock (ITestOutputHelper xUnitTestOutputHelper)
        : base(xUnitTestOutputHelper)
    {
    }

    /// <summary>
    ///   Verifies that <see cref="IPrimeClock"/> extends <see cref="IPrimeTime"/>.
    /// </summary>
    [Fact]
    public void IPrimeClock_ExtendsIPrimeTime ()
    {
        typeof(IPrimeClock).GetInterfaces().Should().Contain(typeof(IPrimeTime));
    }

    /// <summary>
    ///   Verifies that <see cref="PrimeClock"/> implements <see cref="IPrimeClock"/>.
    /// </summary>
    [Fact]
    public void PrimeClock_ImplementsIPrimeClock ()
    {
        IPrimeClock clock = new PrimeClock();
        clock.Should().NotBeNull();
    }

    /// <summary>
    ///   Verifies that a stub clock returning a fixed instant yields consistent "now" values:
    ///   Instant matches UtcNow.ToInstant(), LocalZonedNow and LocalNow derive from the same instant,
    ///   and time/date components match the zoned values.
    /// </summary>
    [Fact]
    public void StubClock_NowMembersAreConsistentWithFixedInstant ()
    {
        Instant instant = Instant.FromUtc(2025, 3, 7, 12, 0);
        DateTimeZone utc = DateTimeZone.Utc;
        DateTimeZone localZone = DateTimeZoneProviders.Bcl.GetSystemDefault();
        IPrimeClock clock = new StubPrimeClock(instant, localZone);

        clock.Instant.Should().Be(instant);
        clock.UtcNow.ToInstant().Should().Be(instant);
        clock.UtcZonedNow.ToInstant().Should().Be(instant);
        clock.LocalZonedNow.ToInstant().Should().Be(instant);
        clock.LocalZonedNow.Zone.Should().Be(localZone);
        clock.LocalNow.Should().Be(clock.LocalZonedNow.LocalDateTime);
        clock.UtcNowTime.Should().Be(clock.UtcNow.TimeOfDay);
        clock.UtcNowDate.Should().Be(clock.UtcNow.Date);
        clock.LocalNowTime.Should().Be(clock.LocalZonedNow.TimeOfDay);
        clock.LocalNowDate.Should().Be(clock.LocalZonedNow.Date);
    }

    /// <summary>
    ///   Verifies that <see cref="PrimeClock"/> UTC "now" members are consistent: zone is UTC,
    ///   UtcZonedNow is in UTC, and UtcNow time/date components match UtcNowTime and UtcNowDate.
    ///   Time is asserted within a 2-second tolerance because each property read obtains a fresh
    ///   instant; date must match exactly (same calendar day).
    /// </summary>
    [Fact]
    public void PrimeClock_UtcNowMembersAreConsistent ()
    {
        IPrimeClock clock = new PrimeClock();
        ZonedDateTime utcNow = clock.UtcNow;
        LocalTime utcNowTime = clock.UtcNowTime;
        LocalDate utcNowDate = clock.UtcNowDate;

        utcNow.Zone.Should().Be(DateTimeZone.Utc);
        clock.UtcZonedNow.Zone.Should().Be(DateTimeZone.Utc);
        long nanoDiff = Math.Abs(utcNow.TimeOfDay.NanosecondOfDay - utcNowTime.NanosecondOfDay);
        nanoDiff.Should().BeLessThanOrEqualTo(2_000_000_000L, "time of day should be within 2 seconds across sequential reads");
        utcNow.Date.Year.Should().Be(utcNowDate.Year);
        utcNow.Date.Month.Should().Be(utcNowDate.Month);
        utcNow.Date.Day.Should().Be(utcNowDate.Day);
    }

    /// <summary>
    ///   Verifies that <see cref="PrimeClock"/> local "now" members are consistent:
    ///   LocalZonedNow has the system default zone and its LocalDateTime, TimeOfDay, and Date
    ///   are internally consistent.
    /// </summary>
    [Fact]
    public void PrimeClock_LocalNowMembersAreConsistent ()
    {
        IPrimeClock clock = new PrimeClock();
        ZonedDateTime localZonedNow = clock.LocalZonedNow;

        localZonedNow.Zone.Should().Be(DateTimeZoneProviders.Bcl.GetSystemDefault());
        localZonedNow.LocalDateTime.TimeOfDay.Should().Be(localZonedNow.TimeOfDay);
        localZonedNow.LocalDateTime.Date.Should().Be(localZonedNow.Date);
    }

    /// <summary>
    ///   Verifies that all "now" values from <see cref="PrimeClock"/> are recent (within the last 5 seconds).
    /// </summary>
    [Fact]
    public void PrimeClock_NowMembersAreRecent ()
    {
        IPrimeClock clock = new PrimeClock();
        Instant before = SystemClock.Instance.GetCurrentInstant().Minus(Duration.FromSeconds(5));
        Instant after = SystemClock.Instance.GetCurrentInstant().Plus(Duration.FromSeconds(5));

        clock.Instant.Should().BeGreaterThan(before).And.BeLessThan(after);
    }

    /// <summary>
    ///   Verifies that <see cref="IPrimeTime.Sleep(TimeSpan)"/> throws
    ///   <see cref="NotSupportedException"/> on <see cref="PrimeClock"/> in this phase.
    /// </summary>
    [Fact]
    public void PrimeClock_Sleep_ThrowsNotSupportedException ()
    {
        IPrimeClock clock = new PrimeClock();
        Action act = () => clock.Sleep(TimeSpan.Zero);
        act.Should().Throw<NotSupportedException>();
    }

    /// <summary>
    ///   Verifies that <see cref="IPrimeTime.DelayAsync(TimeSpan)"/> throws
    ///   <see cref="NotSupportedException"/> on <see cref="PrimeClock"/> in this phase.
    /// </summary>
    [Fact]
    public void PrimeClock_DelayAsync_ThrowsNotSupportedException ()
    {
        IPrimeClock clock = new PrimeClock();
        Func<Task> act = async () => await clock.DelayAsync(TimeSpan.Zero);
        act.Should().ThrowAsync<NotSupportedException>();
    }

    /// <summary>
    ///   Minimal stub implementation of <see cref="IPrimeClock"/> that returns a fixed
    ///   <see cref="Instant"/> and derived values in the given zone, for contract tests.
    /// </summary>
    private sealed class StubPrimeClock : IPrimeClock
    {
        private readonly Instant _instant;
        private readonly DateTimeZone _localZone;

        internal StubPrimeClock (Instant instant, DateTimeZone localZone)
        {
            _instant = instant;
            _localZone = localZone;
        }

        public Instant Instant => _instant;
        public ZonedDateTime UtcNow => _instant.InUtc();
        public ZonedDateTime LocalZonedNow => _instant.InZone(_localZone);
        public ZonedDateTime UtcZonedNow => UtcNow;
        public LocalDateTime LocalNow => LocalZonedNow.LocalDateTime;
        public LocalTime LocalNowTime => LocalZonedNow.TimeOfDay;
        public LocalTime UtcNowTime => UtcNow.TimeOfDay;
        public LocalDate LocalNowDate => LocalZonedNow.Date;
        public LocalDate UtcNowDate => UtcNow.Date;

        public void Sleep (TimeSpan sleepTime) => throw new NotSupportedException();
        public void Sleep (int sleepMilliseconds) => throw new NotSupportedException();
        public Task DelayAsync (TimeSpan delayTime) => throw new NotSupportedException();
        public Task DelayAsync (int millisecondsDelay) => throw new NotSupportedException();
        public Task DelayAsync (TimeSpan delayTime, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
        public Task DelayAsync (int millisecondsDelay, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
        public CancellationToken GetTimeCancellationToken (TimeSpan cancelTime) =>
            throw new NotSupportedException();
        public CancellationToken GetTimeCancellationToken (int cancelMilliseconds) =>
            throw new NotSupportedException();
        public CancellationToken LinkTimeCancellationToken (TimeSpan cancelTime, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
        public CancellationToken LinkTimeCancellationToken (int cancelMilliseconds, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
        public CancellationToken LinkTimeCancellationToken (int cancelMilliseconds,
            CancellationToken token1,
            CancellationToken token2) =>
            throw new NotSupportedException();
        public CancellationToken LinkTimeCancellationToken (TimeSpan cancelTime,
            CancellationToken token1,
            CancellationToken token2) =>
            throw new NotSupportedException();
        public CancellationToken LinkTimeCancellationToken (TimeSpan cancelTime, params CancellationToken[] cancellationTokens) =>
            throw new NotSupportedException();
        public CancellationToken LinkTimeCancellationToken (int cancelMilliseconds, params CancellationToken[] cancellationTokens) =>
            throw new NotSupportedException();
    }
}
