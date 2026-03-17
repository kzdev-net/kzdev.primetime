using System.Diagnostics;

namespace KZDev.PrimeTime;

//################################################################################
/// <summary>
/// Timer options for setting up interval callback timers.
/// </summary>
[DebuggerStepThrough]
public record IntervalTimerOptions : TimerOptions
{
    //--------------------------------------------------------------------------------
    /// <summary>
    /// Sets the option of when the repeating interval should be started.
    /// </summary>
    /// <remarks>
    /// <para>
    /// If this option is <c>false</c>, then the interval will be reset before the callback
    /// is initiated. Note, this means that there could be many active callbacks running
    /// at the same time depending on the interval and the time it takes to complete the
    /// callback call.
    /// </para>
    /// <para>
    /// If this option is <c>true</c>, then the interval will be reset after the callback
    /// has completed (whether returned or exception thrown).
    /// </para>
    /// <para>
    /// This only applies to repeating interval timer registrations, and is ignored
    /// for non-repeating registrations.
    /// </para>
    /// <para>
    /// The default value is <c>false</c>.
    /// </para>
    /// </remarks>
#if NET
    public bool ResetIntervalAfterCallback { get; init; }
#else
    public bool ResetIntervalAfterCallback { get; set; }
#endif
    //--------------------------------------------------------------------------------
}
//################################################################################
