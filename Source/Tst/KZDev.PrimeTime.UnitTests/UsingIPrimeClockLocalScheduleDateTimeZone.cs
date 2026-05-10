// Copyright (c) Kevin Zehrer
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;

using AwesomeAssertions;

using NodaTime;

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
}
//################################################################################
