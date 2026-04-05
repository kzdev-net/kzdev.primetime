// Copyright (c) Kevin Zehrer. All rights reserved.
// This file is part of the PrimeTime project.

#if SYSTEMCLOCK
namespace KZDev.SystemClock.PrimeTime;
#else
namespace KZDev.PrimeTime;
#endif

//################################################################################
/// <summary>
///   Authoritative policy for mapping <see cref="SkippedTimeBehavior"/> and
///   <see cref="DuplicateTimeBehavior"/> when scheduling local calendar day-time timers
///   across daylight saving transitions. Production scheduling in the BCL and NodaTime stacks
///   must follow this table; public documentation lives on <see cref="SkippedTimeBehavior"/> and
///   <see cref="DuplicateTimeBehavior"/>.
/// </summary>
/// <remarks>
///   <para>
///     <strong>Invalid local time (spring-forward gap):</strong>
///     <see cref="SkippedTimeBehavior"/> applies. <see cref="SkippedTimeBehavior.Skip"/> advances
///     to the next valid schedule without firing for the impossible wall time.
///     <see cref="SkippedTimeBehavior.RunAfter"/> and <see cref="SkippedTimeBehavior.RunBefore"/>
///     resolve to the instant after the gap or the instant before the forward shift,
///     respectively, per their remarks. <see cref="DuplicateTimeBehavior"/> does not apply.
///   </para>
///   <para>
///     <strong>Ambiguous local time (fall-back overlap):</strong>
///     <see cref="SkippedTimeBehavior"/> is not used; the wall time exists twice.
///     <see cref="DuplicateTimeBehavior.RunFirst"/> chooses the earlier moment on the timeline;
///     <see cref="DuplicateTimeBehavior.RunLast"/> chooses the later moment. The timer
///     fires once per logical day-time occurrence (no double callback for the duplicate window).
///   </para>
///   <para>
///     When <see cref="SkippedTimeBehavior.RunAfter"/> schedules the next occurrence, another
///     registered callback may legitimately run at or before that resolved instant; in that case
///     the callback for the skipped wall time is not duplicated.
///   </para>
/// </remarks>
internal static class DayTimeSchedulingPolicyTable
{
}
//################################################################################
