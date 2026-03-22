using System.Diagnostics;

using NodaTime;

namespace KZDev.PrimeTime;

//################################################################################
/// <summary>
///   Registration for a timer created on an <see cref="IPrimeClock"/>, supporting
///   interval timers (with <see cref="Change(Duration)"/> and
///   <see cref="Change(Duration, Duration)"/>) and time-of-day timers (with
///   <see cref="Change(LocalTime)"/>).
/// </summary>
public interface IPrimeClockTimerRegistration : IIntervalTimer
{
    //--------------------------------------------------------------------------------
    /// <summary>
    ///   Gets the instant (UTC) at which this timer was registered.
    /// </summary>
    Instant RegisteredInstant { [DebuggerStepThrough] get; }
    //--------------------------------------------------------------------------------
    /// <summary>
    ///   Changes the interval of this registration (interval timers only).
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
    ///   intervals separately. For time-of-day timers, use <see cref="Change(LocalTime)"/>.
    /// </remarks>
    bool Change (Duration interval);
    //--------------------------------------------------------------------------------
    /// <summary>
    ///   Changes the next callback time and, for repeating interval timers, the repeat interval.
    /// </summary>
    /// <param name="nextInterval">
    ///   The duration until the next callback.
    /// </param>
    /// <param name="repeatInterval">
    ///   The interval for subsequent callbacks. Use a non-positive duration (e.g.
    ///   <see cref="Duration.Zero"/> or a non-positive duration for one-shot)
    ///   for a one-shot timer (no repeat).
    /// </param>
    /// <returns>
    ///   <c>true</c> if the change was applied; <c>false</c> if the registration was
    ///   cancelled, disposed, or otherwise invalid.
    /// </returns>
    /// <remarks>
    ///   For a currently non-repeating timer, passing a positive
    ///   <paramref name="repeatInterval"/> may be disallowed by the implementation
    ///   (e.g. throw <see cref="InvalidOperationException"/>).
    /// </remarks>
    bool Change (Duration nextInterval, Duration repeatInterval);
    //--------------------------------------------------------------------------------
    /// <summary>
    ///   Changes the time of day at which this registration fires (time-of-day timers only).
    /// </summary>
    /// <param name="timeOfDay">
    ///   The new local time of day for the next and subsequent triggers.
    /// </param>
    /// <returns>
    ///   <c>true</c> if the change was applied; <c>false</c> if the registration was
    ///   cancelled, disposed, or otherwise invalid.
    /// </returns>
    /// <remarks>
    ///   Only applicable when <see cref="IIntervalTimer"/> (via <see cref="IRegisteredTimer.IsTimeOfDay"/>)
    ///   indicates a time-of-day registration. For interval timers, use
    ///   <see cref="Change(Duration)"/> or <see cref="Change(Duration, Duration)"/>.
    /// </remarks>
    bool Change (LocalTime timeOfDay);
    //--------------------------------------------------------------------------------
}
//################################################################################
