// Copyright (c) Kevin Zehrer
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

#if SYSTEMCLOCK
namespace KZDev.SystemClock.PrimeTime;
#else
namespace KZDev.PrimeTime;
#endif

//################################################################################
/// <summary>
///   Indicates which of the two possible moments on the timeline to use when the same local wall
///   clock time occurs twice—typically a fall-back overlap when daylight saving time ends.
/// </summary>
/// <remarks>
///   <para>
///     For an ambiguous local time, the timer fires <strong>once</strong> per logical day-time
///     occurrence (no double callback covering the duplicate window).
///   </para>
///   <para>
///     <see cref="SkippedTimeBehavior"/> does not apply to ambiguous times; it applies only when
///     the wall time does not exist (spring-forward gap).
///   </para>
/// </remarks>
public enum DuplicateTimeBehavior
{
    //----------------------------------------------------------------------------
    /// <summary>
    ///   Uses the second of the two occurrences in chronological order (the later moment on the
    ///   timeline during the overlap).
    /// </summary>
    RunLast,

    /// <summary>
    ///   Uses the first of the two occurrences in chronological order (the earlier moment on the
    ///   timeline during the overlap).
    /// </summary>
    RunFirst
    //----------------------------------------------------------------------------
}
//################################################################################
