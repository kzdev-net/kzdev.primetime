#if !NET

using NodaTime;

namespace KZDev.PrimeTime;

//################################################################################
/// <summary>
///   Day-time timer registration handle for the NodaTime stack when the unified
///   day-time timer interface used on modern TFMs is not compiled (.NET Standard 2.0).
/// </summary>
public partial interface IClockDayTimeTimer : IClockTimer, IDayTimeTimer
{
    //--------------------------------------------------------------------------------
    /// <summary>
    ///   Changes the target local time of day for this registration.
    /// </summary>
    /// <param name="timeOfDay">The new local time of day for the next and subsequent triggers.</param>
    /// <returns>
    ///   <c>true</c> if the change was applied; <c>false</c> if the registration was
    ///   cancelled, disposed, or otherwise invalid.
    /// </returns>
    bool Change (LocalTime timeOfDay);
    //--------------------------------------------------------------------------------
}
//################################################################################

#endif
