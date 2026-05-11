// Copyright (c) Kevin Zehrer
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

#if SYSTEMCLOCK
namespace KZDev.SystemClock.PrimeTime;
#else
namespace KZDev.PrimeTime;
#endif

//################################################################################
/// <summary>
///   Registration for a time-of-day timer created from a clock's timer registration API;
///   supporting change of the target time of day (BCL subset using the <c>LocalTimeOfDay</c> and
///   <c>UtcTimeOfDay</c> wrappers when <c>System.TimeOnly</c> is available; see remarks).
/// </summary>
/// <remarks>
///   <para>
///     Only supported in .NET 8+. Not supported in NetStandard 2.0.
///   </para>
/// </remarks>
public partial interface IClockDayTimeTimer : IDayTimeTimer
{
#if NET
    /// <summary>
    ///   Changes the target local time of day for this registration.
    /// </summary>
    /// <param name="newTimeOfDay">The new local time of day at which to fire.</param>
    /// <returns>
    ///   <c>true</c> if this is a local time-of-day registration and the change was
    ///   applied; <c>false</c> if this is a UTC time-of-day registration, or the
    ///   registration was cancelled, disposed, or otherwise invalid.
    /// </returns>
    /// <remarks>
    ///   <para>
    ///     Only supported in .NET 8+. Not supported in NetStandard 2.0.
    ///   </para>
    /// </remarks>
    bool Change (LocalTimeOfDay newTimeOfDay);
    /// <summary>
    ///   Changes the target UTC time of day for this registration.
    /// </summary>
    /// <param name="newTimeOfDay">The new UTC time of day at which to fire.</param>
    /// <returns>
    ///   <c>true</c> if this is a UTC time-of-day registration and the change was
    ///   applied; <c>false</c> if this is a local time-of-day registration, or the
    ///   registration was cancelled, disposed, or otherwise invalid.
    /// </returns>
    /// <remarks>
    ///   <para>
    ///     Only supported in .NET 8+. Not supported in NetStandard 2.0.
    ///   </para>
    /// </remarks>
    bool Change (UtcTimeOfDay newTimeOfDay);
#endif
}
//################################################################################
