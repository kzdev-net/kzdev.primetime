#if SYSTEMCLOCK
namespace KZDev.SystemClock.PrimeTime.Testing;
#else
namespace KZDev.PrimeTime.Testing;
#endif

//################################################################################
/// <summary>
///   Discriminator for kinds of <see cref="PrimeTestClockEvent"/> raised on
///   <see cref="IPrimeTestClock.ClockEvents"/>.
/// </summary>
public enum PrimeTestClockEventType
{
    //----------------------------------------------------------------------------
    /// <summary>
    ///   Virtual time advanced to a new instant (forward march, heartbeat, or permitted backward set).
    /// </summary>
    NewTime,
    //----------------------------------------------------------------------------
    /// <summary>
    ///   The automatic runner transitioned from stopped to running.
    /// </summary>
    ClockStarted,
    //----------------------------------------------------------------------------
    /// <summary>
    ///   The automatic runner stopped after joining the background thread and committing the final instant.
    /// </summary>
    ClockStopped
    //----------------------------------------------------------------------------
}
//################################################################################
