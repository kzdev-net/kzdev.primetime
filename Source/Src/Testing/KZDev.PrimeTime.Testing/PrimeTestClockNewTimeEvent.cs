// Copyright (c) Kevin Zehrer
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
using NodaTime;

namespace KZDev.PrimeTime.Testing;

//################################################################################
public sealed partial class PrimeTestClockNewTimeEvent
{
    #region Constructors/Finalizers

    //----------------------------------------------------------------------------
    /// <summary>
    ///   Initializes a new instance of the <see cref="PrimeTestClockNewTimeEvent"/> class.
    /// </summary>
    /// <param name="clockInstant">
    ///   The virtual instant (UTC) at which this event was raised.
    /// </param>
    /// <param name="runRateDuration">
    ///   The virtual time advanced per real second while the clock is running, or
    ///   <see langword="null"/> when the clock is stopped.
    /// </param>
    internal PrimeTestClockNewTimeEvent (
        Instant clockInstant,
        Duration? runRateDuration)
        : base(PrimeTestClockEventType.NewTime, clockInstant, runRateDuration)
    {
    }
    //----------------------------------------------------------------------------

    #endregion Constructors/Finalizers
}
//################################################################################
