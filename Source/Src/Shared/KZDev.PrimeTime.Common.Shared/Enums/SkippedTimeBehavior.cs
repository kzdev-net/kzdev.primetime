namespace KZDev.PrimeTime;

//--------------------------------------------------------------------------------
/// <summary>
/// Defines how a local skipped time due to clock adjustments
/// (such as time zone changes and/or daylight savings time changes)
/// should be handled.
/// </summary>
public enum SkippedTimeBehavior
{
    /// <summary>
    /// Indicates that the timer should be skipped and no callback should be invoked
    /// for the skipped time.
    /// </summary>
    Skip,

    /// <summary>
    /// Indicates that the timer should be run immediately after the skipped time.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This will cause the timer to trigger the respective callback as soon as possible
    /// after the skipped time, but only if there isn't another callback time that would
    /// run at that time or before it; in which case, the timer callback for the skipped
    /// time will be skipped.
    /// </para>
    /// <para>
    /// This only applies to time shifts related to daylight savings time changes. If the
    /// timezone changes, the timer callback times will be recalculated and the timer will
    /// just run at the next scheduled time.
    /// </para>
    /// </remarks>
    RunAfter,

    /// <summary>
    /// Indicates that the timer should be run immediately before the skipped time.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This will cause the timer to trigger the respective callback immediately before the
    /// daylight savings time change occurs. This will cause the timer to trigger the
    /// callback right as the clock is adjusted forward.
    /// </para>
    /// <para>
    /// This only applies to time shifts related to daylight savings time changes. If the
    /// timezone changes, the timer callback times will be recalculated and the timer will
    /// just run at the next scheduled time.
    /// </para>
    /// </remarks>
    RunBefore
}
//--------------------------------------------------------------------------------
