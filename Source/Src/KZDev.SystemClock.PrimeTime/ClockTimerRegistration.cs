// Copyright (c) Kevin Zehrer
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

namespace KZDev.SystemClock.PrimeTime;

/// <summary>
///   BCL time-basis partial for <see cref="ClockTimerRegistration"/>.
/// </summary>
internal abstract partial class ClockTimerRegistration
{
    /// <summary>
    ///   Offset-based instant recorded when this registration was created.
    /// </summary>
    private DateTimeOffset _registeredTime;

    /// <summary>
    ///   Persists the clock's current instant as this registration's creation time, using the
    ///   same UTC vs local calendar-day basis as scheduling (<see cref="GetScheduleNowOffset"/>).
    /// </summary>
    private partial void CaptureRegisteredTime () => _registeredTime = GetScheduleNowOffset();

    /// <summary>
    ///   Gets the captured creation time as a <see cref="DateTimeOffset"/>.
    /// </summary>
    /// <returns>The offset used when the registration was created.</returns>
    private partial DateTimeOffset GetRegisteredTimeOffset () => _registeredTime;

    /// <summary>
    ///   Gets the clock's current time in the schedule basis (UTC vs local calendar day).
    /// </summary>
    /// <returns>
    ///   <see cref="IPrimeClock.UtcNowDateTimeOffset"/> for UTC calendar-day scheduling; otherwise <see cref="IPrimeClock.LocalNowDateTimeOffset"/>.
    /// </returns>
    internal DateTimeOffset GetScheduleNowOffset () =>
        UtcTimeOfDaySchedule ? Clock.UtcNowDateTimeOffset : Clock.LocalNowDateTimeOffset;
}
