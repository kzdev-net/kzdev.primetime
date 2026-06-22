// Copyright (c) Kevin Zehrer
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
using NodaTime;

namespace KZDev.PrimeTime;

//################################################################################
public partial interface IClockTimer
{
    //----------------------------------------------------------------------------
    /// <summary>
    ///   Gets the instant (UTC) at which this timer was registered (NodaTime stack).
    /// </summary>
    Instant RegisteredInstant { get; }
    //----------------------------------------------------------------------------
}
//################################################################################
