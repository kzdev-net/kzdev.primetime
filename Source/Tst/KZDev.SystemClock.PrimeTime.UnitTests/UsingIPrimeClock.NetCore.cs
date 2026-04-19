// Copyright (c) Kevin Zehrer
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

#if NET
using Microsoft.Extensions.Time.Testing;
#endif

namespace KZDev.SystemClock.PrimeTime.UnitTests;

//################################################################################
public partial class UsingIPrimeClock
{
#if NET
    /// <summary>
    ///   Verifies that <see cref="PrimeClock"/> with a <see cref="FakeTimeProvider"/> at a fixed
    ///   UTC time yields consistent "now" values: UtcNowDateTimeOffset matches the set time, LocalNowDateTimeOffset reflects
    ///   the provider's local zone, and DateTime/DateTimeOffset members are consistent.
    /// </summary>
    [Fact]
    public void PrimeClock_WithFakeTimeProvider_NowMembersAreConsistentWithFixedUtc ()
    {
        DateTimeOffset fixedUtc = new(2025, 3, 7, 12, 0, 0, TimeSpan.Zero);
        FakeTimeProvider fake = new(fixedUtc);
        IPrimeClock clock = new PrimeClock(fake);

        clock.UtcNowDateTimeOffset.Should().Be(fixedUtc);
        clock.UtcNowDateTimeOffset.Offset.Should().Be(TimeSpan.Zero);
        clock.UtcNowDateTime.Should().Be(fixedUtc.UtcDateTime);
        clock.UtcNowDateTime.Kind.Should().Be(DateTimeKind.Utc);
        clock.LocalNowDateTimeOffset.Should().Be(fake.GetLocalNow());
        clock.LocalNowDateTimeOffset.Offset.Should().Be(fake.GetLocalNow().Offset);
        clock.LocalNowDateTime.Should().Be(clock.LocalNowDateTimeOffset.LocalDateTime);
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
    ///   updates all "now" values (UtcNowDateTimeOffset, LocalNowDateTimeOffset, DateTime, and on .NET the time/date components).
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

        clock.UtcNowDateTimeOffset.Should().Be(initial);
        clock.UtcNowDateOnly.Should().Be(new(2025, 3, 7));
        clock.UtcNowTimeOnly.Should().Be(new(10, 0, 0));

        fake.Advance(advanceBy);

        clock.UtcNowDateTimeOffset.Should().Be(expectedAfter);
        clock.UtcNowDateTime.Should().Be(expectedAfter.UtcDateTime);
        clock.UtcNowDateOnly.Should().Be(new(2025, 3, 7));
        clock.UtcNowTimeOnly.Should().Be(new(12, 30, 0));
        clock.LocalNowDateTimeOffset.Should().Be(fake.GetLocalNow());
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

        clock.UtcNowDateTimeOffset.Should().Be(fake.Start);
        clock.UtcNowDateTimeOffset.Offset.Should().Be(TimeSpan.Zero);
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

        clock.UtcNowDateTimeOffset.Should().Be(setTime);
        clock.UtcNowDateTime.Should().Be(setTime.UtcDateTime);
        clock.LocalNowDateTimeOffset.UtcDateTime.Should().Be(setTime.UtcDateTime, "local and UTC now must be the same moment");
        clock.LocalNowDateTime.Should().Be(clock.LocalNowDateTimeOffset.LocalDateTime);
        clock.UtcNowTimeOnly.Should().Be(TimeOnly.FromDateTime(setTime.UtcDateTime));
        clock.UtcNowDateOnly.Should().Be(DateOnly.FromDateTime(setTime.UtcDateTime));
        clock.LocalNowTimeOnly.Should().Be(TimeOnly.FromDateTime(clock.LocalNowDateTimeOffset.DateTime));
        clock.LocalNowDateOnly.Should().Be(DateOnly.FromDateTime(clock.LocalNowDateTimeOffset.DateTime));
    }

    /// <summary>
    ///   Verifies that <see cref="FakeTimeProvider.SetLocalTimeZone"/> affects
    ///   <see cref="IPrimeClock.LocalNowDateTimeOffset"/> and related local members so they match the
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
        DateTimeOffset LocalNowDateTimeOffset = clock.LocalNowDateTimeOffset;
        LocalNowDateTimeOffset.Offset.Should().Be(TimeSpan.FromHours(2));
        LocalNowDateTimeOffset.UtcDateTime.Should().Be(fixedUtc.UtcDateTime);
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

        clock.UtcNowDateTimeOffset.Should().Be(start);
        fake.Advance(TimeSpan.FromDays(1));
        clock.UtcNowDateTimeOffset.Should().Be(start.AddDays(1));
        fake.Advance(TimeSpan.FromHours(12));
        clock.UtcNowDateTimeOffset.Should().Be(start.AddDays(1).AddHours(12));
        fake.Advance(TimeSpan.FromMinutes(30));
        clock.UtcNowDateTimeOffset.Should().Be(start.AddDays(1).AddHours(12).AddMinutes(30));
    }

    /// <summary>
    ///   Verifies that <see cref="IPrimeClock.LocalScheduleTimeZone"/> matches
    ///   <see cref="TimeProvider.System"/> local zone for the default production clock.
    /// </summary>
    [Fact]
    public void PrimeClock_WithTimeProviderSystem_LocalScheduleTimeZone_MatchesProviderLocalTimeZone ()
    {
        IPrimeClock clock = new PrimeClock(TimeProvider.System);
        clock.LocalScheduleTimeZone.Should().Be(TimeProvider.System.LocalTimeZone);
    }

    /// <summary>
    ///   Verifies that <see cref="IPrimeClock.LocalScheduleTimeZone"/> follows the
    ///   <see cref="FakeTimeProvider"/> local zone so tests can inject a synthetic <see cref="TimeZoneInfo"/>.
    /// </summary>
    [Fact]
    public void PrimeClock_WithFakeTimeProvider_LocalScheduleTimeZone_MatchesConfiguredZone ()
    {
        DateTimeOffset fixedUtc = new(2025, 3, 7, 12, 0, 0, TimeSpan.Zero);
        FakeTimeProvider fake = new(fixedUtc);
        TimeZoneInfo synthetic = TimeZoneInfo.CreateCustomTimeZone("Phase1Test+03", TimeSpan.FromHours(3), null, null);
        fake.SetLocalTimeZone(synthetic);
        IPrimeClock clock = new PrimeClock(fake);
        clock.LocalScheduleTimeZone.Should().BeSameAs(synthetic);
    }

    /// <summary>
    ///   Verifies that <see cref="PrimeTestClock"/> exposes <see cref="IPrimeClock.LocalScheduleTimeZone"/>
    ///   consistent with its local-time mapping (<see cref="TimeZoneInfo.Local"/>).
    /// </summary>
    [Fact]
    public void PrimeTestClock_LocalScheduleTimeZone_MatchesTimeZoneInfoLocal ()
    {
        IPrimeClock clock = new PrimeTestClock();
        clock.LocalScheduleTimeZone.Should().Be(TimeZoneInfo.Local);
    }
#endif
}
//################################################################################

