using System.Diagnostics;

#if SYSTEMCLOCK
namespace KZDev.SystemClock.PrimeTime;
#else
namespace KZDev.PrimeTime;
//################################################################################
#endif

/// <summary>
///   Timer options for setting up interval callback timers.
/// </summary>
[DebuggerStepThrough]
public sealed record IntervalTimerOptions : TimerOptions
{
    //----------------------------------------------------------------------------
    /// <summary>
    ///   For a repeating registration, when <c>false</c> (the default), the next interval is not armed
    ///   until the current callback completes. When set to <c>true</c>, the next interval is armed before
    ///   the user callback runs, allowing the next tick to fire while the current callback may still be
    ///   running.
    /// </summary>
    /// <remarks>
    ///   <para>
    ///     When this option is <c>false</c> (the default), the next tick is not armed until the current
    ///     callback finishes, so timer-driven callbacks for the same registration never overlap and a slow
    ///     callback delays every following tick. For a synchronous callback, that is when the delegate has
    ///     returned; for an asynchronous callback, when the returned task has completed (including awaited
    ///     work). Only then does the repeat interval count down toward the next fire.
    ///   </para>
    ///   <para>
    ///     When this option is <c>true</c>, the underlying timer is armed for the next repeat interval
    ///     before the user callback runs. If a callback runs longer than the repeat interval, another tick
    ///     can fire and start another callback while the previous one is still executing.
    ///   </para>
    ///   <para>
    ///     When this option is <c>false</c>, a repeating registration reports
    ///     <see cref="TimerState.ProcessingCallback"/> while a callback is executing. When
    ///     <c>true</c>, it reports <see cref="TimerState.RepeatProcessingCallback"/> during the callback
    ///     instead.
    ///   </para>
    ///   <para>
    ///     This applies only to repeating interval timer registrations and is ignored for
    ///     non-repeating registrations. The default is <c>false</c>.
    ///   </para>
    /// </remarks>
#if NET
    public bool ResetIntervalBeforeCallback { get; init; }
#else
    public bool ResetIntervalBeforeCallback { get; set; }
    //----------------------------------------------------------------------------
#endif
}
//################################################################################
