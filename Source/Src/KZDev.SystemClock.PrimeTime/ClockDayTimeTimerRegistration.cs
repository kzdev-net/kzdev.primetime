#if NET

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace KZDev.SystemClock.PrimeTime;

//################################################################################
/// <summary>
///   BCL time-basis partial for <see cref="ClockDayTimeTimerRegistration"/>.
/// </summary>
internal sealed partial class ClockDayTimeTimerRegistration
{
    //----------------------------------------------------------------------------
    /// <summary>
    ///   Delay applied between scheduling attempts when concurrent callbacks are disallowed and a tick is still running.
    /// </summary>
    private static readonly TimeSpan RunSequentiallyRetryDelay = TimeSpan.FromMilliseconds(30);

    /// <summary>
    ///   Indicates whether the configured time of day is interpreted in UTC or local zone per day.
    /// </summary>
    private readonly bool _utcTimeOfDaySchedule;

    /// <summary>
    ///   Wall-clock time of day used to compute the next fire.
    /// </summary>
    private TimeOnly _targetTimeOfDay;

    /// <summary>
    ///   Offset-based instant recorded when this registration was created.
    /// </summary>
    private DateTimeOffset _registeredTime;

    /// <summary>
    ///   Scheduled start of the next callback, if one is pending.
    /// </summary>
    private DateTimeOffset? _nextCallbackScheduledOffset;

    /// <summary>
    ///   Start of the most recent callback, if any.
    /// </summary>
    private DateTimeOffset? _lastCallbackStartedOffset;
    //----------------------------------------------------------------------------

    #region Constructors/Finalizers

    //----------------------------------------------------------------------------
    /// <summary>
    ///   Initializes a new instance for the specified schedule basis and time of day.
    /// </summary>
    /// <param name="clock">Clock used for scheduling and reading the current time.</param>
    /// <param name="utcTimeOfDaySchedule">
    ///   <c>true</c> for UTC calendar-day scheduling; <c>false</c> for local zone days.
    /// </param>
    /// <param name="targetTimeOfDay">Wall-clock time of day for recurring fires.</param>
    /// <param name="callbackKind">Shape of the user callback.</param>
    /// <param name="callback">User callback delegate.</param>
    /// <param name="callbackState">Optional state forwarded to context callbacks.</param>
    /// <param name="options">Optional timer behavior options.</param>
    /// <param name="cancellationToken">Token that cancels scheduling and callbacks.</param>
#if NET
    [SetsRequiredMembers]
#endif
    private ClockDayTimeTimerRegistration (IPrimeClock clock,
        bool utcTimeOfDaySchedule,
        TimeOnly targetTimeOfDay,
        IntervalTimerCallbackKind callbackKind,
        Delegate callback,
        object? callbackState,
        DayTimeTimerOptions? options,
        CancellationToken cancellationToken)
    {
        _utcTimeOfDaySchedule = utcTimeOfDaySchedule;
        _targetTimeOfDay = targetTimeOfDay;
        FinishConstruction(clock, callbackKind, callback, callbackState, options, cancellationToken);
    }
    //----------------------------------------------------------------------------

    //----------------------------------------------------------------------------
    /// <summary>
    ///   Initializes a new instance for a local-time day-time timer.
    /// </summary>
    /// <param name="clock">Clock used for scheduling and reading the current time.</param>
    /// <param name="localTimeOfDay">Local time of day for recurring fires.</param>
    /// <param name="callbackKind">Shape of the user callback.</param>
    /// <param name="callback">User callback delegate.</param>
    /// <param name="callbackState">Optional state forwarded to context callbacks.</param>
    /// <param name="options">Optional timer behavior options.</param>
    /// <param name="cancellationToken">Token that cancels scheduling and callbacks.</param>
#if NET
    [SetsRequiredMembers]
#endif
    internal ClockDayTimeTimerRegistration (IPrimeClock clock,
        LocalTimeOfDay localTimeOfDay,
        IntervalTimerCallbackKind callbackKind,
        Delegate callback,
        object? callbackState,
        DayTimeTimerOptions? options,
        CancellationToken cancellationToken)
        : this(clock, false, localTimeOfDay.Value, callbackKind, callback, callbackState, options, cancellationToken)
    {
    }
    //----------------------------------------------------------------------------

    //----------------------------------------------------------------------------
    /// <summary>
    ///   Initializes a new instance for a UTC day-time timer.
    /// </summary>
    /// <param name="clock">Clock used for scheduling and reading the current time.</param>
    /// <param name="utcTimeOfDay">UTC time of day for recurring fires.</param>
    /// <param name="callbackKind">Shape of the user callback.</param>
    /// <param name="callback">User callback delegate.</param>
    /// <param name="callbackState">Optional state forwarded to context callbacks.</param>
    /// <param name="options">Optional timer behavior options.</param>
    /// <param name="cancellationToken">Token that cancels scheduling and callbacks.</param>
#if NET
    [SetsRequiredMembers]
#endif
    internal ClockDayTimeTimerRegistration (IPrimeClock clock,
        UtcTimeOfDay utcTimeOfDay,
        IntervalTimerCallbackKind callbackKind,
        Delegate callback,
        object? callbackState,
        DayTimeTimerOptions? options,
        CancellationToken cancellationToken)
        : this(clock, true, utcTimeOfDay.Value, callbackKind, callback, callbackState, options, cancellationToken)
    {
    }
    //----------------------------------------------------------------------------

    #endregion Constructors/Finalizers

    //----------------------------------------------------------------------------
    /// <summary>
    ///   Gets the clock's current time in the schedule basis (UTC vs local calendar day).
    /// </summary>
    /// <returns>
    ///   <see cref="IPrimeClock.UtcNowDateTimeOffset"/> for UTC calendar-day scheduling; otherwise <see cref="IPrimeClock.LocalNowDateTimeOffset"/>.
    /// </returns>
    private DateTimeOffset GetScheduleNowOffset () =>
        _utcTimeOfDaySchedule ? _clock.UtcNowDateTimeOffset : _clock.LocalNowDateTimeOffset;
    //----------------------------------------------------------------------------

    //----------------------------------------------------------------------------
    /// <summary>
    ///   Persists the clock's current instant as this registration's creation time.
    /// </summary>
    private partial void CaptureRegisteredTimeForDayTimer () =>
        _registeredTime = _clock.UtcNowDateTimeOffset;
    //----------------------------------------------------------------------------

    //----------------------------------------------------------------------------
    /// <summary>
    ///   Gets the captured creation time as a <see cref="DateTimeOffset"/>.
    /// </summary>
    /// <returns>The offset used when the registration was created.</returns>
    private partial DateTimeOffset GetRegisteredTimeOffset () => _registeredTime;
    //----------------------------------------------------------------------------

    //----------------------------------------------------------------------------
    /// <summary>
    ///   Gets whether the schedule uses local time-of-day semantics.
    /// </summary>
    /// <returns><c>true</c> when local calendar days apply; otherwise, <c>false</c>.</returns>
    private partial bool GetIsLocalTimeRepresentation () => !_utcTimeOfDaySchedule;
    //----------------------------------------------------------------------------

    //----------------------------------------------------------------------------
    /// <summary>
    ///   Computes the delay from now until the next time-of-day occurrence.
    /// </summary>
    /// <returns>The nonnegative delay until the next fire, or one day if the computed delay is nonpositive.</returns>
    private partial TimeSpan GetDelayUntilNextForTimer ()
    {
        // The target time of day names a wall-clock moment on each calendar day; "now" in the same
        // basis (UTC vs local) is the anchor for turning that into a delay until the next fire.
        DateTimeOffset scheduleNowOffset = GetScheduleNowOffset();
        TimeSpan delayUntilNextFire;
        if (_utcTimeOfDaySchedule)
        {
            // UTC calendar day: interpret _targetTimeOfDay on today's UTC date, then roll forward
            // one UTC day if that instant is not strictly after the current instant.
            DateTime nowUtc = scheduleNowOffset.UtcDateTime;
            DateOnly scheduleCalendarDate = DateOnly.FromDateTime(nowUtc);
            DateTime nextOccurrenceDateTime = scheduleCalendarDate.ToDateTime(_targetTimeOfDay);
            if (nextOccurrenceDateTime <= nowUtc)
            {
                nextOccurrenceDateTime = scheduleCalendarDate.AddDays(1).ToDateTime(_targetTimeOfDay);
            }

            // nextOccurrenceDateTime is UTC-unspecified; pair with UTC offset so subtraction against
            // scheduleNowOffset yields the correct elapsed time to the next fire.
            DateTimeOffset nextFireDateTimeOffset = new(nextOccurrenceDateTime, TimeSpan.Zero);
            delayUntilNextFire = nextFireDateTimeOffset - scheduleNowOffset;
        }
        else
        {
            // Local calendar day: next fire depends on the zone (DST gaps/overlaps) and duplicate/skipped
            // wall-time policies; the shared helper encapsulates that next-occurrence math.
            delayUntilNextFire =
                DayTimeBclLocalWallTimeScheduling.GetDelayUntilNextLocalDayTime(scheduleNowOffset,
                _clock.LocalScheduleTimeZone, _targetTimeOfDay, _skippedTimeBehavior,
                _duplicateTimeBehavior);
        }

        // Timer APIs expect a positive due time; clamp a zero or negative residual to one day so we
        // reschedule rather than spin or mis-schedule at the boundary.
        if (delayUntilNextFire <= TimeSpan.Zero)
        {
            delayUntilNextFire = TimeSpan.FromDays(1);
        }

        return delayUntilNextFire;
    }
    //----------------------------------------------------------------------------

    //----------------------------------------------------------------------------
    /// <summary>
    ///   Records when the next callback is expected from the given delay.
    /// </summary>
    /// <param name="delay">Delay from now until the next fire.</param>
    private partial void SetNextCallbackScheduledFromDelay (TimeSpan delay) =>
        _nextCallbackScheduledOffset = GetScheduleNowOffset() + delay;
    //----------------------------------------------------------------------------

    //----------------------------------------------------------------------------
    /// <summary>
    ///   Clears the next-callback schedule and records the start of the current tick.
    /// </summary>
    private partial void RecordDayTimeCallbackTickStarted ()
    {
        _nextCallbackScheduledOffset = null;
        _lastCallbackStartedOffset = GetScheduleNowOffset();
    }
    //----------------------------------------------------------------------------

    //----------------------------------------------------------------------------
    /// <summary>
    ///   Gets elapsed milliseconds since the last callback while holding the registration gate.
    /// </summary>
    /// <returns>
    ///   <c>-1</c> if no prior callback exists; <c>0</c> if callbacks are running or the last start is not in the past;
    ///   otherwise the elapsed milliseconds.
    /// </returns>
    private partial long GetDayTimeElapsedMillisecondsWhileLocked ()
    {
        if (_lastCallbackStartedOffset is not { } lastCallbackStartOffset)
            return -1;
        if (_callbacksRunning > 0)
            return 0;
        DateTimeOffset scheduleNowOffset = GetScheduleNowOffset();
        if (lastCallbackStartOffset >= scheduleNowOffset)
            return 0;
        return (long)(scheduleNowOffset - lastCallbackStartOffset).TotalMilliseconds;
    }
    //----------------------------------------------------------------------------

    //----------------------------------------------------------------------------
    /// <summary>
    ///   Gets remaining milliseconds until the next scheduled callback while holding the registration gate.
    /// </summary>
    /// <returns>
    ///   <c>-1</c> if no next callback is scheduled; <c>0</c> if the next instant is not in the future; otherwise the remaining milliseconds.
    /// </returns>
    private partial long GetDayTimeTimeUntilNextMillisecondsWhileLocked ()
    {
        if (_nextCallbackScheduledOffset is not { } pendingNextFireOffset)
            return -1;
        DateTimeOffset scheduleNowOffset = GetScheduleNowOffset();
        if (pendingNextFireOffset <= scheduleNowOffset)
            return 0;
        return (long)(pendingNextFireOffset - scheduleNowOffset).TotalMilliseconds;
    }
    //----------------------------------------------------------------------------

    //----------------------------------------------------------------------------
    /// <summary>
    ///   Converts a delay into a nonnegative timer duration in milliseconds.
    /// </summary>
    /// <param name="delay">Delay until the next scheduled fire.</param>
    /// <param name="milliseconds">The clamped timer duration in milliseconds.</param>
    /// <returns>Always <c>true</c> for this BCL basis.</returns>
    private partial bool TryGetTimerMillisecondsFromDelay (TimeSpan delay, out int milliseconds)
    {
        long totalMillisecondsClamped = (long)Math.Min(delay.TotalMilliseconds, int.MaxValue);
        if (totalMillisecondsClamped < 0)
            totalMillisecondsClamped = 0;
        milliseconds = (int)totalMillisecondsClamped;
        return true;
    }
    //----------------------------------------------------------------------------

    //----------------------------------------------------------------------------
    /// <summary>
    ///   Gets the retry delay in milliseconds when sequential callback execution is required.
    /// </summary>
    /// <returns>A positive millisecond count suitable for the underlying timer.</returns>
    private partial int GetRunSequentiallyRetryMilliseconds ()
    {
        int retryMilliseconds = (int)Math.Min(RunSequentiallyRetryDelay.TotalMilliseconds, int.MaxValue);
        if (retryMilliseconds <= 0)
            return 1;
        return retryMilliseconds;
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Gets whether this registration schedules using local calendar days.
    /// </summary>
    private partial bool IsLocalDayTimeSchedule => !_utcTimeOfDaySchedule;

    /// <summary>
    ///   Gets whether this registration schedules using UTC calendar days.
    /// </summary>
    private partial bool IsUtcDayTimeSchedule => _utcTimeOfDaySchedule;

    /// <summary>
    ///   Applies a local <see cref="TimeOnly"/> schedule after a dynamic change.
    /// </summary>
    /// <param name="newTimeOfDay">New time of day.</param>
    private partial void ApplyLocalScheduleTimeOfDay (TimeOnly newTimeOfDay) => _targetTimeOfDay = newTimeOfDay;

    /// <summary>
    ///   Applies a UTC <see cref="TimeOnly"/> schedule after a dynamic change.
    /// </summary>
    /// <param name="newTimeOfDay">New time of day.</param>
    private partial void ApplyUtcScheduleTimeOfDay (TimeOnly newTimeOfDay) => _targetTimeOfDay = newTimeOfDay;
}
//################################################################################
#endif
