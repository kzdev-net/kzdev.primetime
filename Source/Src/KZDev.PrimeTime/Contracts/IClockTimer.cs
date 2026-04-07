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
