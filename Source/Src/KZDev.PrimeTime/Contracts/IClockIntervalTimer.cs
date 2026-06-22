// Copyright (c) Kevin Zehrer
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
using NodaTime;

namespace KZDev.PrimeTime;

//################################################################################
public partial interface IClockIntervalTimer
{
    //----------------------------------------------------------------------------
    /// <summary>
    ///   Changes the next callback time and, for repeating interval timers, the repeat interval
    ///   (NodaTime <see cref="Duration"/> overload).
    /// </summary>
    /// <param name="nextInterval">
    ///   The duration until the next callback.
    /// </param>
    /// <param name="repeatInterval">
    ///   The interval for subsequent callbacks. Use a non-positive duration mapped from
    ///   <see cref="Timeout.InfiniteTimeSpan"/> for one-shot (no repeat).
    /// </param>
    /// <returns>
    ///   <c>true</c> if the change was applied; <c>false</c> if the registration was
    ///   cancelled, disposed, or otherwise invalid.
    /// </returns>
    /// <remarks>
    ///   For a currently non-repeating timer, passing a positive
    ///   <paramref name="repeatInterval"/> may be disallowed by the implementation.
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    ///   This registration is one-shot (not repeating) and <paramref name="repeatInterval"/> is a
    ///   positive duration, which would convert it to a repeating timer.
    /// </exception>
    bool Change (Duration nextInterval, Duration repeatInterval);
    //----------------------------------------------------------------------------
    /// <summary>
    ///   Changes the interval of this registration (NodaTime <see cref="Duration"/> overload).
    /// </summary>
    /// <param name="interval">
    ///   The duration until the next callback. For a repeating timer, this also
    ///   becomes the repeat interval.
    /// </param>
    /// <returns>
    ///   <c>true</c> if the change was applied; <c>false</c> if the registration was
    ///   cancelled, disposed, or otherwise invalid.
    /// </returns>
    /// <remarks>
    ///   Does not change whether the timer is repeating or one-shot. For a repeating
    ///   timer, <paramref name="interval"/> is used for both the next and subsequent
    ///   intervals. Use <see cref="Change(Duration, Duration)"/> to set next and repeat
    ///   intervals separately.
    /// </remarks>
    bool Change (Duration interval);
    //----------------------------------------------------------------------------
}
//################################################################################
