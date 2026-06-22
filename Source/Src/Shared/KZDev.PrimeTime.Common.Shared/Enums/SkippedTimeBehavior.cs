// Copyright (c) Kevin Zehrer
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

#if SYSTEMCLOCK
namespace KZDev.SystemClock.PrimeTime;
#else
namespace KZDev.PrimeTime;
#endif

//################################################################################
/// <summary>
///   Defines how a local wall-clock time that does not exist on the local calendar day should be
///   handled—typically a spring-forward gap when daylight saving time starts.
/// </summary>
/// <remarks>
///   <para>
///     This enum applies to <strong>invalid</strong> local times only. When a local wall time is
///     <strong>ambiguous</strong> (fall-back overlap), use <see cref="DuplicateTimeBehavior"/>
///     instead; <see cref="SkippedTimeBehavior"/> is not used in that case.
///   </para>
/// </remarks>
public enum SkippedTimeBehavior
{
    //----------------------------------------------------------------------------
    /// <summary>
    ///   The timer does not fire for the nonexistent wall time; scheduling advances to the next
    ///   valid occurrence without invoking a callback for the skipped instant.
    /// </summary>
    Skip,

    /// <summary>
    ///   The timer runs as soon as possible after the skipped (nonexistent) local time.
    /// </summary>
    /// <remarks>
    ///   <para>
    ///     The timer triggers the respective callback as soon as possible after the skipped time,
    ///     unless another callback time would run at or before that instant—in that case the
    ///     callback for the skipped time is skipped.
    ///   </para>
    ///   <para>
    ///     This applies to time shifts from daylight saving time changes. If the time zone
    ///     changes, callback times are recalculated and the timer runs at the next scheduled time.
    ///   </para>
    /// </remarks>
    RunAfter,

    /// <summary>
    ///   The timer runs immediately before the skipped time.
    /// </summary>
    /// <remarks>
    ///   <para>
    ///     The timer triggers the respective callback immediately before the daylight saving time
    ///     change, as the clock is adjusted forward.
    ///   </para>
    ///   <para>
    ///     This applies to time shifts from daylight saving time changes. If the time zone
    ///     changes, callback times are recalculated and the timer runs at the next scheduled time.
    ///   </para>
    /// </remarks>
    RunBefore
    //----------------------------------------------------------------------------
}
//################################################################################
