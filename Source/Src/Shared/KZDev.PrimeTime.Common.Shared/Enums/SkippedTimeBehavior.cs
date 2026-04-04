#if SYSTEMCLOCK
namespace KZDev.SystemClock.PrimeTime;
#else
namespace KZDev.PrimeTime;
#endif

//################################################################################
/// <summary>
///   Defines how a local skipped time due to clock adjustments (such as time zone changes
///   and/or daylight saving time changes) should be handled.
/// </summary>
public enum SkippedTimeBehavior
{
    //----------------------------------------------------------------------------
    /// <summary>
    ///   The timer is skipped and no callback is invoked for the skipped time.
    /// </summary>
    Skip,

    /// <summary>
    ///   The timer runs as soon as possible after the skipped time.
    /// </summary>
    /// <remarks>
    ///   <para>
    ///     The timer triggers the respective callback as soon as possible after the skipped time,
    ///     unless another callback time would run at or before that instant—in that case the
    ///     callback for the skipped time is skipped.
    ///   </para>
    ///   <para>
    ///     This applies to time shifts from daylight saving time changes. If the time zone
    ///     changes, callback times are recalculated and the timer runs at the next scheduled time.
    ///   </para>
    /// </remarks>
    RunAfter,

    /// <summary>
    ///   The timer runs immediately before the skipped time.
    /// </summary>
    /// <remarks>
    ///   <para>
    ///     The timer triggers the respective callback immediately before the daylight saving time
    ///     change, as the clock is adjusted forward.
    ///   </para>
    ///   <para>
    ///     This applies to time shifts from daylight saving time changes. If the time zone
    ///     changes, callback times are recalculated and the timer runs at the next scheduled time.
    ///   </para>
    /// </remarks>
    RunBefore
    //----------------------------------------------------------------------------
}
//################################################################################
