#if SYSTEMCLOCK
namespace KZDev.SystemClock.PrimeTime;
#else
namespace KZDev.PrimeTime;
#endif

//################################################################################
/// <summary>
///   Represents the state of a timer registration.
/// </summary>
public enum TimerState
{
    //--------------------------------------------------------------------------------
    /// <summary>
    ///   The timer is active and is not processing any callbacks.
    /// </summary>
    /// <remarks>
    ///   <para>
    ///     For interval timers, this state applies when the timer has not triggered any callbacks yet.
    ///   </para>
    ///   <para>
    ///     For time-of-day timers, this state applies when no callback is running for a scheduled
    ///     trigger but future time-of-day triggers remain.
    ///   </para>
    /// </remarks>
    Active,
    //--------------------------------------------------------------------------------
    /// <summary>
    ///   The timer has been cancelled.
    /// </summary>
    Cancelled,
    //--------------------------------------------------------------------------------
    /// <summary>
    ///   The timer has finished all callbacks. Applies only to non-repeating timers.
    /// </summary>
    /// <remarks>
    ///   <para>
    ///     For interval timers, callback processing has completed.
    ///   </para>
    ///   <para>
    ///     For time-of-day timers, all callbacks for the provided times of day have run.
    ///   </para>
    /// </remarks>
    Completed,
    //--------------------------------------------------------------------------------
    /// <summary>
    ///   The timer has been disabled or stopped.
    /// </summary>
    Disabled,
    //--------------------------------------------------------------------------------
    /// <summary>
    ///   The timer has been disposed.
    /// </summary>
    Disposed,
    //--------------------------------------------------------------------------------
    /// <summary>
    ///   The timer is processing at least one callback.
    /// </summary>
    /// <remarks>
    ///   <para>
    ///     For interval timers, this applies when the timer is non-repeating or repeating with
    ///     <see cref="IntervalTimerOptions.ResetIntervalAfterCallback"/> set to <c>true</c>.
    ///   </para>
    ///   <para>
    ///     For time-of-day timers, this applies when at least one callback is running for a
    ///     scheduled time-of-day trigger.
    ///   </para>
    /// </remarks>
    ProcessingCallback,
    //--------------------------------------------------------------------------------
    /// <summary>
    ///   The timer is in a repeat cycle where the interval resets before the callback runs.
    ///   Interval timers only.
    /// </summary>
    /// <remarks>
    ///   No callback is running in this state.
    /// </remarks>
    RepeatCycle,
    //--------------------------------------------------------------------------------
    /// <summary>
    ///   A callback is running and the repeat interval resets before the callback (interval
    ///   timers only).
    /// </summary>
    /// <remarks>
    ///   Applies to repeating interval timers with
    ///   <see cref="IntervalTimerOptions.ResetIntervalAfterCallback"/> set to <c>false</c>.
    /// </remarks>
    RepeatProcessingCallback
    //--------------------------------------------------------------------------------
}
//################################################################################
