// Copyright (c) Kevin Zehrer
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
//
// Deterministic tests using Microsoft.Extensions.TimeProvider.Testing.FakeTimeProvider (NET only).
// The package is not available for netstandard2.0.

using AwesomeAssertions;
using KZDev.PrimeTime.Tests;

#if NET
using Microsoft.Extensions.Time.Testing;
#endif

namespace KZDev.SystemClock.PrimeTime.UnitTests;

public partial class UsingIPrimeSystemClock
{
#if NET
    /// <summary>
    ///   Verifies that <see cref="PrimeSystemClock"/> with a <see cref="FakeTimeProvider"/> at a fixed
    ///   UTC time yields consistent "now" values: UtcNow matches the set time, LocalNow reflects
    ///   the provider's local zone, and DateTime/DateTimeOffset members are consistent.
    /// </summary>
    [Fact]
    public void PrimeSystemClock_WithFakeTimeProvider_NowMembersAreConsistentWithFixedUtc ()
    {
        DateTimeOffset fixedUtc = new(2025, 3, 7, 12, 0, 0, TimeSpan.Zero);
        FakeTimeProvider fake = new(fixedUtc);
        IPrimeSystemClock clock = new PrimeSystemClock(fake);

        clock.UtcNow.Should().Be(fixedUtc);
        clock.UtcNow.Offset.Should().Be(TimeSpan.Zero);
        clock.UtcDateTimeNow.Should().Be(fixedUtc.UtcDateTime);
        clock.UtcDateTimeNow.Kind.Should().Be(DateTimeKind.Utc);
        clock.LocalNow.Should().Be(fake.GetLocalNow());
        clock.LocalNow.Offset.Should().Be(fake.GetLocalNow().Offset);
        clock.LocalDateTimeNow.Should().Be(clock.LocalNow.LocalDateTime);
        clock.LocalDateTimeNow.Kind.Should().Be(DateTimeKind.Local);
    }

    /// <summary>
    ///   Verifies that at a fixed UTC instant, <see cref="PrimeSystemClock"/> with a
    ///   <see cref="FakeTimeProvider"/> (with local zone set to UTC) returns exact time-only and
    ///   date-only "now" values.
    /// </summary>
    [Fact]
    public void PrimeSystemClock_WithFakeTimeProvider_TimeOnlyAndDateOnlyReturnExactValuesAtFixedInstant ()
    {
        DateTimeOffset fixedUtc = new(2025, 3, 7, 12, 30, 45, TimeSpan.Zero);
        FakeTimeProvider fake = new(fixedUtc);
        fake.SetLocalTimeZone(TimeZoneInfo.Utc);
        IPrimeSystemClock clock = new PrimeSystemClock(fake);

        clock.UtcNowTime.Should().Be(TimeOnly.FromDateTime(fixedUtc.UtcDateTime));
        clock.UtcNowDate.Should().Be(DateOnly.FromDateTime(fixedUtc.UtcDateTime));
        clock.LocalNowTime.Should().Be(clock.UtcNowTime);
        clock.LocalNowDate.Should().Be(clock.UtcNowDate);
        clock.UtcNowTime.Should().Be(new(12, 30, 45));
        clock.UtcNowDate.Should().Be(new(2025, 3, 7));
    }

    /// <summary>
    ///   Verifies that advancing a <see cref="FakeTimeProvider"/> used by <see cref="PrimeSystemClock"/>
    ///   updates all "now" values (UtcNow, LocalNow, DateTime, and on .NET the time/date components).
    /// </summary>
    [Fact]
    public void PrimeSystemClock_WithFakeTimeProvider_AdvanceUpdatesNow ()
    {
        DateTimeOffset initial = new(2025, 3, 7, 10, 0, 0, TimeSpan.Zero);
        TimeSpan advanceBy = TimeSpan.FromHours(2).Add(TimeSpan.FromMinutes(30));
        DateTimeOffset expectedAfter = initial.Add(advanceBy);
        FakeTimeProvider fake = new(initial);
        fake.SetLocalTimeZone(TimeZoneInfo.Utc);
        IPrimeSystemClock clock = new PrimeSystemClock(fake);

        clock.UtcNow.Should().Be(initial);
        clock.UtcNowDate.Should().Be(new(2025, 3, 7));
        clock.UtcNowTime.Should().Be(new(10, 0, 0));

        fake.Advance(advanceBy);

        clock.UtcNow.Should().Be(expectedAfter);
        clock.UtcDateTimeNow.Should().Be(expectedAfter.UtcDateTime);
        clock.UtcNowDate.Should().Be(new(2025, 3, 7));
        clock.UtcNowTime.Should().Be(new(12, 30, 0));
        clock.LocalNow.Should().Be(fake.GetLocalNow());
    }

    /// <summary>
    ///   Verifies that <see cref="FakeTimeProvider"/> default start (midnight 2000-01-01 UTC) is
    ///   reflected by <see cref="PrimeSystemClock"/> when no explicit time is set.
    /// </summary>
    [Fact]
    public void PrimeSystemClock_WithFakeTimeProvider_StartIsUsedWhenNoSetUtcNow ()
    {
        FakeTimeProvider fake = new();
        IPrimeSystemClock clock = new PrimeSystemClock(fake);

        clock.UtcNow.Should().Be(fake.Start);
        clock.UtcNow.Offset.Should().Be(TimeSpan.Zero);
        clock.UtcDateTimeNow.Year.Should().Be(2000);
        clock.UtcDateTimeNow.Month.Should().Be(1);
        clock.UtcDateTimeNow.Day.Should().Be(1);
    }

    /// <summary>
    ///   Verifies that <see cref="FakeTimeProvider.SetUtcNow"/> is reflected in all
    ///   <see cref="IPrimeSystemClock"/> "now" members: UTC values match the set time,
    ///   and local values represent the same moment (same UtcDateTime) with the provider's offset.
    /// </summary>
    [Fact]
    public void PrimeSystemClock_WithFakeTimeProvider_SetUtcNow_ReflectedInAllNowMembers ()
    {
        DateTimeOffset setTime = new(2030, 6, 15, 14, 45, 30, TimeSpan.Zero);
        FakeTimeProvider fake = new();
        fake.SetUtcNow(setTime);
        IPrimeSystemClock clock = new PrimeSystemClock(fake);

        clock.UtcNow.Should().Be(setTime);
        clock.UtcDateTimeNow.Should().Be(setTime.UtcDateTime);
        clock.LocalNow.UtcDateTime.Should().Be(setTime.UtcDateTime, "local and UTC now must be the same moment");
        clock.LocalDateTimeNow.Should().Be(clock.LocalNow.LocalDateTime);
        clock.UtcNowTime.Should().Be(TimeOnly.FromDateTime(setTime.UtcDateTime));
        clock.UtcNowDate.Should().Be(DateOnly.FromDateTime(setTime.UtcDateTime));
        clock.LocalNowTime.Should().Be(TimeOnly.FromDateTime(clock.LocalNow.DateTime));
        clock.LocalNowDate.Should().Be(DateOnly.FromDateTime(clock.LocalNow.DateTime));
    }

    /// <summary>
    ///   Verifies that <see cref="FakeTimeProvider.SetLocalTimeZone"/> affects
    ///   <see cref="IPrimeSystemClock.LocalNow"/> and related local members so they match the
    ///   configured zone offset.
    /// </summary>
    [Fact]
    public void PrimeSystemClock_WithFakeTimeProvider_LocalTimeZone_AffectsLocalNow ()
    {
        DateTimeOffset fixedUtc = new(2025, 3, 7, 12, 0, 0, TimeSpan.Zero);
        FakeTimeProvider fake = new(fixedUtc);
        TimeZoneInfo plusTwo = TimeZoneInfo.CreateCustomTimeZone("+02", TimeSpan.FromHours(2), null, null);
        fake.SetLocalTimeZone(plusTwo);

        IPrimeSystemClock clock = new PrimeSystemClock(fake);
        DateTimeOffset localNow = clock.LocalNow;
        localNow.Offset.Should().Be(TimeSpan.FromHours(2));
        localNow.UtcDateTime.Should().Be(fixedUtc.UtcDateTime);
        clock.LocalDateTimeNow.Kind.Should().Be(DateTimeKind.Local);
    }

    /// <summary>
    ///   Verifies that multiple <see cref="FakeTimeProvider.Advance"/> calls accumulate and
    ///   <see cref="PrimeSystemClock"/> always reflects the current fake time.
    /// </summary>
    [Fact]
    public void PrimeSystemClock_WithFakeTimeProvider_MultipleAdvances_Accumulate ()
    {
        DateTimeOffset start = new(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);
        FakeTimeProvider fake = new(start);
        IPrimeSystemClock clock = new PrimeSystemClock(fake);

        clock.UtcNow.Should().Be(start);
        fake.Advance(TimeSpan.FromDays(1));
        clock.UtcNow.Should().Be(start.AddDays(1));
        fake.Advance(TimeSpan.FromHours(12));
        clock.UtcNow.Should().Be(start.AddDays(1).AddHours(12));
        fake.Advance(TimeSpan.FromMinutes(30));
        clock.UtcNow.Should().Be(start.AddDays(1).AddHours(12).AddMinutes(30));
    }
#endif
}
