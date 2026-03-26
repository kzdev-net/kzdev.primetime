using System.Diagnostics;

namespace KZDev.PrimeTime;

//################################################################################
/// <summary>
///   Registration for a timer created on an <see cref="IPrimeSystemClock"/>.
/// </summary>
public interface IClockTimer : IRegisteredTimer
{
    //--------------------------------------------------------------------------------
    /// <summary>
    ///   Gets the time (UTC or local per registration options) at which this timer was registered.
    /// </summary>
    DateTimeOffset RegisteredTime { [DebuggerStepThrough] get; }
    //--------------------------------------------------------------------------------
}
//################################################################################
