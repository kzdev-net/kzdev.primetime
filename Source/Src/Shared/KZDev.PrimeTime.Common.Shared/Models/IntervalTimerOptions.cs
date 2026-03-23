using System.Diagnostics;

namespace KZDev.PrimeTime;

//################################################################################
/// <summary>
///   Timer options for setting up interval callback timers.
/// </summary>
[DebuggerStepThrough]
public record IntervalTimerOptions : TimerOptions
{
    //--------------------------------------------------------------------------------
    /// <summary>
    ///   Sets when the repeating interval is started relative to each callback.
    /// </summary>
    /// <remarks>
    ///   <para>
    ///     If this option is <c>false</c>, the interval is reset before the callback is initiated.
    ///     Many callbacks may run concurrently depending on the interval and callback duration.
    ///   </para>
    ///   <para>
    ///     If this option is <c>true</c>, the interval is reset after the callback completes
    ///     (whether it returns normally or throws).
    ///   </para>
    ///   <para>
    ///     This applies only to repeating interval timer registrations and is ignored for
    ///     non-repeating registrations. The default is <c>false</c>.
    ///   </para>
    /// </remarks>
#if NET
    public bool ResetIntervalAfterCallback { get; init; }
#else
    public bool ResetIntervalAfterCallback { get; set; }
#endif
    //--------------------------------------------------------------------------------
}
//################################################################################
