// Copyright (c) Kevin Zehrer
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
using NodaTime;

namespace KZDev.PrimeTime.Testing;

//################################################################################
public sealed partial class PrimeTestClockStoppedEvent
{
    #region Constructors/Finalizers

    //----------------------------------------------------------------------------
    /// <summary>
    ///   Initializes a new instance of the <see cref="PrimeTestClockStoppedEvent"/> class.
    /// </summary>
    /// <param name="clockInstant">
    ///   The final committed virtual instant (UTC) after the runner stopped.
    /// </param>
    /// <param name="runRateDuration">
    ///   The virtual time advanced per real second that was active before the runner stopped, or
    ///   <see langword="null"/> if no rate was configured.
    /// </param>
    internal PrimeTestClockStoppedEvent (
        Instant clockInstant,
        Duration? runRateDuration)
        : base(PrimeTestClockEventType.ClockStopped, clockInstant, runRateDuration)
    {
    }
    //----------------------------------------------------------------------------

    #endregion Constructors/Finalizers
}
//################################################################################
