// Copyright (c) Kevin Zehrer
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Diagnostics;

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
    public TimeZoneInfo LocalScheduleTimeZone { [DebuggerStepThrough] get => _timeProvider.LocalTimeZone; }
    //----------------------------------------------------------------------------

    #endregion IPrimeClock Implementation — Local schedule zone
}
//################################################################################
