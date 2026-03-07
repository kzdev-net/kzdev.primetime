namespace KZDev.PrimeTime
{
    //################################################################################
    /// <summary>
    /// Controls whether timer callbacks capture and restore the calling execution context
    /// (e.g. SynchronizationContext, AsyncLocal) or run without it (Unsafe).
    /// </summary>
    public enum TimerCallbackExecutionContext
    {
        /// <summary>
        /// Capture the current execution context when the timer is registered and restore it
        /// when invoking the callback. This is the default and ensures callbacks run in the
        /// same context as the registration site (e.g. same sync context, async locals).
        /// </summary>
        Capture,

        /// <summary>
        /// Do not capture execution context. Callbacks run without restoring the calling
        /// context (Unsafe). Use when the callback does not depend on sync context or when
        /// avoiding context capture is desired for performance or isolation.
        /// </summary>
        Unsafe
    }
    //################################################################################
}
