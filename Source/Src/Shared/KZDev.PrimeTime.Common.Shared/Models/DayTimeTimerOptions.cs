using System.Diagnostics;

#if SYSTEMCLOCK
namespace KZDev.SystemClock.PrimeTime;
#else
namespace KZDev.PrimeTime;
//################################################################################
#endif

/// <summary>
///   Timer options for setting up time-of-day callback timers.
/// </summary>
/// <remarks>
///   <para>
///     For local calendar day-time registrations, <see cref="SkippedTimeBehavior"/> and
///     <see cref="DuplicateTimeBehavior"/> are applied when mapping a target wall time to the
///     next instant near daylight saving transitions (invalid or ambiguous local times).
///     UTC time-of-day registrations do not use those behaviors.
///   </para>
/// </remarks>
[DebuggerStepThrough]
public sealed record DayTimeTimerOptions : TimerOptions
{
    //----------------------------------------------------------------------------
    /// <summary>
    ///   Sets whether more than one callback is allowed to run concurrently.
    /// </summary>
#if NET
    public ConcurrentTriggerProcessing ConcurrentTriggerProcessing { get; init; }
#else
    public ConcurrentTriggerProcessing ConcurrentTriggerProcessing { get; set; }
    //----------------------------------------------------------------------------
#endif
    //----------------------------------------------------------------------------
    /// <summary>
    ///   Sets how skipped times are handled during clock transitions.
    /// </summary>
#if NET
    public SkippedTimeBehavior SkippedTimeBehavior { get; init; } = SkippedTimeBehavior.RunAfter;
#else
    public SkippedTimeBehavior SkippedTimeBehavior { get; set; } = SkippedTimeBehavior.RunAfter;
    //----------------------------------------------------------------------------
#endif
    //----------------------------------------------------------------------------
    /// <summary>
    ///   Sets how duplicate trigger times are handled during clock transitions.
    /// </summary>
#if NET
    public DuplicateTimeBehavior DuplicateTimeBehavior { get; init; } = DuplicateTimeBehavior.RunLast;
#else
    public DuplicateTimeBehavior DuplicateTimeBehavior { get; set; } = DuplicateTimeBehavior.RunLast;
    //----------------------------------------------------------------------------
#endif
}
//################################################################################
