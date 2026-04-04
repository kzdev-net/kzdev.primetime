// Copyright (c) Kevin Zehrer. All rights reserved.
// This file is part of the PrimeTime project.

#if SYSTEMCLOCK
namespace KZDev.SystemClock.PrimeTime;
#else
namespace KZDev.PrimeTime;
#endif

//################################################################################
/// <summary>
///   Registration for a time-of-day timer created from a clock's timer registration API;
///   supporting change of the target time of day (BCL subset using
///   <see cref="LocalTimeOfDay"/> and <see cref="UtcTimeOfDay"/>).
/// </summary>
public partial interface IClockDayTimeTimer : IDayTimeTimer
{
#if NET

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

#endif
}
//################################################################################
