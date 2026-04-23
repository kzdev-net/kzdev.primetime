// Copyright (c) Kevin Zehrer
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using NodaTime;
using NodaTime.TimeZones;

namespace KZDev.PrimeTime;

//################################################################################
/// <summary>
///   Maps Noda <see cref="DateTimeZone"/> values to BCL <see cref="TimeZoneInfo"/> for
///   <see cref="IPrimeClock.LocalScheduleTimeZone"/> and <see cref="TimeProvider.LocalTimeZone"/> adapters.
/// </summary>
internal static class NodaDateTimeZoneBclInterop
{
    //----------------------------------------------------------------------------
    /// <summary>
    ///   Returns the wrapped <see cref="TimeZoneInfo"/> when <paramref name="zone"/> is a
    ///   <see cref="BclDateTimeZone"/>; otherwise returns <see cref="TimeZoneInfo.Local"/> so callers still
    ///   receive a concrete zone (non-BCL zones have no single BCL equivalent).
    /// </summary>
    /// <param name="zone">The zone used for local calendar-day and local zoned projections.</param>
    /// <returns>
    ///   <see cref="BclDateTimeZone.OriginalZone"/> when applicable; otherwise <see cref="TimeZoneInfo.Local"/>.
    /// </returns>
    internal static TimeZoneInfo GetLocalScheduleTimeZoneInfo (DateTimeZone zone)
    {
        if (zone is BclDateTimeZone bclZone)
            return bclZone.OriginalZone;
        return TimeZoneInfo.Local;
    }
    //----------------------------------------------------------------------------
}
//################################################################################
