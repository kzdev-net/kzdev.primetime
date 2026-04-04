#if SYSTEMCLOCK
namespace KZDev.SystemClock.PrimeTime;
#else
namespace KZDev.PrimeTime;
#endif

//################################################################################
/// <summary>
///   Controls whether timer callbacks capture and restore the calling execution context
///   (for example <see cref="System.Threading.SynchronizationContext"/> and
///   <see cref="System.Threading.AsyncLocal{T}"/>) or run without it (unsafe).
/// </summary>
public enum TimerCallbackExecutionContext
{
    //----------------------------------------------------------------------------
    /// <summary>
    ///   Capture the current execution context when the timer is registered and restore it when
    ///   invoking the callback. This is the default so callbacks run in the same context as the
    ///   registration site (same synchronization context, async locals, and so on).
    /// </summary>
    Capture,

    /// <summary>
    ///   Do not capture execution context. Callbacks run without restoring the registration
    ///   context (unsafe). Use when the callback does not depend on synchronization context or when
    ///   avoiding context capture is desired for performance or isolation.
    /// </summary>
    Unsafe
    //----------------------------------------------------------------------------
}
//################################################################################
