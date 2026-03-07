namespace KZDev.PrimeTime
{
    //################################################################################
    /// <summary>
    /// Timer options for setting up time-of-day callback timers.
    /// </summary>
    public record DayTimeTimerOptions : TimerOptions
    {
        //--------------------------------------------------------------------------------
        /// <summary>
        /// Sets the option of allowing more than one callback to be processing concurrently.
        /// </summary>
#if NET
        public ConcurrentTriggerProcessing ConcurrentTriggerProcessing { get; init; }
#else
        public ConcurrentTriggerProcessing ConcurrentTriggerProcessing { get; set; }
#endif
        //--------------------------------------------------------------------------------
        /// <summary>
        /// Sets the option of how to handle skipped times during clock transitions.
        /// </summary>
#if NET
        public SkippedTimeBehavior SkippedTimeBehavior { get; init; } = SkippedTimeBehavior.RunAfter;
#else
        public SkippedTimeBehavior SkippedTimeBehavior { get; set; } = SkippedTimeBehavior.RunAfter;
#endif
        //--------------------------------------------------------------------------------
        /// <summary>
        /// Sets the option of how to handle duplicate times during clock transitions.
        /// </summary>
#if NET
        public DuplicateTimeBehavior DuplicateTimeBehavior { get; init; } = DuplicateTimeBehavior.RunLast;
#else
        public DuplicateTimeBehavior DuplicateTimeBehavior { get; set; } = DuplicateTimeBehavior.RunLast;
#endif
        //--------------------------------------------------------------------------------
    }
    //################################################################################
}
