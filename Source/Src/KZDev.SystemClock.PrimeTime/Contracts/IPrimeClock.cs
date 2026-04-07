// Copyright (c) Kevin Zehrer. All rights reserved.
// This file is part of the PrimeTime project.

namespace KZDev.SystemClock.PrimeTime;

//################################################################################
/// <summary>
///   System Clock–specific members for <see cref="IPrimeClock"/> (BCL local calendar scheduling).
/// </summary>
public partial interface IPrimeClock
{
    #region IPrimeClock — Local schedule zone

    //----------------------------------------------------------------------------
    /// <summary>
    ///   Gets the time zone used to resolve the local calendar date and local wall-clock time for
    ///   local day-time timer registration (BCL time-of-day surface when available).
    /// </summary>
    /// <remarks>
    ///   <para>
    ///     Matches <see cref="TimeProvider.LocalTimeZone"/> when the implementation is backed by a
    ///     <see cref="TimeProvider"/>. Unit tests may substitute a synthetic
    ///     <see cref="TimeZoneInfo"/> for deterministic DST coverage.
    ///   </para>
    ///   <para>
    ///     The NodaTime-based package does not declare this member; local scheduling there uses the
    ///     NodaTime zone corresponding to the clock's current local zoned time
    ///     (<c>LocalZonedNowInstant.Zone</c>).
    ///   </para>
    /// </remarks>
    TimeZoneInfo LocalScheduleTimeZone { get; }
    //----------------------------------------------------------------------------

    #endregion IPrimeClock — Local schedule zone
}
//################################################################################
