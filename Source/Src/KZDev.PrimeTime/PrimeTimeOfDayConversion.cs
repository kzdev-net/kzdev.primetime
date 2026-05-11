// Copyright (c) Kevin Zehrer
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using NodaTime;

namespace KZDev.PrimeTime;

#if NET
//################################################################################
/// <summary>
///   Converts between Noda <see cref="LocalTime"/> and BCL time-of-day shapes used by PrimeTime
///   (<see cref="TimeOnly"/>, <see cref="LocalTimeOfDay"/>, <see cref="UtcTimeOfDay"/>), preserving
///   100-nanosecond tick resolution.
/// </summary>
/// <remarks>
///   <para>
///     Only supported in .NET 8+. Not supported in NetStandard 2.0.
///   </para>
/// </remarks>
public static class PrimeTimeOfDayConversion
{
    //----------------------------------------------------------------------------
    /// <summary>
    ///   Converts a BCL <see cref="TimeOnly"/> wall-clock value to Noda <see cref="LocalTime"/>.
    /// </summary>
    /// <param name="timeOnly">The time of day.</param>
    /// <returns>
    ///   The equivalent <see cref="LocalTime"/> via <see cref="LocalTime.FromTicksSinceMidnight"/>.
    /// </returns>
    public static LocalTime ToLocalTime (TimeOnly timeOnly) =>
        LocalTime.FromTicksSinceMidnight(timeOnly.Ticks);
    //----------------------------------------------------------------------------

    //----------------------------------------------------------------------------
    /// <summary>
    ///   Converts a local-schedule <see cref="LocalTimeOfDay"/> to Noda <see cref="LocalTime"/>.
    /// </summary>
    /// <param name="timeOfDay">The wrapped local time of day.</param>
    /// <returns>
    ///   The equivalent <see cref="LocalTime"/> via <see cref="LocalTime.FromTicksSinceMidnight"/>.
    /// </returns>
    public static LocalTime ToLocalTime (LocalTimeOfDay timeOfDay) =>
        LocalTime.FromTicksSinceMidnight(timeOfDay.Value.Ticks);
    //----------------------------------------------------------------------------

    //----------------------------------------------------------------------------
    /// <summary>
    ///   Converts a UTC-schedule <see cref="UtcTimeOfDay"/> to Noda <see cref="LocalTime"/> representing the same
    ///   wall-clock time of day (ticks since midnight); interpretation as UTC vs local is determined by timer registration.
    /// </summary>
    /// <param name="timeOfDay">The wrapped UTC time of day.</param>
    /// <returns>
    ///   The equivalent <see cref="LocalTime"/> via <see cref="LocalTime.FromTicksSinceMidnight"/>.
    /// </returns>
    public static LocalTime ToLocalTime (UtcTimeOfDay timeOfDay) =>
        LocalTime.FromTicksSinceMidnight(timeOfDay.Value.Ticks);
    //----------------------------------------------------------------------------

    //----------------------------------------------------------------------------
    /// <summary>
    ///   Converts Noda <see cref="LocalTime"/> to a <see cref="LocalTimeOfDay"/> for persistence or BCL-first APIs.
    /// </summary>
    /// <param name="localTime">The Noda local time of day.</param>
    /// <returns>
    ///   A <see cref="LocalTimeOfDay"/> wrapping <see cref="TimeOnly.FromTimeSpan(TimeSpan)"/> for
    ///   <see cref="TimeSpan.FromTicks"/> of <see cref="LocalTime.TickOfDay"/>.
    /// </returns>
    public static LocalTimeOfDay ToLocalTimeOfDay (LocalTime localTime) =>
        new(TimeOnly.FromTimeSpan(TimeSpan.FromTicks(localTime.TickOfDay)));
    //----------------------------------------------------------------------------

    //----------------------------------------------------------------------------
    /// <summary>
    ///   Converts Noda <see cref="LocalTime"/> to a <see cref="UtcTimeOfDay"/> for persistence or BCL-first APIs.
    /// </summary>
    /// <param name="localTime">The Noda local time of day (wall-clock ticks; use with UTC-schedule registration).</param>
    /// <returns>
    ///   A <see cref="UtcTimeOfDay"/> wrapping <see cref="TimeOnly.FromTimeSpan(TimeSpan)"/> for
    ///   <see cref="TimeSpan.FromTicks"/> of <see cref="LocalTime.TickOfDay"/>.
    /// </returns>
    public static UtcTimeOfDay ToUtcTimeOfDay (LocalTime localTime) =>
        new(TimeOnly.FromTimeSpan(TimeSpan.FromTicks(localTime.TickOfDay)));
    //----------------------------------------------------------------------------
}
//################################################################################
#endif
