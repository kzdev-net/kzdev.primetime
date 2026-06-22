// Copyright (c) Kevin Zehrer
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
using NodaTime;

namespace KZDev.PrimeTime;

//################################################################################
public partial interface IClockDayTimeTimer
{
    //----------------------------------------------------------------------------
    /// <summary>
    ///   Changes the time of day at which this registration fires (NodaTime
    ///   <see cref="LocalTime"/> overload; local semantic day-time registrations).
    /// </summary>
    /// <param name="targetTimeOfDay">
    ///   The new local time of day for the next and subsequent triggers.
    /// </param>
    /// <returns>
    ///   <c>true</c> if the change was applied; <c>false</c> if the registration was
    ///   cancelled, disposed, or otherwise invalid.
    /// </returns>
    bool Change (LocalTime targetTimeOfDay);
    //----------------------------------------------------------------------------
}
//################################################################################
