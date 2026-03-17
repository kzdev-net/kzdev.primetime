namespace KZDev.PrimeTime;

//################################################################################
/// <summary>
///   A timer that has been registered with a PrimeTime clock; the handle returned to
///   monitor and control the registration.
/// </summary>
public interface IRegisteredTimer : IDisposable
{
    //--------------------------------------------------------------------------------
    /// <summary>
    ///   A unique identifier for this timer registration instance.
    /// </summary>
    int Id { get; }
    //--------------------------------------------------------------------------------
    /// <summary>
    ///   Indicates whether the timer has been cancelled.
    /// </summary>
    bool IsCancelled { get; }
    //--------------------------------------------------------------------------------
    /// <summary>
    ///   Returns whether this is a time-of-day clock registration or a time-interval
    ///   clock registration.
    /// </summary>
    /// <returns>
    ///   <c>true</c> if this is a time-of-day clock registration, <c>false</c> if this is a
    ///   time interval clock registration.
    /// </returns>
    bool IsTimeOfDay { get; }
    //--------------------------------------------------------------------------------
    /// <summary>
    ///   Returns whether this is a repeating timer registration or not.
    /// </summary>
    bool IsRepeating { get; }
    //--------------------------------------------------------------------------------
    /// <summary>
    ///   Returns whether the properties on this registration are based on local time
    ///   or UTC time. Timers by default are based on UTC time, but can be configured
    ///   to use local time instead using the <see cref="TimerOptions.LocalTimeRepresentation"/>
    ///   timer option during registration.
    /// </summary>
    bool IsLocalTimeRepresentation { get; }
    //--------------------------------------------------------------------------------
    /// <summary>
    ///   Returns whether this timer registration is currently active. This will
    ///   return <c>false</c> if the registration has been cancelled, if
    ///   all the callbacks have completed, or if the timer registration has been disposed.
    ///   Otherwise, this will return <c>true</c>, even if <see cref="State"/> is
    ///   <see cref="TimerState.Disabled"/>.
    /// </summary>
    bool IsActive { get; }
    //--------------------------------------------------------------------------------
    /// <summary>
    ///   Returns the current state of this timer registration.
    /// </summary>
    TimerState State { get; }
    //--------------------------------------------------------------------------------
    /// <summary>
    ///   Returns whether there are currently callbacks being processed for this timer registration.
    /// </summary>
    /// <remarks>
    ///   This will always return <c>false</c> if the timer registration is cancelled or disposed,
    ///   even if there are callbacks currently being processed.
    /// </remarks>
    bool CallbacksProcessing { get; }
    //--------------------------------------------------------------------------------
    /// <summary>
    ///   Gets or sets the enabled state of this timer registration. When this is set to
    ///   <c>false</c>, the <see cref="State"/> will be set to <see cref="TimerState.Disabled"/>
    ///   and no further callbacks will be made. Changing this to <c>true</c> will cause
    ///   the timer registration to be re-enabled which has the effect of resetting and
    ///   restarting the timer.
    /// </summary>
    /// <remarks>
    ///   <para>
    ///     This will return <c>false</c> if the timer is inactive (<see cref="IsActive"/>
    ///     is <c>false</c>).
    ///   </para>
    ///   <para>
    ///     Trying to set this to <c>true</c> when the timer is inactive will have no effect
    ///     unless the timer <see cref="State"/> is <see cref="TimerState.Completed"/>,
    ///     in which case the timer will be restarted.
    ///   </para>
    /// </remarks>
    bool Enabled { get; set; }
    //--------------------------------------------------------------------------------
    /// <summary>
    ///   Cancels this timer registration. When this is called, the <see cref="State"/>
    ///   will be set to <see cref="TimerState.Cancelled"/> and <see cref="IsActive"/>
    ///   will be set to <c>false</c>. No further callbacks will be made, and the timer
    ///   can not be restarted.
    /// </summary>
    void Cancel ();
    //--------------------------------------------------------------------------------
    /// <summary>
    ///   Calling this method will stop the timer registration from signalling any further
    ///   callbacks. This has the exact same effect as setting <see cref="Enabled"/> to
    ///   <c>false</c>.
    /// </summary>
    /// <returns>
    ///   <c>true</c> if the timer was stopped, <c>false</c> if the timer was already
    ///   stopped (or inactive) and no action was taken.
    /// </returns>
    bool Stop ();
    //--------------------------------------------------------------------------------
    /// <summary>
    ///   Calling this method will restart the timer if it is not already running. This
    ///   has the exact same effect as setting <see cref="Enabled"/> to <c>true</c>.
    /// </summary>
    /// <returns>
    ///   <c>true</c> if the timer was started, <c>false</c> if the timer was already
    ///   running and no action was taken, or if the timer has been cancelled or disposed.
    /// </returns>
    bool Start ();
    //--------------------------------------------------------------------------------
}
//################################################################################
