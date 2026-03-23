namespace KZDev.PrimeTime;

//################################################################################
/// <summary>
///   Indicates how duplicate trigger times should be handled. Duplicates can result from clock
///   adjustments (such as time zone changes and/or daylight saving time changes). This does not
///   apply to ordinary clock drift unless the drift is very substantial.
/// </summary>
public enum DuplicateTimeBehavior
{
    /// <summary>
    ///   Only the last duplicate time invokes the callback; the earlier duplicate does not.
    /// </summary>
    RunLast,

    /// <summary>
    ///   Only the first duplicate time invokes the callback; later duplicates do not.
    /// </summary>
    RunFirst
}
//################################################################################
