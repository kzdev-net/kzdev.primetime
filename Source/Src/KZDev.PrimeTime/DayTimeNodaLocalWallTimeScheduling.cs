// Copyright (c) Kevin Zehrer. All rights reserved.
// This file is part of the PrimeTime project.

using NodaTime;
using NodaTime.TimeZones;

namespace KZDev.PrimeTime;

//################################################################################
/// <summary>
///   Resolves local calendar date + Noda <see cref="LocalTime"/> of day to an <see cref="Instant"/>
///   in a <see cref="DateTimeZone"/>, following <see cref="DayTimeSchedulingPolicyTable"/>.
/// </summary>
internal static class DayTimeNodaLocalWallTimeScheduling
{
    /// <summary>
    ///   Maximum number of successive local calendar days to scan forward in
    ///   <see cref="GetDelayUntilNextLocalDayTime"/> when searching for the next fire strictly after the current
    ///   instant.
    /// </summary>
    /// <remarks>
    ///   Kept aligned with <see cref="DayTimeBclLocalWallTimeScheduling"/> so both stacks cap work identically in
    ///   pathological cases.
    /// </remarks>
    private const int MaxDaySearchWindow = 800;

    //----------------------------------------------------------------------------
    /// <summary>
    ///   Resolves a gap mapping (<see cref="ZoneLocalMapping.Count"/> is 0) per
    ///   <see cref="SkippedTimeBehavior"/>.
    /// </summary>
    /// <param name="mapping">The zone mapping for a local time in a spring-forward gap.</param>
    /// <param name="skippedTimeBehavior">Policy for invalid local wall times.</param>
    /// <param name="fireInstant">The resolved instant when this method returns <c>true</c>.</param>
    /// <returns><c>false</c> for <see cref="SkippedTimeBehavior.Skip"/>; otherwise <c>true</c>.</returns>
    private static bool TryResolveGapMapping (ZoneLocalMapping mapping,
        SkippedTimeBehavior skippedTimeBehavior,
        out Instant fireInstant)
    {
        switch (skippedTimeBehavior)
        {
            case SkippedTimeBehavior.Skip:
                fireInstant = default;
                return false;
            case SkippedTimeBehavior.RunAfter:
                fireInstant = mapping.LateInterval.Start;
                return true;
            case SkippedTimeBehavior.RunBefore:
                fireInstant = mapping.EarlyInterval.End - Duration.Epsilon;
                return true;
            default:
                throw new ArgumentOutOfRangeException(nameof(skippedTimeBehavior));
        }
    }
    //----------------------------------------------------------------------------

    //----------------------------------------------------------------------------
    /// <summary>
    ///   Attempts to resolve the scheduled fire instant for one local calendar day and target time of day.
    /// </summary>
    /// <param name="calendarDate">The local calendar date in <paramref name="zone"/>.</param>
    /// <param name="targetTimeOfDay">The wall-clock time of day on that date.</param>
    /// <param name="zone">The time zone that defines local calendar days and DST rules.</param>
    /// <param name="skippedTimeBehavior">Policy when <paramref name="targetTimeOfDay"/> is invalid (spring gap).</param>
    /// <param name="duplicateTimeBehavior">Policy when the wall time is ambiguous (fall-back overlap).</param>
    /// <param name="fireInstant">The resolved instant when this method returns <c>true</c>.</param>
    /// <returns>
    ///   <c>false</c> when <paramref name="skippedTimeBehavior"/> is <see cref="SkippedTimeBehavior.Skip"/> and the
    ///   target is invalid; otherwise <c>true</c>.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="zone"/> is <c>null</c>.</exception>
    internal static bool TryGetLocalDayTimeFireInstantForDate (LocalDate calendarDate,
        LocalTime targetTimeOfDay, DateTimeZone zone, SkippedTimeBehavior skippedTimeBehavior,
        DuplicateTimeBehavior duplicateTimeBehavior, out Instant fireInstant)
    {
        if (zone is null)
        {
            throw new ArgumentNullException(nameof(zone));
        }

        LocalDateTime localWall = calendarDate.At(targetTimeOfDay);
        ZoneLocalMapping mapping = zone.MapLocal(localWall);
        switch (mapping.Count)
        {
            case 1:
                fireInstant = mapping.First().ToInstant();
                return true;
            case 2:
                fireInstant = duplicateTimeBehavior switch
                {
                    DuplicateTimeBehavior.RunFirst => mapping.First().ToInstant(),
                    DuplicateTimeBehavior.RunLast => mapping.Last().ToInstant(),
                    _ => throw new ArgumentOutOfRangeException(nameof(duplicateTimeBehavior))
                };
                return true;
            case 0:
                return TryResolveGapMapping(mapping, skippedTimeBehavior, out fireInstant);
            default:
                throw new InvalidOperationException($"Unexpected ZoneLocalMapping.Count: {mapping.Count}.");
        }
    }
    //----------------------------------------------------------------------------

    //----------------------------------------------------------------------------
    /// <summary>
    ///   Computes the duration from <paramref name="scheduleNow"/> until the next local day-time occurrence.
    /// </summary>
    /// <param name="scheduleNow">The clock&apos;s current instant.</param>
    /// <param name="zone">The schedule zone (e.g. <see cref="IPrimeClock.LocalZonedNow"/><c>.Zone</c>).</param>
    /// <param name="targetTimeOfDay">The recurring local time of day.</param>
    /// <param name="skippedTimeBehavior">Policy for invalid local wall times.</param>
    /// <param name="duplicateTimeBehavior">Policy for ambiguous local wall times.</param>
    /// <returns>Nonnegative duration until the next fire.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="zone"/> is <c>null</c>.</exception>
    /// <exception cref="InvalidOperationException">
    ///   No qualifying fire instant exists within <see cref="MaxDaySearchWindow"/> successive local calendar days.
    /// </exception>
    internal static Duration GetDelayUntilNextLocalDayTime (Instant scheduleNow,
        DateTimeZone zone, LocalTime targetTimeOfDay, SkippedTimeBehavior skippedTimeBehavior,
        DuplicateTimeBehavior duplicateTimeBehavior)
    {
        if (zone is null)
        {
            throw new ArgumentNullException(nameof(zone));
        }

        LocalDate startDate = scheduleNow.InZone(zone).Date;
        for (int dayOffset = 0; dayOffset < MaxDaySearchWindow; dayOffset++)
        {
            LocalDate candidateDate = startDate.PlusDays(dayOffset);
            if (!TryGetLocalDayTimeFireInstantForDate(candidateDate, targetTimeOfDay, zone,
                    skippedTimeBehavior, duplicateTimeBehavior, out Instant fireInstant))
            {
                continue;
            }

            if (fireInstant > scheduleNow)
            {
                return fireInstant - scheduleNow;
            }
        }

        throw new InvalidOperationException(
            $"Unable to find a valid local day-time fire instant within {MaxDaySearchWindow} calendar days " +
            $"for time zone '{zone.Id}' and target time of day '{targetTimeOfDay}'.");
    }
    //----------------------------------------------------------------------------
}
//################################################################################
