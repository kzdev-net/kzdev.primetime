// Copyright (c) Kevin Zehrer. All rights reserved.
// This file is part of the PrimeTime project.

namespace KZDev.SystemClock.PrimeTime;

//################################################################################
/// <content>
///   <see cref="IPrimeClock.LocalScheduleTimeZone"/> for the System Clock <see cref="PrimeClock"/>.
/// </content>
internal sealed partial class PrimeClock
{
    #region IPrimeClock Implementation — Local schedule zone

    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public TimeZoneInfo LocalScheduleTimeZone => _timeProvider.LocalTimeZone;
    //----------------------------------------------------------------------------

    #endregion IPrimeClock Implementation — Local schedule zone
}
//################################################################################
