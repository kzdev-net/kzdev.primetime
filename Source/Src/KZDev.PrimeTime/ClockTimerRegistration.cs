// Copyright (c) Kevin Zehrer
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Diagnostics;

using NodaTime;

namespace KZDev.PrimeTime;

//################################################################################
/// <summary>
///   NodaTime partial for <see cref="ClockTimerRegistration"/> (instant/duration basis).
/// </summary>
internal abstract partial class ClockTimerRegistration
{
    //----------------------------------------------------------------------------
    /// <summary>
    ///   Converts a Noda <see cref="Duration"/> to a BCL timer millisecond value, clamping overflow.
    /// </summary>
    /// <param name="duration">Elapsed duration to convert.</param>
    /// <returns>
    ///   <see cref="Timeout.Infinite"/> when the duration is nonpositive or not representable as a finite timer; otherwise a clamped millisecond count.
    /// </returns>
    private static int DurationToTimerMilliseconds (Duration duration)
    {
        if (duration <= Duration.Zero)
            return Timeout.Infinite;
        try
        {
            TimeSpan timeSpan = duration.ToTimeSpan();
            long totalMillisecondsClamped = (long)Math.Min(timeSpan.TotalMilliseconds, int.MaxValue);
            if (totalMillisecondsClamped <= 0)
                return Timeout.Infinite;
            return (int)totalMillisecondsClamped;
        }
        catch (OverflowException)
        {
            return int.MaxValue;
        }
    }
    //----------------------------------------------------------------------------
    /// <summary>
    ///   Persists the clock's current instant as this registration's creation time.
    /// </summary>
    private partial void CaptureRegisteredTime () => RegisteredInstant = Clock.NowInstant;
    //----------------------------------------------------------------------------
    /// <summary>
    ///   Gets the captured creation time as a <see cref="DateTimeOffset"/>.
    /// </summary>
    /// <returns>The offset corresponding to <see cref="RegisteredInstant"/>.</returns>
    private partial DateTimeOffset GetRegisteredTimeOffset () => RegisteredInstant.ToDateTimeOffset();
    //----------------------------------------------------------------------------

    #region Interface Implementations

    #region IClockTimer Implementation

    //----------------------------------------------------------------------------
    /// <summary>
    ///   Gets the instant at which this registration was created.
    /// </summary>
    public Instant RegisteredInstant { [DebuggerStepThrough] get; [DebuggerStepThrough] private set; }
    //----------------------------------------------------------------------------

    #endregion IClockTimer Implementation

    #endregion Interface Implementations
}
//################################################################################
