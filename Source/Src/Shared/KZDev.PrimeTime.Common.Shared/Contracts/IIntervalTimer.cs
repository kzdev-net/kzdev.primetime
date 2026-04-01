using System.Diagnostics;

#if SYSTEMCLOCK
namespace KZDev.SystemClock.PrimeTime;
#else
namespace KZDev.PrimeTime;
#endif

//################################################################################
/// <summary>
///   Registration for an interval timer.
/// </summary>
public interface IIntervalTimer : IClockTimer
{
    //--------------------------------------------------------------------------------
    /// <summary>
    ///   Returns whether this repeating registration was set up to restart the timer
    ///   interval after each callback completes, or restart the timer interval before
    ///   the callback is initiated (default).
    /// </summary>
    /// <remarks>
    ///   This only applies to repeating time-interval registrations, and is <c>false</c>
    ///   for non-repeating registrations.
    /// </remarks>
    bool IsResetAfterCallback { [DebuggerStepThrough] get; }
    //--------------------------------------------------------------------------------
    /// <summary>
    ///   Returns the time elapsed since the last callback for this registration.
    /// </summary>
    /// <remarks>
    ///   <para>
    ///     Value is in milliseconds. If no callbacks have been made yet, this will return -1.
    ///   </para>
    ///   <para>
    ///     If the current <see cref="IRegisteredTimer.State">State</see> is
    ///     <see cref="TimerState.ProcessingCallback"/>, this will return 0
    ///     regardless of the number of callbacks that are currently being processed and not
    ///     yet completed.
    ///   </para>
    ///   <para>
    ///     Implementations in the SystemClock stack may expose this as TimeSpan;
    ///     NodaTime stack as Duration. This common contract uses milliseconds to remain
    ///     time-type agnostic.
    ///   </para>
    /// </remarks>
    long ElapsedTime { [DebuggerStepThrough] get; }
    //--------------------------------------------------------------------------------
    /// <summary>
    ///   Returns the time remaining until the next callback for this registration.
    /// </summary>
    /// <remarks>
    ///   <para>
    ///     Value is in milliseconds. For a non-repeating timer registration that has already
    ///     signaled at least once, this will return -1.
    ///   </para>
    ///   <para>
    ///     For a repeating interval timer where
    ///     <see cref="IsResetAfterCallback">IsResetAfterCallback</see>
    ///     is <c>true</c>, and with a current <see cref="IRegisteredTimer.State">State</see> of
    ///     <see cref="TimerState.ProcessingCallback"/>, this will return the time until
    ///     the next callback after the current callback completes.
    ///   </para>
    ///   <para>
    ///     Implementations in the SystemClock stack may expose this as TimeSpan;
    ///     NodaTime stack as Duration. This common contract uses milliseconds. A value of -1
    ///     indicates no next callback or not applicable.
    ///   </para>
    /// </remarks>
    long TimeUntilNextCallback { [DebuggerStepThrough] get; }
    //--------------------------------------------------------------------------------
}
//################################################################################
