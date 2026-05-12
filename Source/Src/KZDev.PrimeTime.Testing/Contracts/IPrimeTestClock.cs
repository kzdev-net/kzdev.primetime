using NodaTime;

namespace KZDev.PrimeTime.Testing;

//################################################################################
/// <summary>
///   NodaTime-specific test clock members: instant and local time setters, and
///   duration-based advance, run, and start overloads.
/// </summary>
public partial interface IPrimeTestClock
{
    //----------------------------------------------------------------------------
    /// <summary>
    ///   Sets the current instant (UTC) of the clock to the specified value. When the
    ///   clock is not running, this is the instant returned by <see cref="IPrimeClock.NowInstant"/>
    ///   and related members.
    /// </summary>
    /// <param name="instant">
    ///   The new current instant on the global timeline.
    /// </param>
    void SetInstant (Instant instant);
    //----------------------------------------------------------------------------
    /// <summary>
    ///   Sets the current time of the clock to the specified local date and time,
    ///   interpreted in the clock's default (local) time zone.
    /// </summary>
    /// <param name="localDateTime">
    ///   The new current local date and time.
    /// </param>
    void SetLocalTime (LocalDateTime localDateTime);
    //----------------------------------------------------------------------------
    /// <summary>
    ///   Advances the clock's virtual time by the specified duration. Pending delays
    ///   (Sleep, DelayAsync) that are due by the new time complete, time-based
    ///   cancellation tokens that expire by the new time are cancelled, and any
    ///   interval or day-time timers that are due are invoked.
    /// </summary>
    /// <param name="duration">
    ///   The amount of virtual time to add to the current instant.
    /// </param>
    void Advance (Duration duration);
    //----------------------------------------------------------------------------
    /// <summary>
    ///   Advances the clock's virtual time by the specified duration, processing all
    ///   due delays, time cancellations, and timer callbacks. Equivalent to
    ///   <see cref="Advance(NodaTime.Duration)"/> for a single step of the given duration.
    /// </summary>
    /// <param name="duration">
    ///   The amount of virtual time to advance.
    /// </param>
    void RunFor (Duration duration);
    //----------------------------------------------------------------------------
    /// <summary>
    ///   Starts automatic advancement of virtual time at the given rate (e.g. 1 second
    ///   of real time = <paramref name="rate"/> of virtual time). When the clock is
    ///   running, <see cref="IPrimeTestTime.IsRunning"/> is <c>true</c>.
    /// </summary>
    /// <param name="rate">
    ///   The amount of virtual time that elapses per real second, or <c>null</c> to
    ///   use 1 second of virtual time per real second.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    ///   Thrown when <paramref name="rate"/> is not <c>null</c> and its BCL conversion is less than 100 milliseconds
    ///   or greater than 1 hour of virtual time per real second (same bounds as
    ///   <see cref="IPrimeTestClock.Start(System.TimeSpan?)"/>).
    /// </exception>
    void Start (Duration? rate = null);
    //----------------------------------------------------------------------------
}
//################################################################################
