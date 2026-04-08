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
///   supporting change of the target time of day (BCL subset using the <c>LocalTimeOfDay</c> and
///   <c>UtcTimeOfDay</c> wrappers when <c>System.TimeOnly</c> is available; see remarks).
/// </summary>
/// <remarks>
///   <para>
///     API reference generated from a modern target (for example .NET 10) may list
///     <c>Change</c> overloads that are not emitted when you target .NET Standard 2.0 or
///     .NET Framework (for example .NET Framework 4.8.1). Those overloads depend on
///     <c>System.TimeOnly</c> and are included only when the library is built with the SDK
///     <c>NET</c> conditional compilation symbol (effectively .NET 6+).
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
    ///     Not present in the compiled contract for .NET Standard 2.0 or .NET Framework; requires
    ///     <see cref="TimeOnly"/> (see remarks on <see cref="IClockDayTimeTimer"/>).
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
    ///     Not present in the compiled contract for .NET Standard 2.0 or .NET Framework; requires
    ///     <see cref="TimeOnly"/> (see remarks on <see cref="IClockDayTimeTimer"/>).
    ///   </para>
    /// </remarks>
    bool Change (UtcTimeOfDay newTimeOfDay);
#endif
}
//################################################################################
