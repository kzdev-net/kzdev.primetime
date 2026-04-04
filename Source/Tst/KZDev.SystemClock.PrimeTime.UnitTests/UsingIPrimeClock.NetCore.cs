// Copyright (c) Kevin Zehrer
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using AwesomeAssertions;
using KZDev.PrimeTime.Tests;

#if NET
using Microsoft.Extensions.Time.Testing;
#endif

namespace KZDev.SystemClock.PrimeTime.UnitTests;

public partial class UsingIPrimeClock
{
#if NET
    /// <summary>
    ///   Verifies that <see cref="PrimeClock"/> with a <see cref="FakeTimeProvider"/> at a fixed
    ///   UTC time yields consistent "now" values: UtcNowOffset matches the set time, LocalNowOffset reflects
    ///   the provider's local zone, and DateTime/DateTimeOffset members are consistent.
    /// </summary>
    [Fact]
    public void PrimeClock_WithFakeTimeProvider_NowMembersAreConsistentWithFixedUtc ()
    {
        DateTimeOffset fixedUtc = new(2025, 3, 7, 12, 0, 0, TimeSpan.Zero);
        FakeTimeProvider fake = new(fixedUtc);
        IPrimeClock clock = new PrimeClock(fake);

        clock.UtcNowOffset.Should().Be(fixedUtc);
        clock.UtcNowOffset.Offset.Should().Be(TimeSpan.Zero);
        clock.UtcNowDateTime.Should().Be(fixedUtc.UtcDateTime);
        clock.UtcNowDateTime.Kind.Should().Be(DateTimeKind.Utc);
        clock.LocalNowOffset.Should().Be(fake.GetLocalNow());
        clock.LocalNowOffset.Offset.Should().Be(fake.GetLocalNow().Offset);
        clock.LocalNowDateTime.Should().Be(clock.LocalNowOffset.LocalDateTime);
        clock.LocalNowDateTime.Kind.Should().Be(DateTimeKind.Local);
    }

    /// <summary>
    ///   Verifies that at a fixed UTC instant, <see cref="PrimeClock"/> with a
    ///   <see cref="FakeTimeProvider"/> (with local zone set to UTC) returns exact time-only and
    ///   date-only "now" values.
    /// </summary>
    [Fact]
    public void PrimeClock_WithFakeTimeProvider_TimeOnlyAndDateOnlyReturnExactValuesAtFixedInstant ()
    {
        DateTimeOffset fixedUtc = new(2025, 3, 7, 12, 30, 45, TimeSpan.Zero);
        FakeTimeProvider fake = new(fixedUtc);
        fake.SetLocalTimeZone(TimeZoneInfo.Utc);
        IPrimeClock clock = new PrimeClock(fake);

        clock.UtcNowTimeOnly.Should().Be(TimeOnly.FromDateTime(fixedUtc.UtcDateTime));
        clock.UtcNowDateOnly.Should().Be(DateOnly.FromDateTime(fixedUtc.UtcDateTime));
        clock.LocalNowTimeOnly.Should().Be(clock.UtcNowTimeOnly);
        clock.LocalNowDateOnly.Should().Be(clock.UtcNowDateOnly);
        clock.UtcNowTimeOnly.Should().Be(new(12, 30, 45));
        clock.UtcNowDateOnly.Should().Be(new(2025, 3, 7));
    }

    /// <summary>
    ///   Verifies that advancing a <see cref="FakeTimeProvider"/> used by <see cref="PrimeClock"/>
    ///   updates all "now" values (UtcNowOffset, LocalNowOffset, DateTime, and on .NET the time/date components).
    /// </summary>
    [Fact]
    public void PrimeClock_WithFakeTimeProvider_AdvanceUpdatesNow ()
    {
        DateTimeOffset initial = new(2025, 3, 7, 10, 0, 0, TimeSpan.Zero);
        TimeSpan advanceBy = TimeSpan.FromHours(2).Add(TimeSpan.FromMinutes(30));
        DateTimeOffset expectedAfter = initial.Add(advanceBy);
        FakeTimeProvider fake = new(initial);
        fake.SetLocalTimeZone(TimeZoneInfo.Utc);
        IPrimeClock clock = new PrimeClock(fake);

        clock.UtcNowOffset.Should().Be(initial);
        clock.UtcNowDateOnly.Should().Be(new(2025, 3, 7));
        clock.UtcNowTimeOnly.Should().Be(new(10, 0, 0));

        fake.Advance(advanceBy);

        clock.UtcNowOffset.Should().Be(expectedAfter);
        clock.UtcNowDateTime.Should().Be(expectedAfter.UtcDateTime);
        clock.UtcNowDateOnly.Should().Be(new(2025, 3, 7));
        clock.UtcNowTimeOnly.Should().Be(new(12, 30, 0));
        clock.LocalNowOffset.Should().Be(fake.GetLocalNow());
    }

    /// <summary>
    ///   Verifies that <see cref="FakeTimeProvider"/> default start (midnight 2000-01-01 UTC) is
    ///   reflected by <see cref="PrimeClock"/> when no explicit time is set.
    /// </summary>
    [Fact]
    public void PrimeClock_WithFakeTimeProvider_StartIsUsedWhenNoSetUtcNow ()
    {
        FakeTimeProvider fake = new();
        IPrimeClock clock = new PrimeClock(fake);

        clock.UtcNowOffset.Should().Be(fake.Start);
        clock.UtcNowOffset.Offset.Should().Be(TimeSpan.Zero);
        clock.UtcNowDateTime.Year.Should().Be(2000);
        clock.UtcNowDateTime.Month.Should().Be(1);
        clock.UtcNowDateTime.Day.Should().Be(1);
    }

    /// <summary>
    ///   Verifies that <see cref="FakeTimeProvider.SetUtcNow"/> is reflected in all
    ///   <see cref="IPrimeClock"/> "now" members: UTC values match the set time,
    ///   and local values represent the same moment (same UtcDateTime) with the provider's offset.
    /// </summary>
    [Fact]
    public void PrimeClock_WithFakeTimeProvider_SetUtcNow_ReflectedInAllNowMembers ()
    {
        DateTimeOffset setTime = new(2030, 6, 15, 14, 45, 30, TimeSpan.Zero);
        FakeTimeProvider fake = new();
        fake.SetUtcNow(setTime);
        IPrimeClock clock = new PrimeClock(fake);

        clock.UtcNowOffset.Should().Be(setTime);
        clock.UtcNowDateTime.Should().Be(setTime.UtcDateTime);
        clock.LocalNowOffset.UtcDateTime.Should().Be(setTime.UtcDateTime, "local and UTC now must be the same moment");
        clock.LocalNowDateTime.Should().Be(clock.LocalNowOffset.LocalDateTime);
        clock.UtcNowTimeOnly.Should().Be(TimeOnly.FromDateTime(setTime.UtcDateTime));
        clock.UtcNowDateOnly.Should().Be(DateOnly.FromDateTime(setTime.UtcDateTime));
        clock.LocalNowTimeOnly.Should().Be(TimeOnly.FromDateTime(clock.LocalNowOffset.DateTime));
        clock.LocalNowDateOnly.Should().Be(DateOnly.FromDateTime(clock.LocalNowOffset.DateTime));
    }

    /// <summary>
    ///   Verifies that <see cref="FakeTimeProvider.SetLocalTimeZone"/> affects
    ///   <see cref="IPrimeClock.LocalNowOffset"/> and related local members so they match the
    ///   configured zone offset.
    /// </summary>
    [Fact]
    public void PrimeClock_WithFakeTimeProvider_LocalTimeZone_AffectsLocalNow ()
    {
        DateTimeOffset fixedUtc = new(2025, 3, 7, 12, 0, 0, TimeSpan.Zero);
        FakeTimeProvider fake = new(fixedUtc);
        TimeZoneInfo plusTwo = TimeZoneInfo.CreateCustomTimeZone("+02", TimeSpan.FromHours(2), null, null);
        fake.SetLocalTimeZone(plusTwo);

        IPrimeClock clock = new PrimeClock(fake);
        DateTimeOffset LocalNowOffset = clock.LocalNowOffset;
        LocalNowOffset.Offset.Should().Be(TimeSpan.FromHours(2));
        LocalNowOffset.UtcDateTime.Should().Be(fixedUtc.UtcDateTime);
        clock.LocalNowDateTime.Kind.Should().Be(DateTimeKind.Local);
    }

    /// <summary>
    ///   Verifies that multiple <see cref="FakeTimeProvider.Advance"/> calls accumulate and
    ///   <see cref="PrimeClock"/> always reflects the current fake time.
    /// </summary>
    [Fact]
    public void PrimeClock_WithFakeTimeProvider_MultipleAdvances_Accumulate ()
    {
        DateTimeOffset start = new(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);
        FakeTimeProvider fake = new(start);
        IPrimeClock clock = new PrimeClock(fake);

        clock.UtcNowOffset.Should().Be(start);
        fake.Advance(TimeSpan.FromDays(1));
        clock.UtcNowOffset.Should().Be(start.AddDays(1));
        fake.Advance(TimeSpan.FromHours(12));
        clock.UtcNowOffset.Should().Be(start.AddDays(1).AddHours(12));
        fake.Advance(TimeSpan.FromMinutes(30));
        clock.UtcNowOffset.Should().Be(start.AddDays(1).AddHours(12).AddMinutes(30));
    }
#endif
}

