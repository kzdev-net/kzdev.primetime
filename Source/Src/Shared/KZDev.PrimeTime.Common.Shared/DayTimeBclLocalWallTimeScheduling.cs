// Copyright (c) Kevin Zehrer. All rights reserved.
// This file is part of the PrimeTime project.

#if NET

#if SYSTEMCLOCK
using KZDev.SystemClock.PrimeTime.Observability;

namespace KZDev.SystemClock.PrimeTime;
#else
using KZDev.PrimeTime.Observability;

namespace KZDev.PrimeTime;
#endif

//################################################################################
/// <summary>
///   Resolves local calendar date + wall-clock time of day to an absolute <see cref="DateTimeOffset"/>
///   using a <see cref="TimeZoneInfo"/>, following <see cref="DayTimeSchedulingPolicyTable"/>.
/// </summary>
internal static class DayTimeBclLocalWallTimeScheduling
{
    /// <summary>
    ///   Maximum number of successive local calendar days to scan forward in
    ///   <see cref="GetDelayUntilNextLocalDayTime"/> when searching for the next fire strictly after the current
    ///   instant.
    /// </summary>
    /// <remarks>
    ///   800 exceeds two common years (730 days) with headroom for leap years, repeated spring-forward skips for the
    ///   same wall time, and days where the resolved instant is still not after the current schedule instant. Cap work in pathological
    ///   cases; when no match is found within this window, <see cref="GetDelayUntilNextLocalDayTime"/> throws
    ///   <see cref="InvalidOperationException"/> so silent mis-scheduling does not occur.
    /// </remarks>
    private const int MaxDaySearchWindow = 800;

    /// <summary>
    ///   Wall-clock minutes in a standard 24-hour calendar day (24 × 60).
    /// </summary>
    private const int MinutesPerDay = 1440;

    //----------------------------------------------------------------------------
    /// <summary>
    ///   Picks the earlier or later UTC instant for an ambiguous local wall time.
    /// </summary>
    /// <param name="wallUnspecified">The ambiguous local date and time (kind unspecified).</param>
    /// <param name="zone">The zone in which <paramref name="wallUnspecified"/> is interpreted.</param>
    /// <param name="duplicateTimeBehavior">RunFirst (earlier UTC) vs RunLast (later UTC).</param>
    /// <returns>The selected <see cref="DateTimeOffset"/>.</returns>
    private static DateTimeOffset ResolveAmbiguousWallTime (DateTime wallUnspecified,
        TimeZoneInfo zone,
        DuplicateTimeBehavior duplicateTimeBehavior)
    {
        TimeSpan[] offsets = zone.GetAmbiguousTimeOffsets(wallUnspecified);
        DateTimeOffset first = new(wallUnspecified, offsets[0]);
        DateTimeOffset second = new(wallUnspecified, offsets[1]);
        if (duplicateTimeBehavior == DuplicateTimeBehavior.RunFirst)
        {
            return first.UtcDateTime <= second.UtcDateTime ? first : second;
        }
        return first.UtcDateTime <= second.UtcDateTime ? second : first;
    }
    //----------------------------------------------------------------------------
    /// <summary>
    ///   Resolves an invalid local wall time on a calendar date per <see cref="SkippedTimeBehavior"/>.
    /// </summary>
    /// <param name="calendarDate">The local calendar date.</param>
    /// <param name="zone">The zone.</param>
    /// <param name="skippedTimeBehavior">Skip, RunAfter, or RunBefore.</param>
    /// <param name="fireInstant">The resolved instant when returning <c>true</c>.</param>
    /// <returns><c>false</c> for <see cref="SkippedTimeBehavior.Skip"/>; otherwise <c>true</c>.</returns>
    private static bool TryResolveInvalidWallTimeOnDate (DateOnly calendarDate,
        TimeZoneInfo zone, SkippedTimeBehavior skippedTimeBehavior, out DateTimeOffset fireInstant)
    {
        switch (skippedTimeBehavior)
        {
            case SkippedTimeBehavior.Skip:
                fireInstant = default;
                return false;
            case SkippedTimeBehavior.RunAfter:
                fireInstant = GetInvalidRunAfterInstant(calendarDate, zone);
                return true;
            case SkippedTimeBehavior.RunBefore:
                fireInstant = GetInvalidRunBeforeInstant(calendarDate, zone);
                return true;
            default:
                throw new ArgumentOutOfRangeException(nameof(skippedTimeBehavior));
        }
    }
    //----------------------------------------------------------------------------
    /// <summary>
    ///   Finds the first minute offset from local midnight on <paramref name="calendarDate"/> whose wall time is
    ///   invalid in <paramref name="zone"/> (start of a spring-forward gap).
    /// </summary>
    /// <param name="calendarDate">The local calendar date to inspect.</param>
    /// <param name="zone">The zone.</param>
    /// <param name="missingInvalidWindowMessageSuffix">
    ///   Suffix for the exception when the date has no invalid minute (e.g. <c>RunAfter resolution</c>).
    /// </param>
    /// <returns>The minute offset from midnight (0 through 1439) of the first invalid wall time.</returns>
    /// <exception cref="InvalidOperationException">
    ///   The local day has no invalid wall times in <paramref name="zone"/>.
    /// </exception>
    private static int GetFirstInvalidMinute (DateOnly calendarDate, TimeZoneInfo zone,
        string missingInvalidWindowMessageSuffix)
    {
        DateTime midnight = calendarDate.ToDateTime(TimeOnly.MinValue);
        for (int minute = 0; minute < MinutesPerDay; minute++)
        {
            DateTime wall = midnight.AddMinutes(minute);
            if (zone.IsInvalidTime(wall))
            {
                return minute;
            }
        }

        PrimeTimeEventSource.Log.DayTimeSchedulingResolutionFault(zone.Id,
            PrimeTimeEventSource.FaultCode_ExpectedInvalidWindowMissing,
            $"{calendarDate:O} {missingInvalidWindowMessageSuffix}");
        throw new InvalidOperationException(
            $"Expected an invalid local time window on the calendar date for {missingInvalidWindowMessageSuffix}.");
    }
    //----------------------------------------------------------------------------
    /// <summary>
    ///   Finds the first valid local wall time after a spring-forward gap on <paramref name="calendarDate"/>,
    ///   then converts it to UTC. Uses wall-clock inspection because <see cref="TimeZoneInfo.ConvertTimeFromUtc"/>
    ///   never yields a local time for which <see cref="TimeZoneInfo.IsInvalidTime(System.DateTime)"/> is true.
    /// </summary>
    /// <param name="calendarDate">The local calendar date that contains the gap.</param>
    /// <param name="zone">The zone.</param>
    /// <returns>The resolved fire instant (UTC-offset form).</returns>
    private static DateTimeOffset GetInvalidRunAfterInstant (DateOnly calendarDate, TimeZoneInfo zone)
    {
        DateTime midnight = calendarDate.ToDateTime(TimeOnly.MinValue);
        int firstInvalidMinute = GetFirstInvalidMinute(calendarDate, zone, "RunAfter resolution");
        DateTime probe = midnight.AddMinutes(firstInvalidMinute);
        DateTime dayEnd = midnight.AddDays(1);
        while (probe < dayEnd && zone.IsInvalidTime(probe))
        {
            probe = probe.AddMinutes(1);
        }

        if (probe >= dayEnd || zone.IsInvalidTime(probe))
        {
            PrimeTimeEventSource.Log.DayTimeSchedulingResolutionFault(zone.Id,
                PrimeTimeEventSource.FaultCode_RunAfterResolutionFailed,
                calendarDate.ToString("O"));
            throw new InvalidOperationException("Could not resolve RunAfter instant after a spring-forward gap.");
        }

        TimeSpan offset = zone.GetUtcOffset(probe);
        return new DateTimeOffset(probe, offset);
    }
    //----------------------------------------------------------------------------
    /// <summary>
    ///   Finds the last valid local wall instant before the spring-forward gap on <paramref name="calendarDate"/>.
    /// </summary>
    /// <param name="calendarDate">The local calendar date that contains the gap.</param>
    /// <param name="zone">The zone.</param>
    /// <returns>The resolved fire instant (UTC-offset form).</returns>
    private static DateTimeOffset GetInvalidRunBeforeInstant (DateOnly calendarDate, TimeZoneInfo zone)
    {
        DateTime midnight = calendarDate.ToDateTime(TimeOnly.MinValue);
        int firstInvalidMinute = GetFirstInvalidMinute(calendarDate, zone, "RunBefore resolution");
        DateTime probe = midnight.AddMinutes(firstInvalidMinute).AddTicks(-1);
        while (probe >= midnight && zone.IsInvalidTime(probe))
        {
            probe = probe.AddTicks(-1);
        }

        if (probe < midnight || zone.IsInvalidTime(probe))
        {
            PrimeTimeEventSource.Log.DayTimeSchedulingResolutionFault(zone.Id,
                PrimeTimeEventSource.FaultCode_RunBeforeResolutionFailed,
                calendarDate.ToString("O"));
            throw new InvalidOperationException("Could not resolve RunBefore instant before a spring-forward gap.");
        }

        TimeSpan offset = zone.GetUtcOffset(probe);
        return new DateTimeOffset(probe, offset);
    }
    //----------------------------------------------------------------------------
    /// <summary>
    ///   Attempts to resolve the scheduled fire instant for one local calendar day and target time of day.
    /// </summary>
    /// <param name="calendarDate">The local calendar date in <paramref name="zone"/>.</param>
    /// <param name="targetTimeOfDay">The wall-clock time of day on that date.</param>
    /// <param name="zone">The time zone that defines local calendar days and DST rules.</param>
    /// <param name="skippedTimeBehavior">Policy when <paramref name="targetTimeOfDay"/> is invalid (spring gap).</param>
    /// <param name="duplicateTimeBehavior">Policy when the wall time is ambiguous (fall-back overlap).</param>
    /// <param name="fireInstant">The resolved instant in UTC, when this method returns <c>true</c>.</param>
    /// <returns>
    ///   <c>false</c> when <paramref name="skippedTimeBehavior"/> is <see cref="SkippedTimeBehavior.Skip"/> and the
    ///   target is invalid; otherwise <c>true</c>.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="zone"/> is <c>null</c>.</exception>
    internal static bool TryGetLocalDayTimeFireInstantForDate (DateOnly calendarDate,
        TimeOnly targetTimeOfDay, TimeZoneInfo zone, SkippedTimeBehavior skippedTimeBehavior,
        DuplicateTimeBehavior duplicateTimeBehavior, out DateTimeOffset fireInstant)
    {
        ArgumentNullException.ThrowIfNull(zone);
        DateTime wallUnspecified = calendarDate.ToDateTime(targetTimeOfDay);
        if (zone.IsAmbiguousTime(wallUnspecified))
        {
            fireInstant = ResolveAmbiguousWallTime(wallUnspecified, zone, duplicateTimeBehavior);
            return true;
        }

        if (zone.IsInvalidTime(wallUnspecified))
        {
            return TryResolveInvalidWallTimeOnDate(calendarDate, zone, skippedTimeBehavior, out fireInstant);
        }

        TimeSpan offset = zone.GetUtcOffset(wallUnspecified);
        fireInstant = new DateTimeOffset(wallUnspecified, offset);
        return true;
    }
    //----------------------------------------------------------------------------
    /// <summary>
    ///   Computes the delay from <paramref name="scheduleNow"/> until the next local day-time occurrence.
    /// </summary>
    /// <param name="scheduleNow">
    ///   The clock&apos;s current time as a <see cref="DateTimeOffset"/> (e.g. <see cref="IPrimeClock.LocalNowDateTimeOffset"/>
    ///   for local day-time schedules). The absolute instant is used for comparisons; the offset is not required to be
    ///   UTC (+00:00).
    /// </param>
    /// <param name="zone">The schedule zone.</param>
    /// <param name="targetTimeOfDay">The recurring local time of day.</param>
    /// <param name="skippedTimeBehavior">Policy for invalid local wall times.</param>
    /// <param name="duplicateTimeBehavior">Policy for ambiguous local wall times.</param>
    /// <returns>Nonnegative delay until the next fire.</returns>
    /// <exception cref="InvalidOperationException">
    ///   No qualifying fire instant exists within <see cref="MaxDaySearchWindow"/> successive local calendar days
    ///   (unexpected for normal zones and options).
    /// </exception>
    public static TimeSpan GetDelayUntilNextLocalDayTime (DateTimeOffset scheduleNow,
        TimeZoneInfo zone, TimeOnly targetTimeOfDay, SkippedTimeBehavior skippedTimeBehavior,
        DuplicateTimeBehavior duplicateTimeBehavior)
    {
        ArgumentNullException.ThrowIfNull(zone);
        DateTime localNow = TimeZoneInfo.ConvertTimeFromUtc(scheduleNow.UtcDateTime, zone);
        DateOnly startDate = DateOnly.FromDateTime(localNow);
        for (int dayOffset = 0; dayOffset < MaxDaySearchWindow; dayOffset++)
        {
            DateOnly candidateDate = startDate.AddDays(dayOffset);
            if (!TryGetLocalDayTimeFireInstantForDate(candidateDate, targetTimeOfDay, zone,
                    skippedTimeBehavior, duplicateTimeBehavior, out DateTimeOffset fireInstant))
            {
                continue;
            }

            if (fireInstant > scheduleNow)
            {
                return fireInstant - scheduleNow;
            }
        }

        PrimeTimeEventSource.Log.DayTimeSchedulingFireInstantNotFound(zone.Id, targetTimeOfDay.ToString("O"),
            MaxDaySearchWindow);
        throw new InvalidOperationException($"Unable to find a valid local day-time fire instant within {MaxDaySearchWindow} calendar days " +
            $"for time zone '{zone.Id}' and target time of day '{targetTimeOfDay}'.");
    }
    //----------------------------------------------------------------------------
}
//################################################################################

#endif
