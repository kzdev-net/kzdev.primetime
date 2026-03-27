using System.Diagnostics;

namespace KZDev.PrimeTime;

//################################################################################
/// <summary>
///   Registration for a timer created from a clock's timer registration API (BCL "now"
///   subset or full NodaTime-capable clock).
/// </summary>
public partial interface IClockTimer : IRegisteredTimer
{
    //--------------------------------------------------------------------------------
    /// <summary>
    ///   Gets the time (UTC or local per registration options) at which this timer was registered.
    /// </summary>
    DateTimeOffset RegisteredTime { [DebuggerStepThrough] get; }
    //--------------------------------------------------------------------------------
}
//################################################################################
