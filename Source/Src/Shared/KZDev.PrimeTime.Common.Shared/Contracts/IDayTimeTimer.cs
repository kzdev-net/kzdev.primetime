using System.Diagnostics;

#if SYSTEMCLOCK
namespace KZDev.SystemClock.PrimeTime;
#else
namespace KZDev.PrimeTime;
#endif

//################################################################################
/// <summary>
///   Registration for a time-of-day timer.
/// </summary>
public interface IDayTimeTimer : IClockTimer
{
    //----------------------------------------------------------------------------
    /// <summary>
    ///   Gets the concurrent trigger processing setting for this registration.
    /// </summary>
    ConcurrentTriggerProcessing ConcurrentTriggerProcessing { [DebuggerStepThrough] get; }
    //----------------------------------------------------------------------------
    /// <summary>
    ///   Gets the skipped time behavior setting for this registration.
    /// </summary>
    SkippedTimeBehavior SkippedTimeBehavior { [DebuggerStepThrough] get; }
    //----------------------------------------------------------------------------
    /// <summary>
    ///   Gets the duplicate time behavior setting for this registration.
    /// </summary>
    DuplicateTimeBehavior DuplicateTimeBehavior { [DebuggerStepThrough] get; }
    //----------------------------------------------------------------------------
    /// <summary>
    ///   Returns the time elapsed since the last callback for this registration.
    /// </summary>
    /// <remarks>
    ///   <para>
    ///     Value is in milliseconds. If no callbacks have been made yet, this will return -1.
    ///   </para>
    ///   <para>
    ///     If the current <see cref="IClockTimer.State">State</see> is
    ///     <see cref="TimerState.ProcessingCallback"/>, this will return 0
    ///     regardless of the number of callbacks that are currently being processed and not
    ///     yet completed.
    ///   </para>
    ///   <para>
    ///     Elapsed time is calculated from differences between UTC instants. For local
    ///     time-of-day registrations, the "now" reference is the local-offset representation
    ///     of the same virtual UTC instant used by the clock; for UTC time-of-day registrations,
    ///     it uses the virtual UTC instant directly.
    ///   </para>
    /// </remarks>
    long ElapsedTime { [DebuggerStepThrough] get; }
    //----------------------------------------------------------------------------
    /// <summary>
    ///   Returns the time remaining until the next scheduled callback for this registration.
    /// </summary>
    /// <remarks>
    ///   <para>
    ///     Value is in milliseconds. Returns -1 when there is no next callback (for example,
    ///     the registration is cancelled or disposed and will not fire again).
    ///   </para>
    ///   <para>
    ///     Remaining time uses UTC instant arithmetic: the next scheduled instant is stored
    ///     in UTC form and compared to the virtual UTC now. Local time-of-day registrations
    ///     still derive that scheduled instant from the virtual local calendar day; UTC
    ///     registrations from the virtual UTC day.
    ///   </para>
    /// </remarks>
    long TimeUntilNextCallback { [DebuggerStepThrough] get; }
    //----------------------------------------------------------------------------
}
//################################################################################
