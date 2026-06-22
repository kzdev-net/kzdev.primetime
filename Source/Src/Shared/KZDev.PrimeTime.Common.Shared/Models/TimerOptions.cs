// Copyright (c) Kevin Zehrer
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
using System.Diagnostics;

#if SYSTEMCLOCK
namespace KZDev.SystemClock.PrimeTime;
#else
namespace KZDev.PrimeTime;
//################################################################################
#endif

/// <summary>
///   The timer options for setting up callback timers.
/// </summary>
[DebuggerStepThrough]
public abstract record TimerOptions
{
    //----------------------------------------------------------------------------
    /// <summary>
    ///   Sets whether timer registration times are represented in local time or UTC.
    ///   By default, all timer registration properties are represented in UTC. For time-of-day
    ///   registrations, it may be more convenient or appropriate to represent the times as local
    ///   time, and this option allows for that.
    /// </summary>
    /// <remarks>
    ///   For both interval and time-of-day registrations, this option affects registration
    ///   properties such as registered time. For time-of-day registrations, callback times are
    ///   determined by the time-of-day parameters passed to the registration methods.
    /// </remarks>
#if NET
    public bool LocalTimeRepresentation { get; init; }
#else
    public bool LocalTimeRepresentation { get; set; }
    //----------------------------------------------------------------------------
#endif
    //----------------------------------------------------------------------------
    /// <summary>
    ///   Sets whether timer callbacks capture and restore the calling execution context or run
    ///   without it (Unsafe). Default is <see cref="TimerCallbackExecutionContext.Capture"/>.
    /// </summary>
#if NET
    public TimerCallbackExecutionContext CallbackExecutionContext { get; init; } = TimerCallbackExecutionContext.Capture;
#else
    public TimerCallbackExecutionContext CallbackExecutionContext { get; set; } = TimerCallbackExecutionContext.Capture;
    //----------------------------------------------------------------------------
#endif
}
//################################################################################
