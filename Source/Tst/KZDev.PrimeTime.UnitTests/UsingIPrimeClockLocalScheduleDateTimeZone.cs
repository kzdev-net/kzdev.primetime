// Copyright (c) Kevin Zehrer
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;

using AwesomeAssertions;

using KZDev.PrimeTime.Testing;

using NodaTime;
using NodaTime.TimeZones;

namespace KZDev.PrimeTime.UnitTests;

//################################################################################
/// <summary>
///   Validates <see cref="IPrimeClock.LocalScheduleDateTimeZone"/> against
///   <see cref="IPrimeClock.LocalScheduleTimeZone"/> mapping rules for <see cref="PrimeClock"/>.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed class UsingIPrimeClockLocalScheduleDateTimeZone
{
    /// <summary>
    ///   Verifies <see cref="NodaDateTimeZoneBclConversion.GetLocalScheduleTimeZoneInfo"/> applied to
    ///   <see cref="IPrimeClock.LocalScheduleDateTimeZone"/> matches <see cref="IPrimeClock.LocalScheduleTimeZone"/>
    ///   for production <see cref="PrimeClock"/>.
    /// </summary>
    [Fact]
    public void PrimeClock_LocalScheduleZone_MatchesInteropOfDateTimeZone ()
    {
        IPrimeClock clock = new PrimeClock();
        TimeZoneInfo fromZone = NodaDateTimeZoneBclConversion.GetLocalScheduleTimeZoneInfo(
            clock.LocalScheduleDateTimeZone);
        fromZone.Should().Be(clock.LocalScheduleTimeZone);
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies <see cref="PrimeTestClock"/> exposes a <see cref="TimeZoneInfo"/> consistent with its Noda zone
    ///   when that zone is BCL-backed.
    /// </summary>
    [Fact]
    public void PrimeTestClock_BclBackedZone_LocalScheduleZone_MatchesInteropOfDateTimeZone ()
    {
        TimeZoneInfo synthetic = TimeZoneInfo.CreateCustomTimeZone(
            "KZDevPrimeTestSchedule", TimeSpan.FromHours(-5), "KZDevPrimeTestSchedule", "KZDevPrimeTestSchedule");
        DateTimeZone nodaZone = BclDateTimeZone.FromTimeZoneInfo(synthetic);
        IPrimeClock clock = new PrimeTestClock(Instant.FromUtc(2025, 1, 1, 0, 0, 0), nodaZone);
        TimeZoneInfo fromZone = NodaDateTimeZoneBclConversion.GetLocalScheduleTimeZoneInfo(
            clock.LocalScheduleDateTimeZone);
        fromZone.Should().Be(clock.LocalScheduleTimeZone);
        fromZone.Should().BeSameAs(synthetic);
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies Tzdb-backed <see cref="PrimeTestClock"/> still aligns interop with
    ///   <see cref="NodaDateTimeZoneBclConversion.GetLocalScheduleTimeZoneInfo"/> (fallback to
    ///   <see cref="TimeZoneInfo.Local"/>).
    /// </summary>
    [Fact]
    public void PrimeTestClock_TzdbZone_LocalScheduleZone_MatchesInteropOfDateTimeZone ()
    {
        DateTimeZone tzdbZone = DateTimeZoneProviders.Tzdb["America/New_York"];
        IPrimeClock clock = new PrimeTestClock(Instant.FromUtc(2025, 1, 1, 0, 0, 0), tzdbZone);
        TimeZoneInfo fromZone = NodaDateTimeZoneBclConversion.GetLocalScheduleTimeZoneInfo(
            clock.LocalScheduleDateTimeZone);
        fromZone.Should().Be(clock.LocalScheduleTimeZone);
    }
    //----------------------------------------------------------------------------
}
//################################################################################
