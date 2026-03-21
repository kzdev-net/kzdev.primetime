#if NET

namespace KZDev.PrimeTime;

//################################################################################
/// <summary>
///   Registration for a time-of-day timer created on an <see cref="IPrimeSystemClock"/>,
///   supporting change of the target time of day.
/// </summary>
public interface IClockDayTimeTimer : IDayTimeTimer, IClockTimer
{
    //--------------------------------------------------------------------------------
    /// <summary>
    ///   Changes the target local time of day for this registration.
    /// </summary>
    /// <param name="newTimeOfDay">The new local time of day at which to fire.</param>
    /// <returns>
    ///   <c>true</c> if this is a local time-of-day registration and the change was
    ///   applied; <c>false</c> if this is a UTC time-of-day registration, or the
    ///   registration was cancelled, disposed, or otherwise invalid.
    /// </returns>
    bool Change (LocalTimeOfDay newTimeOfDay);
    //--------------------------------------------------------------------------------
    /// <summary>
    ///   Changes the target UTC time of day for this registration.
    /// </summary>
    /// <param name="newTimeOfDay">The new UTC time of day at which to fire.</param>
    /// <returns>
    ///   <c>true</c> if this is a UTC time-of-day registration and the change was
    ///   applied; <c>false</c> if this is a local time-of-day registration, or the
    ///   registration was cancelled, disposed, or otherwise invalid.
    /// </returns>
    bool Change (UtcTimeOfDay newTimeOfDay);
    //--------------------------------------------------------------------------------
}
//################################################################################

#endif
