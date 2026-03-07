namespace KZDev.PrimeTime
{
    //################################################################################
    /// <summary>
    /// Indicates how duplicate trigger times should be handled. Duplicate trigger times
    /// can happen as the result of clock adjustments (such as time zone changes and/or
    /// daylight savings time changes). This does not apply to simply clock drift adjustments
    /// unless they are very substantial.
    /// </summary>
    public enum DuplicateTimeBehavior
    {
        /// <summary>
        /// Only trigger the callback for the last duplicate time. When the time is
        /// hit initially, the callback will not be invoked.
        /// </summary>
        RunLast,

        /// <summary>
        /// Only trigger the callback for the first duplicate time. When the time is
        /// hit again, the callback will not be invoked.
        /// </summary>
        RunFirst
    }
    //################################################################################
}
