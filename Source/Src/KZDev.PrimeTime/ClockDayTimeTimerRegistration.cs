// Copyright (c) Kevin Zehrer. All rights reserved.
// This file is part of the PrimeTime project.

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

using NodaTime;

namespace KZDev.PrimeTime;

//################################################################################
/// <summary>
///   NodaTime partial for <see cref="ClockDayTimeTimerRegistration"/> (instant/duration basis).
/// </summary>
internal sealed partial class ClockDayTimeTimerRegistration
{
    //----------------------------------------------------------------------------
    /// <summary>
    ///   Delay applied between scheduling attempts when concurrent callbacks are disallowed and a tick is still running.
    /// </summary>
    private static readonly Duration RunSequentiallyRetryDelay = Duration.FromMilliseconds(30);

    /// <summary>
    ///   Indicates whether the configured time of day is interpreted in UTC or local zone per day.
    /// </summary>
    private readonly bool _utcTimeOfDaySchedule;

    /// <summary>
    ///   Wall-clock time of day used to compute the next fire.
    /// </summary>
    private LocalTime _targetTimeOfDay;

    /// <summary>
    ///   Scheduled start of the next callback, if one is pending.
    /// </summary>
    private Instant? _nextCallbackInstant;

    /// <summary>
    ///   Start of the most recent callback, if any.
    /// </summary>
    private Instant? _lastCallbackInstant;
    //----------------------------------------------------------------------------

    #region Constructors/Finalizers

    //----------------------------------------------------------------------------
    /// <summary>
    ///   Initializes a new instance for the specified schedule basis and Noda <see cref="LocalTime"/> of day.
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
    internal ClockDayTimeTimerRegistration (IPrimeClock clock,
        bool utcTimeOfDaySchedule,
        LocalTime targetTimeOfDay,
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

    #endregion Constructors/Finalizers

    //----------------------------------------------------------------------------
    /// <summary>
    ///   Converts a Noda <see cref="Duration"/> to a BCL timer millisecond value, clamping overflow.
    /// </summary>
    /// <param name="duration">Elapsed duration to convert.</param>
    /// <returns>
    ///   <see cref="Timeout.Infinite"/> when the duration is nonpositive or not representable as a finite timer; otherwise a clamped millisecond count.
    /// </returns>
    private static int DurationToTimerMilliseconds (Duration duration)
    {
        if (duration <= Duration.Zero)
            return Timeout.Infinite;
        try
        {
            TimeSpan timeSpan = duration.ToTimeSpan();
            long totalMillisecondsClamped = (long)Math.Min(timeSpan.TotalMilliseconds, int.MaxValue);
            if (totalMillisecondsClamped <= 0)
                return Timeout.Infinite;
            return (int)totalMillisecondsClamped;
        }
        catch (OverflowException)
        {
            return int.MaxValue;
        }
    }
    //----------------------------------------------------------------------------

    //----------------------------------------------------------------------------
    /// <summary>
    ///   Persists the clock's current instant as this registration's creation time.
    /// </summary>
    private partial void CaptureRegisteredTimeForDayTimer () => RegisteredInstant = _clock.NowInstant;
    //----------------------------------------------------------------------------

    //----------------------------------------------------------------------------
    /// <summary>
    ///   Gets the captured creation time as a <see cref="DateTimeOffset"/>.
    /// </summary>
    /// <returns>The offset corresponding to <see cref="RegisteredInstant"/>.</returns>
    private partial DateTimeOffset GetRegisteredTimeOffset () => RegisteredInstant.ToDateTimeOffset();
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
    ///   Converts the delay until the next fire to a <see cref="TimeSpan"/>, clamping overflow.
    /// </summary>
    /// <returns>The delay as a <see cref="TimeSpan"/>, or one day when conversion overflows.</returns>
    private TimeSpan GetDelayUntilNextAsTimeSpan ()
    {
        Duration delayDuration = GetDelayUntilNextDuration();
        try
        {
            return delayDuration.ToTimeSpan();
        }
        catch (OverflowException)
        {
            return TimeSpan.FromDays(1);
        }
    }
    //----------------------------------------------------------------------------

    //----------------------------------------------------------------------------
    /// <summary>
    ///   Computes the duration from now until the next time-of-day occurrence.
    /// </summary>
    /// <returns>The nonnegative duration until the next fire.</returns>
    private Duration GetDelayUntilNextDuration ()
    {
        // _targetTimeOfDay picks which wall-clock time fires each day; the clock's current instant is
        // subtracted from the next matching zoned occurrence to produce a duration for the timer.
        Instant nowInstant = _clock.NowInstant;
        if (_utcTimeOfDaySchedule)
        {
            // UTC schedule: use today's UTC calendar date, attach _targetTimeOfDay, map through UTC.
            // If that instant is still on or before now, use tomorrow's UTC date instead.
            ZonedDateTime utcZonedNow = _clock.UtcNowInstant;
            LocalDate utcCalendarDate = utcZonedNow.Date;
            LocalDateTime scheduleLocalDateTime = utcCalendarDate.At(_targetTimeOfDay);
            ZonedDateTime scheduleZonedDateTime = scheduleLocalDateTime.InZoneLeniently(DateTimeZone.Utc);
            if (scheduleZonedDateTime.ToInstant() <= nowInstant)
            {
                scheduleLocalDateTime = utcCalendarDate.PlusDays(1).At(_targetTimeOfDay);
                scheduleZonedDateTime = scheduleLocalDateTime.InZoneLeniently(DateTimeZone.Utc);
            }

            return scheduleZonedDateTime.ToInstant() - nowInstant;
        }

        // Local schedule: calendar boundaries and DST follow the clock's local zone; skipped and duplicate
        // wall-time policies match DayTimeSchedulingPolicyTable (shared with the BCL stack).
        ZonedDateTime localZonedNow = _clock.LocalZonedNowInstant;
        return DayTimeNodaLocalWallTimeScheduling.GetDelayUntilNextLocalDayTime(nowInstant,
            localZonedNow.Zone, _targetTimeOfDay, _skippedTimeBehavior, _duplicateTimeBehavior);
    }
    //----------------------------------------------------------------------------

    //----------------------------------------------------------------------------
    /// <summary>
    ///   Computes the delay from now until the next time-of-day occurrence for the underlying timer.
    /// </summary>
    /// <returns>The delay as a <see cref="TimeSpan"/>.</returns>
    private partial TimeSpan GetDelayUntilNextForTimer () => GetDelayUntilNextAsTimeSpan();
    //----------------------------------------------------------------------------

    //----------------------------------------------------------------------------
    /// <summary>
    ///   Records when the next callback is expected from the given delay.
    /// </summary>
    /// <param name="delay">Delay from now until the next fire.</param>
    private partial void SetNextCallbackScheduledFromDelay (TimeSpan delay) =>
        _nextCallbackInstant = _clock.NowInstant + Duration.FromTimeSpan(delay);
    //----------------------------------------------------------------------------

    //----------------------------------------------------------------------------
    /// <summary>
    ///   Clears the next-callback schedule and records the start of the current tick.
    /// </summary>
    private partial void RecordDayTimeCallbackTickStarted ()
    {
        _nextCallbackInstant = null;
        _lastCallbackInstant = _clock.NowInstant;
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
        if (_lastCallbackInstant is not { } lastCallbackStartInstant)
            return -1;
        if (_callbacksRunning > 0)
            return 0;
        Instant nowInstant = _clock.NowInstant;
        if (lastCallbackStartInstant >= nowInstant)
            return 0;
        return (long)(nowInstant - lastCallbackStartInstant).TotalMilliseconds;
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
        if (_nextCallbackInstant is not { } nextCallbackInstant)
            return -1;
        Instant nowInstant = _clock.NowInstant;
        if (nextCallbackInstant <= nowInstant)
            return 0;
        return (long)(nextCallbackInstant - nowInstant).TotalMilliseconds;
    }
    //----------------------------------------------------------------------------

    //----------------------------------------------------------------------------
    /// <summary>
    ///   Converts a delay into a timer duration in milliseconds when representable.
    /// </summary>
    /// <param name="delay">Delay until the next scheduled fire.</param>
    /// <param name="milliseconds">When this method returns <c>true</c>, the nonnegative timer duration in milliseconds.</param>
    /// <returns><c>true</c> when a finite millisecond delay is produced; <c>false</c> when the duration maps to an infinite timer.</returns>
    private partial bool TryGetTimerMillisecondsFromDelay (TimeSpan delay, out int milliseconds)
    {
        Duration delayDuration = Duration.FromTimeSpan(delay);
        int timerMilliseconds = DurationToTimerMilliseconds(delayDuration);
        if (timerMilliseconds == Timeout.Infinite)
        {
            milliseconds = 0;
            return false;
        }
        milliseconds = timerMilliseconds;
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

#if NET
    /// <summary>
    ///   Gets whether this registration schedules using local calendar days.
    /// </summary>
    private partial bool IsLocalDayTimeSchedule { [DebuggerStepThrough] get => !_utcTimeOfDaySchedule; }

    /// <summary>
    ///   Gets whether this registration schedules using UTC calendar days.
    /// </summary>
    private partial bool IsUtcDayTimeSchedule { [DebuggerStepThrough] get => _utcTimeOfDaySchedule; }

    /// <summary>
    ///   Applies a local <see cref="TimeOnly"/> schedule after a dynamic change.
    /// </summary>
    /// <param name="newTimeOfDay">New time of day.</param>
    private partial void ApplyLocalScheduleTimeOfDay (TimeOnly newTimeOfDay) =>
        _targetTimeOfDay = new LocalTime(newTimeOfDay.Hour, newTimeOfDay.Minute, newTimeOfDay.Second, newTimeOfDay.Millisecond);

    /// <summary>
    ///   Applies a UTC <see cref="TimeOnly"/> schedule after a dynamic change.
    /// </summary>
    /// <param name="newTimeOfDay">New time of day.</param>
    private partial void ApplyUtcScheduleTimeOfDay (TimeOnly newTimeOfDay) =>
        _targetTimeOfDay = new LocalTime(newTimeOfDay.Hour, newTimeOfDay.Minute, newTimeOfDay.Second, newTimeOfDay.Millisecond);
#endif

    #region Interface Implementations

    #region IClockTimer Implementation

    //----------------------------------------------------------------------------
    /// <summary>
    ///   Gets the instant at which this registration was created.
    /// </summary>
    public Instant RegisteredInstant { [DebuggerStepThrough] get; [DebuggerStepThrough] private set; }
    //----------------------------------------------------------------------------

    #endregion IClockTimer Implementation

    #region IClockDayTimeTimer Implementation

    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public bool Change (LocalTime targetTimeOfDay)
    {
        lock (_gate)
        {
            if (_disposed || _state == TimerState.Cancelled)
                return false;
            _targetTimeOfDay = targetTimeOfDay;
            if (!_enabled)
                return true;
            ScheduleNext();
            return true;
        }
    }
    //----------------------------------------------------------------------------

    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public bool Change (Duration interval) => false;
    //----------------------------------------------------------------------------

    #endregion IClockDayTimeTimer Implementation

    #endregion Interface Implementations
}
//################################################################################
