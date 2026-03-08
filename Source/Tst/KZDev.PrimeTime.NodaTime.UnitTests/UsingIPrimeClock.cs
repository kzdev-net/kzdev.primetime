// Copyright (c) Kevin Zehrer
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using AwesomeAssertions;
using KZDev.PrimeTime.Tests;
using NodaTime;
using NodaTime.Testing;

namespace KZDev.PrimeTime.NodaTime.UnitTests;

/// <summary>
///   Unit tests for <see cref="IPrimeClock"/> and <see cref="PrimeClock"/>.
///   Verifies that all "now" members return values consistent with a known instant and time zone.
///   Uses <see cref="FakeClock"/> from NodaTime.Testing for deterministic tests with
///   <see cref="PrimeClock"/>.
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
    ///   Verifies that <see cref="PrimeClock"/> with a <see cref="FakeClock"/> at a fixed instant
    ///   yields consistent "now" values: Instant matches UtcNow.ToInstant(), LocalZonedNow and
    ///   LocalNow derive from the same instant, and time/date components match the zoned values.
    /// </summary>
    [Fact]
    public void PrimeClock_WithFakeClock_NowMembersAreConsistentWithFixedInstant ()
    {
        Instant instant = Instant.FromUtc(2025, 3, 7, 12, 0);
        DateTimeZone localZone = DateTimeZoneProviders.Bcl.GetSystemDefault();
        IPrimeClock clock = new PrimeClock(new FakeClock(instant), localZone);

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
    ///   Verifies that at a fixed instant, <see cref="PrimeClock"/> with a <see cref="FakeClock"/>
    ///   returns exact time-only and date-only "now" values: UtcNowTime/UtcNowDate, and
    ///   LocalNowTime/LocalNowDate matching the same instant when using UTC as the local zone.
    /// </summary>
    [Fact]
    public void PrimeClock_WithFakeClock_TimeOnlyAndDateOnlyReturnExactValuesAtFixedInstant ()
    {
        Instant instant = Instant.FromUtc(2025, 3, 7, 12, 30, 45);
        DateTimeZone utc = DateTimeZone.Utc;
        IPrimeClock clock = new PrimeClock(new FakeClock(instant), utc);

        clock.UtcNowTime.Should().Be(new LocalTime(12, 30, 45));
        clock.UtcNowDate.Should().Be(new LocalDate(2025, 3, 7));
        clock.LocalNowTime.Should().Be(clock.UtcNowTime);
        clock.LocalNowDate.Should().Be(clock.UtcNowDate);
    }

    /// <summary>
    ///   Verifies that advancing a <see cref="FakeClock"/> used by <see cref="PrimeClock"/> updates
    ///   all "now" values (Instant, UtcNow, LocalZonedNow, time/date components) to the new instant.
    /// </summary>
    [Fact]
    public void PrimeClock_WithFakeClock_AdvanceUpdatesNow ()
    {
        Instant initial = Instant.FromUtc(2025, 3, 7, 10, 0, 0);
        Duration advanceBy = Duration.FromHours(2).Plus(Duration.FromMinutes(30));
        Instant expectedAfter = initial.Plus(advanceBy);
        DateTimeZone utc = DateTimeZone.Utc;
        FakeClock fakeClock = new FakeClock(initial);
        IPrimeClock clock = new PrimeClock(fakeClock, utc);

        clock.Instant.Should().Be(initial);
        clock.UtcNowDate.Should().Be(new LocalDate(2025, 3, 7));
        clock.UtcNowTime.Should().Be(new LocalTime(10, 0, 0));

        fakeClock.Advance(advanceBy);

        clock.Instant.Should().Be(expectedAfter);
        clock.UtcNow.ToInstant().Should().Be(expectedAfter);
        clock.UtcNowDate.Should().Be(new LocalDate(2025, 3, 7));
        clock.UtcNowTime.Should().Be(new LocalTime(12, 30, 0));
        clock.LocalZonedNow.ToInstant().Should().Be(expectedAfter);
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

}
