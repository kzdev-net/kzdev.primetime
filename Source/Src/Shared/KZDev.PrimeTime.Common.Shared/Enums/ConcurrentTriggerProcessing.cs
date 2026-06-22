// Copyright (c) Kevin Zehrer
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

#if SYSTEMCLOCK
namespace KZDev.SystemClock.PrimeTime;
#else
namespace KZDev.PrimeTime;
#endif

//################################################################################
/// <summary>
///   Options for concurrent trigger processing for time-of-day timers.
/// </summary>
public enum ConcurrentTriggerProcessing
{
    //----------------------------------------------------------------------------
    /// <summary>
    ///   Concurrent trigger processing is not allowed. If a trigger fires while a callback is
    ///   running, the new trigger is skipped (no callback for that time of day).
    /// </summary>
    /// <remarks>
    ///   This is the default value.
    /// </remarks>
    Skip,
    /// <summary>
    ///   Concurrent trigger processing is allowed. If a trigger fires while a callback is running,
    ///   the new trigger is processed concurrently.
    /// </summary>
    /// <remarks>
    ///   Multiple callbacks may run concurrently; the callback implementation must ensure that is
    ///   safe and that the level of concurrency is acceptable.
    /// </remarks>
    RunConcurrently,
    /// <summary>
    ///   Concurrent trigger processing is not allowed, but triggers are not skipped. If a trigger
    ///   fires while a callback is running, the new trigger is queued and runs after the current
    ///   callback completes.
    /// </summary>
    /// <remarks>
    ///   Only one callback is queued at a time; if a trigger fires while a callback is running and 
    ///   there is already a callback queued, the new trigger is skipped (no callback for that time of day).
    /// </remarks>
    RunSequentially
    //----------------------------------------------------------------------------
}
//################################################################################
