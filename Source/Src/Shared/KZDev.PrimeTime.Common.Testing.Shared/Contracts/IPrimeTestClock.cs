#if SYSTEMCLOCK
namespace KZDev.SystemClock.PrimeTime;
#else
namespace KZDev.PrimeTime;
#endif

//################################################################################
/// <summary>
///   Extends <see cref="IPrimeTestTime"/> and <see cref="IPrimeClock"/> with
///   test-controllable time: set current time, advance by a duration, run for a duration,
///   and start/stop automatic advancement with an optional rate. All "now" values, delays,
///   time-based cancellation, and timers are driven by this virtual time so tests are deterministic.
/// </summary>
public partial interface IPrimeTestClock : IPrimeTestTime, IPrimeClock
{
    //--------------------------------------------------------------------------------
    /// <summary>
    ///   Occurs when the clock's current time has changed (e.g. after
    ///   <see cref="SetTime"/>, <see cref="Advance"/>, <see cref="RunFor"/>, or
    ///   automatic advancement from <see cref="Start"/>).
    /// </summary>
    event EventHandler<ClockTimeChangedEventArgs>? ClockEvents;
    //--------------------------------------------------------------------------------
    /// <summary>
    ///   Sets the current UTC time of the clock to the specified value. When the clock
    ///   is not running, this is the time returned by UtcNowOffset and related members.
    /// </summary>
    /// <param name="utcTime">
    ///   The new current UTC time.
    /// </param>
    void SetTime (DateTimeOffset utcTime);
    //--------------------------------------------------------------------------------
    /// <summary>
    ///   Advances the clock's virtual time by the specified duration. Pending delays
    ///   (Sleep, DelayAsync) that are due by the new time complete, time-based
    ///   cancellation tokens that expire by the new time are cancelled, and any
    ///   interval or day-time timers that are due are invoked.
    /// </summary>
    /// <param name="duration">
    ///   The amount of virtual time to add to the current time.
    /// </param>
    void Advance (TimeSpan duration);
    //--------------------------------------------------------------------------------
    /// <summary>
    ///   Advances the clock's virtual time by the specified duration, processing all
    ///   due delays, time cancellations, and timer callbacks. Equivalent to
    ///   <see cref="Advance"/> for a single step of the given duration.
    /// </summary>
    /// <param name="duration">
    ///   The amount of virtual time to advance.
    /// </param>
    void RunFor (TimeSpan duration);
    //--------------------------------------------------------------------------------
    /// <summary>
    ///   Starts automatic advancement of virtual time at the given rate (e.g. 1 second
    ///   of real time = <paramref name="rate"/> of virtual time). When the clock is
    ///   running, <see cref="IPrimeTestTime.IsRunning"/> is <c>true</c>.
    /// </summary>
    /// <param name="rate">
    ///   The amount of virtual time that elapses per real second, or <c>null</c> to
    ///   use 1 second of virtual time per real second.
    /// </param>
    void Start (TimeSpan? rate = null);
    //--------------------------------------------------------------------------------
    /// <summary>
    ///   Stops automatic advancement of virtual time.
    /// </summary>
    /// <returns>
    ///   <c>true</c> if the clock was running and is now stopped; <c>false</c> if
    ///   the clock was not running.
    /// </returns>
    bool Stop ();
    //--------------------------------------------------------------------------------
}
//################################################################################
