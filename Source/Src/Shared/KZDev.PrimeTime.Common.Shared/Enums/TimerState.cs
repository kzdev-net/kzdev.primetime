namespace KZDev.PrimeTime;

//################################################################################
/// <summary>
/// Represents the state of a timer registration.
/// </summary>
public enum TimerState
{
    //--------------------------------------------------------------------------------
    /// <summary>
    /// The timer is active and is not processing any callbacks.
    /// </summary>
    /// <remarks>
    /// <para>
    /// For interval timers, this state applies when the timer has not triggered any
    /// callbacks yet.
    /// </para>
    /// <para>
    /// For time of day timers, this state applies when the timer is not currently
    /// processing any callbacks for a provided time of day trigger, but there are
    /// still future time of day triggers that will be processed.
    /// </para>
    /// </remarks>
    Active,
    //--------------------------------------------------------------------------------
    /// <summary>
    /// The timer has been cancelled.
    /// </summary>
    Cancelled,
    //--------------------------------------------------------------------------------
    /// <summary>
    /// The timer has completed with all callbacks. This only applies to non-repeating
    /// timers.
    /// </summary>
    /// <remarks>
    /// <para>
    /// For interval timers, this means that the timer has completed its callback processing.
    /// </para>
    /// <para>
    /// For time of day timers, this means that the timer has completed all the callbacks
    /// for the provided list of times of day.
    /// </para>
    /// </remarks>
    Completed,
    //--------------------------------------------------------------------------------
    /// <summary>
    /// The timer has been disabled or stopped.
    /// </summary>
    Disabled,
    //--------------------------------------------------------------------------------
    /// <summary>
    /// The timer has been disposed.
    /// </summary>
    Disposed,
    //--------------------------------------------------------------------------------
    /// <summary>
    /// The timer is currently processing at least one callback.
    /// </summary>
    /// <remarks>
    /// <para>
    /// For interval timers, this applies only to timers that are either non-repeating or
    /// repeating with the <see cref="IntervalTimerOptions.ResetIntervalAfterCallback"/> option
    /// set to <c>true</c>.
    /// </para>
    /// <para>
    /// For time of day timers, this applies only to timers that are currently processing
    /// at least one callback for a provided time of day trigger.
    /// </para>
    /// </remarks>
    ProcessingCallback,
    //--------------------------------------------------------------------------------
    /// <summary>
    /// The timer is currently in a repeat cycle where the repeat interval resets before
    /// the callback is invoked. This only applies to interval timers.
    /// </summary>
    /// <remarks>
    /// In this state, there are no active callbacks running.
    /// </remarks>
    RepeatCycle,
    //--------------------------------------------------------------------------------
    /// <summary>
    /// The timer is currently processing a callback and also in a repeat cycle where
    /// the repeat interval resets before the callback is invoked. This only applies to
    /// interval timers.
    /// </summary>
    /// <remarks>
    /// This only applies to interval timers that are repeating with the
    /// <see cref="IntervalTimerOptions.ResetIntervalAfterCallback"/> option set to <c>false</c>.
    /// </remarks>
    RepeatProcessingCallback
    //--------------------------------------------------------------------------------
}
//################################################################################
