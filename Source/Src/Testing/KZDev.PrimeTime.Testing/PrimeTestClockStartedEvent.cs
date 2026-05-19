using NodaTime;

namespace KZDev.PrimeTime.Testing;

//################################################################################
public sealed partial class PrimeTestClockStartedEvent
{
    #region Constructors/Finalizers

    //----------------------------------------------------------------------------
    /// <summary>
    ///   Initializes a new instance of the <see cref="PrimeTestClockStartedEvent"/> class.
    /// </summary>
    /// <param name="clockInstant">
    ///   The committed virtual instant (UTC) when the runner started.
    /// </param>
    /// <param name="runRateDuration">
    ///   The virtual time advanced per real second for the newly started runner.
    /// </param>
    internal PrimeTestClockStartedEvent (Instant clockInstant,
        Duration runRateDuration)
        : base(PrimeTestClockEventType.ClockStarted, clockInstant, runRateDuration)
    {
    }
    //----------------------------------------------------------------------------

    #endregion Constructors/Finalizers
}
//################################################################################
