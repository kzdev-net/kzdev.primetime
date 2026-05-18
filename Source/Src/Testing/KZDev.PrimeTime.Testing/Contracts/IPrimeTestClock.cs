using NodaTime;

namespace KZDev.PrimeTime.Testing;

//################################################################################
/// <summary>
///   NodaTime-specific test clock members: <see cref="SetInstant"/>, <see cref="SetLocalTime"/>, and
///   <see cref="Duration"/>-based <see cref="Advance(NodaTime.Duration)"/>,
///   <see cref="RunFor(NodaTime.Duration)"/>, and <see cref="Start(NodaTime.Duration?)"/> overloads.
/// </summary>
/// <remarks>
///   Semantics match the shared BCL <see cref="IPrimeTestClock"/> surface after conversion: forward setters march,
///   backward rules apply after resolving to UTC, run-rate bounds match <see cref="IPrimeTestClock.Start(System.TimeSpan?)"/>,
///   and the automatic runner shares the same deadline-driven behavior documented there.
/// </remarks>
public partial interface IPrimeTestClock
{
    //----------------------------------------------------------------------------
    /// <summary>
    ///   Sets the current UTC instant, using forward marching when <paramref name="instant"/> is strictly after
    ///   the current virtual time.
    /// </summary>
    /// <remarks>
    ///   Equivalent to <see cref="IPrimeTestClock.SetTime(System.DateTimeOffset)"/> after converting
    ///   <paramref name="instant"/> to UTC. When the clock is not running, this is the instant returned by
    ///   <see cref="IPrimeClock.NowInstant"/> and related members until the next change.
    /// </remarks>
    /// <param name="instant">
    ///   The new current instant on the global timeline.
    /// </param>
    /// <exception cref="InvalidOperationException">
    ///   Thrown under the same backward-time rules as <see cref="IPrimeTestClock.SetTime(System.DateTimeOffset)"/>.
    /// </exception>
    void SetInstant (Instant instant);
    //----------------------------------------------------------------------------
    /// <summary>
    ///   Sets virtual time from a local wall-clock value in the clock's zone, using forward marching when the
    ///   resolved instant is strictly after the current virtual time.
    /// </summary>
    /// <remarks>
    ///   Lenient zone mapping applies for skipped or ambiguous local times (spring-forward gaps and fall-back
    ///   overlaps), consistent with production day-time scheduling. Forward moves honor the same march and
    ///   <see cref="IPrimeTestClock.ClockEvents"/> rules as <see cref="SetInstant"/>.
    /// </remarks>
    /// <param name="localDateTime">
    ///   The new current local date and time.
    /// </param>
    /// <exception cref="InvalidOperationException">
    ///   Thrown under the same backward-time rules as <see cref="IPrimeTestClock.SetTime(System.DateTimeOffset)"/>.
    /// </exception>
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
    ///   Starts the deadline-driven automatic runner at the given virtual-time-per-real-second rate.
    /// </summary>
    /// <remarks>
    ///   Forwards to <see cref="IPrimeTestClock.Start(System.TimeSpan?)"/> after converting <paramref name="rate"/>.
    ///   See that member for runner semantics, rate bounds, projection, and persist-on-read.
    /// </remarks>
    /// <param name="rate">
    ///   Virtual time that elapses per one real second, or <c>null</c> for 1:1.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    ///   Thrown when <paramref name="rate"/> is not <c>null</c> and its BCL conversion is outside 100 milliseconds
    ///   to 1 hour of virtual time per real second (inclusive).
    /// </exception>
    void Start (Duration? rate = null);
    //----------------------------------------------------------------------------
}
//################################################################################
