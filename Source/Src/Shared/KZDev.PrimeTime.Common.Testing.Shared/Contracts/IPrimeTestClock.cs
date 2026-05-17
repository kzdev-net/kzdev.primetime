#if SYSTEMCLOCK
namespace KZDev.SystemClock.PrimeTime.Testing;
#else
namespace KZDev.PrimeTime.Testing;
#endif

//################################################################################
/// <summary>
///   Extends <see cref="IPrimeTestTime"/> and <see cref="IPrimeClock"/> with deterministic virtual time for tests:
///   set the current instant, advance or jump forward with event marching, and optionally run a
///   deadline-driven background runner at a validated rate. Delays, time-based cancellation, interval timers,
///   and day-time timers are evaluated against the same virtual timeline.
/// </summary>
/// <remarks>
///   <para>
///     <b>Forward marching.</b> <see cref="Advance(System.TimeSpan)"/> and strictly forward
///     <see cref="SetTime(System.DateTimeOffset)"/> move virtual time to each earliest due instant in order,
///     dispatch delays, expiries, and timers at that instant (callbacks observe that instant in
///     <see cref="IPrimeClock.UtcNowDateTimeOffset"/>), then advance to the next due instant until the target
///     is reached. <see cref="ClockEvents"/> fires once per distinct virtual instant visited during the march.
///   </para>
///   <para>
///     <b>Automatic runner.</b> <see cref="Start(System.TimeSpan?)"/> starts a background loop that waits for the
///     next virtual deadline (including a virtual one-minute <see cref="ClockEvents"/> heartbeat when idle),
///     maps virtual delay to real time using the run rate, and marches using <b>measured</b> real elapsed time on a
///     shared <see cref="System.Diagnostics.Stopwatch"/> anchor (not only the intended sleep). Intended real waits
///     shorter than 15 ms are handled by bursting virtual steps without sleeping. Registering new delays, timers, or
///     time expiries, cancelling work, calling <see cref="Stop"/>, or persist-on-read from a "now" getter can wake
///     the runner early so sooner deadlines are not missed.
///   </para>
///   <para>
///     <b>Observability while running.</b> "Now" surfaces on <see cref="PrimeTestClock"/> linearly project virtual
///     time from the last committed instant and the anchor stopwatch. Reading "now" while the runner is active
///     also <b>persist-on-read</b>: due work up to the projected instant is marched, the committed instant is
///     stored, and the anchor restarts (frequent reads can be costly).
///   </para>
///   <para>
///     <b>Backward moves.</b> While <see cref="IPrimeTestTime.IsRunning"/> is <c>true</c>, any backward
///     <see cref="SetTime(System.DateTimeOffset)"/> throws <see cref="InvalidOperationException"/>. While stopped,
///     backward moves throw if any interval timer registration is active; permitted backward jumps assign the
///     instant without forward marching and raise <see cref="ClockEvents"/> once. Day-time timers recompute
///     <c>NextDueUtc</c> so at most one callback occurs per discrete time-of-day occurrence.
///   </para>
///   <para>
///     <b>Run rate.</b> Explicit rates must be between 100 milliseconds and 1 hour of virtual time per real second
///     (inclusive); <c>null</c> means 1:1. Values outside that range throw <see cref="ArgumentOutOfRangeException"/>
///     at <see cref="Start(System.TimeSpan?)"/> with no clamping.
///   </para>
/// </remarks>
public partial interface IPrimeTestClock : IPrimeTestTime, IPrimeClock
{
    //----------------------------------------------------------------------------
    /// <summary>
    ///   Sets the current UTC time of the clock to the specified value. When the clock
    ///   is not running, this is the time returned by UtcNowDateTimeOffset and related members.
    /// </summary>
    /// <remarks>
    ///   <para>
    ///     When <paramref name="utcTime"/> is strictly after the current virtual instant, virtual time advances
    ///     forward to that instant using the same march as <see cref="Advance(System.TimeSpan)"/> (fire delays,
    ///     expiries, and timers at each crossed instant, and <see cref="ClockEvents"/> once per distinct instant).
    ///     Forward <see cref="SetTime(System.DateTimeOffset)"/> honors the same local day-time DST policies as
    ///     production scheduling where applicable.
    ///   </para>
    ///   <para>
    ///     When <paramref name="utcTime"/> equals the current virtual instant, no forward march runs; a single
    ///     <see cref="ClockEvents"/> may still be raised. When <paramref name="utcTime"/> is strictly before the
    ///     current virtual instant, the instant is assigned without forward marching and without replaying skipped
    ///     delay or timer work; <see cref="ClockEvents"/> is raised once. Permitted backward moves recompute active
    ///     day-time <c>NextDueUtc</c> values.
    ///   </para>
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    ///   Thrown when <paramref name="utcTime"/> is strictly before the current virtual instant and either the clock
    ///   is running or any interval timer registration is active.
    /// </exception>
    /// <param name="utcTime">
    ///   The new current UTC time.
    /// </param>
    void SetTime (DateTimeOffset utcTime);
    //----------------------------------------------------------------------------
    /// <summary>
    ///   Advances virtual time forward by marching to each earliest due instant up to the new horizon, then
    ///   dispatching delays, time expiries, and timers at each step.
    /// </summary>
    /// <remarks>
    ///   <para>
    ///     Negative <paramref name="duration"/> is treated as zero (no backward move). Each callback runs while
    ///     <see cref="IPrimeClock.UtcNowDateTimeOffset"/> equals that event's firing instant. <see cref="ClockEvents"/>
    ///     is raised once per distinct virtual instant visited during the march (including a raise when
    ///     <paramref name="duration"/> is zero).
    ///   </para>
    /// </remarks>
    /// <param name="duration">
    ///   The amount of virtual time to add to the current time.
    /// </param>
    void Advance (TimeSpan duration);
    //----------------------------------------------------------------------------
    /// <summary>
    ///   Advances the clock's virtual time by the specified duration, processing all
    ///   due delays, time cancellations, and timer callbacks. Equivalent to
    ///   <see cref="Advance(System.TimeSpan)"/> for a single step of the given duration.
    /// </summary>
    /// <param name="duration">
    ///   The amount of virtual time to advance.
    /// </param>
    void RunFor (TimeSpan duration);
    //----------------------------------------------------------------------------
    /// <summary>
    ///   Starts a background runner that advances virtual time toward the next delay, time-expiry, timer due
    ///   instant, or virtual-minute <see cref="ClockEvents"/> heartbeat, scaled by the run rate.
    /// </summary>
    /// <remarks>
    ///   <para>
    ///     When already running, this method has no effect. While running, <see cref="IPrimeTestTime.IsRunning"/> is
    ///     <c>true</c>, "now" getters project from a committed instant plus a monotonic anchor, and
    ///     <see cref="Stop"/> stops projection and joins the runner thread.
    ///   </para>
    ///   <para>
    ///     The runner does not use a fixed one-second poll. It computes intended real sleep from the next virtual
    ///     deadline, skips real sleeps shorter than 15 ms by processing virtual steps in-process, wakes on scheduling
    ///     changes and persist-on-read, and after a real wait applies virtual elapsed from <b>measured</b> anchor
    ///     elapsed time (oversleep catch-up).
    ///   </para>
    /// </remarks>
    /// <param name="rate">
    ///   Virtual time that elapses per one real second, or <c>null</c> for 1 second of virtual time per real second.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    ///   Thrown when <paramref name="rate"/> is not <c>null</c> and is less than 100 milliseconds or greater than
    ///   1 hour of virtual time per real second (no clamping).
    /// </exception>
    void Start (TimeSpan? rate = null);
    //----------------------------------------------------------------------------
    /// <summary>
    ///   Stops the automatic runner and clears the monotonic run anchor.
    /// </summary>
    /// <remarks>
    ///   After <see cref="Stop"/>, "now" getters return the persisted virtual instant only (no linear projection).
    /// </remarks>
    /// <returns>
    ///   <c>true</c> if the clock was running and is now stopped; <c>false</c> if the clock was not running.
    /// </returns>
    bool Stop ();
    //----------------------------------------------------------------------------
    /// <summary>
    ///   Occurs when the clock's virtual instant changes after a time adjustment or march step.
    /// </summary>
    /// <remarks>
    ///   <para>
    ///     During forward <see cref="Advance(System.TimeSpan)"/>, forward <see cref="SetTime(System.DateTimeOffset)"/>,
    ///     persist-on-read, or automatic runner marching, this event is raised <b>once per distinct</b> virtual
    ///     instant visited. While <see cref="Start(System.TimeSpan?)"/> is active, if one full virtual minute passes
    ///     without another virtual-time-driving event, the runner raises this event for that heartbeat instant and
    ///     resets the one-minute window when substantive work occurs.
    ///   </para>
    ///   <para>
    ///     Permitted backward <see cref="SetTime(System.DateTimeOffset)"/> while stopped raises this event once for
    ///     the new instant without per-instant march raises.
    ///   </para>
    /// </remarks>
    event EventHandler<ClockTimeChangedEventArgs>? ClockEvents;
    //----------------------------------------------------------------------------
}
//################################################################################
