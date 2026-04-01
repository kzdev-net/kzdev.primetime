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
    //--------------------------------------------------------------------------------
    /// <summary>
    ///   Gets the concurrent trigger processing setting for this registration.
    /// </summary>
    ConcurrentTriggerProcessing ConcurrentTriggerProcessing { [DebuggerStepThrough] get; }
    //--------------------------------------------------------------------------------
    /// <summary>
    ///   Gets the skipped time behavior setting for this registration.
    /// </summary>
    SkippedTimeBehavior SkippedTimeBehavior { [DebuggerStepThrough] get; }
    //--------------------------------------------------------------------------------
    /// <summary>
    ///   Gets the duplicate time behavior setting for this registration.
    /// </summary>
    DuplicateTimeBehavior DuplicateTimeBehavior { [DebuggerStepThrough] get; }
    //--------------------------------------------------------------------------------
}
//################################################################################
