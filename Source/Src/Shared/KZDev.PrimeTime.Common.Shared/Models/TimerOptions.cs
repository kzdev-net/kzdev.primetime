namespace KZDev.PrimeTime;

//################################################################################
/// <summary>
/// The timer options for setting up callback timers.
/// </summary>
public abstract record TimerOptions
{
    //--------------------------------------------------------------------------------
    /// <summary>
    /// Sets the option of whether the timer registration times are represented in local time or UTC.
    /// By default, all timer registration properties are represented in UTC. For time-of-day
    /// registrations, it may be more convenient or appropriate to represent the times as local
    /// time, and this option allows for that.
    /// </summary>
    /// <remarks>
    /// For both interval and time-of-day registrations, this option will affect
    /// the registration properties such as registered time. For time-of-day registrations,
    /// the times of the callbacks are determined by the type of time-of-day
    /// parameters that are passed to the registration methods.
    /// </remarks>
#if NET
    public bool LocalTimeRepresentation { get; init; }
#else
    public bool LocalTimeRepresentation { get; set; }
#endif
    //--------------------------------------------------------------------------------
    /// <summary>
    /// Sets whether timer callbacks capture and restore the calling execution context or run
    /// without it (Unsafe). Default is <see cref="TimerCallbackExecutionContext.Capture"/>.
    /// </summary>
#if NET
    public TimerCallbackExecutionContext CallbackExecutionContext { get; init; } = TimerCallbackExecutionContext.Capture;
#else
    public TimerCallbackExecutionContext CallbackExecutionContext { get; set; } = TimerCallbackExecutionContext.Capture;
#endif
    //--------------------------------------------------------------------------------
}
//################################################################################
