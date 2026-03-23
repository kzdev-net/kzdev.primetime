using System.Diagnostics;

namespace KZDev.PrimeTime;

//################################################################################
/// <summary>
///   Timer options for setting up time-of-day callback timers.
/// </summary>
[DebuggerStepThrough]
public record DayTimeTimerOptions : TimerOptions
{
    //--------------------------------------------------------------------------------
    /// <summary>
    ///   Sets whether more than one callback is allowed to run concurrently.
    /// </summary>
#if NET
    public ConcurrentTriggerProcessing ConcurrentTriggerProcessing { get; init; }
#else
    public ConcurrentTriggerProcessing ConcurrentTriggerProcessing { get; set; }
#endif
    //--------------------------------------------------------------------------------
    /// <summary>
    ///   Sets how skipped times are handled during clock transitions.
    /// </summary>
#if NET
    public SkippedTimeBehavior SkippedTimeBehavior { get; init; } = SkippedTimeBehavior.RunAfter;
#else
    public SkippedTimeBehavior SkippedTimeBehavior { get; set; } = SkippedTimeBehavior.RunAfter;
#endif
    //--------------------------------------------------------------------------------
    /// <summary>
    ///   Sets how duplicate trigger times are handled during clock transitions.
    /// </summary>
#if NET
    public DuplicateTimeBehavior DuplicateTimeBehavior { get; init; } = DuplicateTimeBehavior.RunLast;
#else
    public DuplicateTimeBehavior DuplicateTimeBehavior { get; set; } = DuplicateTimeBehavior.RunLast;
#endif
    //--------------------------------------------------------------------------------
}
//################################################################################
