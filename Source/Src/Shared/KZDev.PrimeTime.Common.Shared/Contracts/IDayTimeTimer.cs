using System.Diagnostics;

namespace KZDev.PrimeTime;

//################################################################################
/// <summary>
///   Registration for a time-of-day timer.
/// </summary>
public interface IDayTimeTimer : IRegisteredTimer
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
