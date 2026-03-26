using System.Diagnostics;

namespace KZDev.SystemClock.PrimeTime;

//################################################################################
/// <summary>
///   Registration for a timer created on an <see cref="IPrimeClock"/>.
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
