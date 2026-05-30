// Copyright (c) Kevin Zehrer
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

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
#if SYSTEMCLOCK
public interface IPrimeTestClock : IPrimeTestTime, IPrimeClock
#else
public partial interface IPrimeTestClock : IPrimeTestTime, IPrimeClock
#endif
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
    ///   Starts a bounded automatic run that advances virtual time by <paramref name="duration"/> at a 1:1
    ///   real-time pace (one second of virtual time per real second), then stops when that virtual elapsed time
    ///   is reached.
    /// </summary>
    /// <remarks>
    ///   <para>
    ///     Returns immediately; wait for completion via <see cref="IPrimeTestTime.IsRunning"/> and/or
    ///     <see cref="PrimeTestClockEventType.ClockStopped"/>. Only starts when the clock is not already running.
    ///     Negative <paramref name="duration"/> is treated as zero. Zero duration raises
    ///     <see cref="PrimeTestClockEventType.ClockStarted"/> and <see cref="PrimeTestClockEventType.ClockStopped"/>
    ///     synchronously before returning.
    ///   </para>
    ///   <para>
    ///     For a faster or slower pace, use <see cref="RunFor(System.TimeSpan, System.TimeSpan)"/>.
    ///     For synchronous deterministic marching, use <see cref="Advance(System.TimeSpan)"/>.
    ///   </para>
    /// </remarks>
    /// <param name="duration">
    ///   Virtual elapsed time after which the bounded run stops.
    /// </param>
    /// <returns>
    ///   <c>true</c> if the bounded run started; <c>false</c> if the clock was already running.
    /// </returns>
    bool RunFor (TimeSpan duration);
    //----------------------------------------------------------------------------
    /// <summary>
    ///   Starts a bounded automatic run that advances virtual time by <paramref name="duration"/> at
    ///   <paramref name="perSecondRate"/> (virtual time per one real second), then stops when that virtual elapsed
    ///   time is reached.
    /// </summary>
    /// <remarks>
    ///   <para>
    ///     Returns immediately; wait for completion via <see cref="IPrimeTestTime.IsRunning"/> and/or
    ///     <see cref="PrimeTestClockEventType.ClockStopped"/>. Only starts when the clock is not already running.
    ///     Negative <paramref name="duration"/> is treated as zero. Zero duration raises
    ///     <see cref="PrimeTestClockEventType.ClockStarted"/> and <see cref="PrimeTestClockEventType.ClockStopped"/>
    ///     synchronously before returning.
    ///   </para>
    /// </remarks>
    /// <param name="duration">
    ///   Virtual elapsed time after which the bounded run stops.
    /// </param>
    /// <param name="perSecondRate">
    ///   Virtual time that elapses per one real second.
    /// </param>
    /// <returns>
    ///   <c>true</c> if the bounded run started; <c>false</c> if the clock was already running.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">
    ///   Thrown when <paramref name="perSecondRate"/> is outside the inclusive range allowed for
    ///   <see cref="Start(System.TimeSpan?)"/> (100 milliseconds to 1 hour of virtual time per real second).
    /// </exception>
    bool RunFor (TimeSpan duration, TimeSpan perSecondRate);
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
    /// <param name="perSecondRate">
    ///   Virtual time that elapses per one real second, or <c>null</c> for 1 second of virtual time per real second.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    ///   Thrown when <paramref name="perSecondRate"/> is not <c>null</c> and is less than 100 milliseconds or
    ///   greater than 1 hour of virtual time per real second (no clamping).
    /// </exception>
    void Start (TimeSpan? perSecondRate = null);
    //----------------------------------------------------------------------------
    /// <summary>
    ///   Stops the automatic runner and clears the monotonic run anchor.
    /// </summary>
    /// <remarks>
    ///   <para>
    ///     After a successful stop, "now" getters return the persisted virtual instant only (no linear projection).
    ///   </para>
    ///   <para>
    ///     When <c>false</c> is returned because the runner thread did not exit within the join timeout, the clock is
    ///     logically stopped but the background runner may still be active. Subsequent <see cref="Start(System.TimeSpan?)"/>
    ///     calls throw <see cref="InvalidOperationException"/> until that thread exits or this clock instance is discarded.
    ///     This typically indicates the runner loop is blocked inside a <see cref="ClockEvents"/> subscriber or
    ///     virtual-time dispatch callback.
    ///   </para>
    /// </remarks>
    /// <returns>
    ///   <c>true</c> if the clock was running and stop completed (runner joined and
    ///   <see cref="PrimeTestClockEventType.ClockStopped"/> was raised); <c>false</c> if the clock was not running, or
    ///   the runner did not join within the allowed time while stopping a running clock.
    /// </returns>
    bool Stop ();
    //----------------------------------------------------------------------------
    /// <summary>
    ///   Occurs when the clock publishes a discriminated lifecycle or virtual-time event.
    /// </summary>
    /// <remarks>
    ///   <para>
    ///     Subscribers receive <see cref="PrimeTestClockEvent"/> payloads and distinguish kinds via
    ///     <see cref="PrimeTestClockEvent.EventType"/> or derived types
    ///     (<see cref="PrimeTestClockNewTimeEvent"/>, <see cref="PrimeTestClockStartedEvent"/>,
    ///     <see cref="PrimeTestClockStoppedEvent"/>).
    ///   </para>
    ///   <para>
    ///     <b>New-time cadence.</b> During forward <see cref="Advance(System.TimeSpan)"/>, forward
    ///     <see cref="SetTime(System.DateTimeOffset)"/>, persist-on-read, or automatic runner marching,
    ///     <see cref="PrimeTestClockEventType.NewTime"/> is raised <b>once per distinct</b> virtual instant visited.
    ///     While <see cref="Start(System.TimeSpan?)"/> is active, if one full virtual minute passes without another
    ///     virtual-time-driving event, the runner raises <see cref="PrimeTestClockEventType.NewTime"/> for that
    ///     heartbeat instant and resets the one-minute window when substantive work occurs. Permitted backward
    ///     <see cref="SetTime(System.DateTimeOffset)"/> while stopped raises <see cref="PrimeTestClockEventType.NewTime"/>
    ///     once for the new instant without per-instant march raises.
    ///   </para>
    ///   <para>
    ///     <b>Lifecycle.</b> <see cref="PrimeTestClockEventType.ClockStarted"/> is raised only on a stopped-to-running
    ///     transition from <see cref="Start(System.TimeSpan?)"/> (not when already running). <see cref="PrimeTestClockEventType.ClockStopped"/>
    ///     is raised only when <see cref="Stop"/> stops a running clock, after the runner thread joins and the final
    ///     virtual instant is committed.
    ///   </para>
    ///   <para>
    ///     <b>Rate on timed events.</b> <see cref="PrimeTestClockTimedEvent.RunRateTimeSpan"/> and, in the Noda package,
    ///     <c>RunRateDuration</c> on timed event types reflect the active runner rate when
    ///     <see cref="IPrimeTestTime.IsRunning"/> is <c>true</c>; they are <see langword="null"/> when the clock is stopped
    ///     (including on <see cref="PrimeTestClockEventType.NewTime"/> while stopped).
    ///   </para>
    ///   <para>
    ///     <b>Subscriber exceptions.</b> Handler exceptions propagate to the code that raised the event. The first
    ///     throwing handler prevents later handlers from running. Events raised while the automatic runner is active
    ///     are delivered on the runner thread, so an unhandled exception can abort in-progress virtual-time work or
    ///     end the runner thread.
    ///   </para>
    /// </remarks>
    event PrimeTestClockEventHandler? ClockEvents;
    //----------------------------------------------------------------------------
}
//################################################################################
