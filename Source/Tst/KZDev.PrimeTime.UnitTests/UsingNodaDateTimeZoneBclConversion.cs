// Copyright (c) Kevin Zehrer
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;

using AwesomeAssertions;

using NodaTime;
using NodaTime.TimeZones;

namespace KZDev.PrimeTime.UnitTests;

//################################################################################
/// <summary>
///   Validates <see cref="NodaDateTimeZoneBclConversion"/> behavior for BCL-backed zones.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed class UsingNodaDateTimeZoneBclConversion
{
    //----------------------------------------------------------------------------
    /// <summary>
    ///   Resolves a US Eastern zone by id, trying IANA id first then Windows id.
    /// </summary>
    /// <returns>A non-null <see cref="TimeZoneInfo"/>.</returns>
    private static TimeZoneInfo ResolveUsEasternTimeZone ()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("America/New_York");
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
        }
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies <see cref="NodaDateTimeZoneBclConversion.GetLocalScheduleTimeZoneInfo"/> returns the wrapped
    ///   <see cref="TimeZoneInfo"/> for a <see cref="BclDateTimeZone"/>.
    /// </summary>
    [Fact]
    public void GetLocalScheduleTimeZoneInfo_WhenBclZone_ReturnsOriginalTimeZoneInfo ()
    {
        TimeZoneInfo synthetic = ResolveUsEasternTimeZone();
        DateTimeZone zone = BclDateTimeZone.FromTimeZoneInfo(synthetic);
        NodaDateTimeZoneBclConversion.GetLocalScheduleTimeZoneInfo(zone).Should().BeSameAs(synthetic);
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies non-<see cref="BclDateTimeZone"/> sources fall back to <see cref="TimeZoneInfo.Local"/>.
    /// </summary>
    [Fact]
    public void GetLocalScheduleTimeZoneInfo_WhenTzdbZone_ReturnsLocalTimeZoneInfo ()
    {
        DateTimeZone zone = DateTimeZoneProviders.Tzdb["America/New_York"];
        NodaDateTimeZoneBclConversion.GetLocalScheduleTimeZoneInfo(zone).Should().Be(TimeZoneInfo.Local);
    }
    //----------------------------------------------------------------------------
}
//################################################################################
