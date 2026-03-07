namespace KZDev.PrimeTime
{
    //################################################################################
    /// <summary>
    /// Defines the options for concurrent trigger processing for time of day timers.
    /// </summary>
    public enum ConcurrentTriggerProcessing
    {
        /// <summary>
        /// Indicates that concurrent trigger processing is not allowed. If a trigger fires while
        /// a callback is already processing, the new trigger will be skipped and no
        /// callback will be invoked for that time of day.
        /// </summary>
        /// <remarks>
        /// This is the default value.
        /// </remarks>
        Skip,
        /// <summary>
        /// Indicates that concurrent trigger processing is allowed. If a trigger fires while
        /// a callback is already processing, the new trigger will be processed concurrently.
        /// </summary>
        /// <remarks>
        /// This can potentially result in multiple callbacks being processed concurrently,
        /// and it is the responsibility of the callback to ensure that this is safe to do so
        /// and that the number of callbacks being processed concurrently is acceptable.
        /// </remarks>
        RunConcurrently,
        /// <summary>
        /// Indicates that concurrent trigger processing is not allowed, but no triggers will
        /// be skipped. If a trigger fires while a callback is already processing, the new
        /// trigger will be queued and processed sequentially after the current callback
        /// completes.
        /// </summary>
        RunSequentially
    }
    //################################################################################
}
