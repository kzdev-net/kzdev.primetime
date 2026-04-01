using NodaTime;

namespace KZDev.PrimeTime;

//################################################################################
public partial interface IClockDayTimeTimer
{
    //--------------------------------------------------------------------------------
    /// <summary>
    ///   Changes the time of day at which this registration fires (NodaTime
    ///   <see cref="LocalTime"/> overload; local semantic day-time registrations).
    /// </summary>
    /// <param name="timeOfDay">
    ///   The new local time of day for the next and subsequent triggers.
    /// </param>
    /// <returns>
    ///   <c>true</c> if the change was applied; <c>false</c> if the registration was
    ///   cancelled, disposed, or otherwise invalid.
    /// </returns>
    bool Change (LocalTime timeOfDay);
    //--------------------------------------------------------------------------------
    /// <summary>
    ///   Interval-style <see cref="Duration"/> change is not applicable for time-of-day
    ///   registrations; implementations return <c>false</c>.
    /// </summary>
    /// <param name="interval">Ignored.</param>
    /// <returns>Always <c>false</c> for day-time registrations.</returns>
    bool Change (Duration interval);
    //--------------------------------------------------------------------------------
}
//################################################################################
