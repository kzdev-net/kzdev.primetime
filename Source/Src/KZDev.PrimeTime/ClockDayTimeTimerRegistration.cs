// Copyright (c) Kevin Zehrer
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

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
    /// <summary>
    ///   Converts a Noda <see cref="Duration"/> to a BCL timer millisecond value, clamping overflow.
    /// </summary>
    /// <param name="duration">Elapsed duration to convert.</param>
    /// <returns>
    ///   <see cref="Timeout.Infinite"/> when the duration is nonpositive or not representable as a finite timer; otherwise a clamped millisecond count.
    /// </returns>
    /// <remarks>
    ///   <see cref="OverflowException"/> from <see cref="Duration.ToTimeSpan"/> is caught and mapped to <see cref="int.MaxValue"/> as a finite timer clamp.
    /// </remarks>
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

#if NET
    /// <summary>
    ///   Applies a local <see cref="TimeOnly"/> schedule after a dynamic change, preserving the full
    ///   precision of <see cref="TimeOnly"/> (100-nanosecond tick resolution).
    /// </summary>
    /// <param name="newTimeOfDay">New time of day.</param>
    private partial void ApplyLocalScheduleTimeOfDay (TimeOnly newTimeOfDay) =>
        _targetTimeOfDay = LocalTime.FromTicksSinceMidnight(newTimeOfDay.Ticks);

    /// <summary>
    ///   Applies a UTC <see cref="TimeOnly"/> schedule after a dynamic change, preserving the full
    ///   precision of <see cref="TimeOnly"/> (100-nanosecond tick resolution).
    /// </summary>
    /// <param name="newTimeOfDay">New time of day.</param>
    private partial void ApplyUtcScheduleTimeOfDay (TimeOnly newTimeOfDay) =>
        _targetTimeOfDay = LocalTime.FromTicksSinceMidnight(newTimeOfDay.Ticks);
#endif

    //----------------------------------------------------------------------------
    /// <summary>
    ///   Converts the delay until the next fire to a <see cref="TimeSpan"/>, clamping overflow.
    /// </summary>
    /// <returns>The delay as a <see cref="TimeSpan"/>, or one day when conversion overflows.</returns>
    /// <remarks>
    ///   <see cref="OverflowException"/> from <see cref="Duration.ToTimeSpan"/> is caught; the method returns a <see cref="TimeSpan"/> of one day instead of throwing.
    /// </remarks>
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
    /// <summary>
    ///   Computes the duration from now until the next time-of-day occurrence.
    /// </summary>
    /// <returns>The nonnegative duration until the next fire.</returns>
    private Duration GetDelayUntilNextDuration ()
    {
        // _targetTimeOfDay picks which wall-clock time fires each day; the clock's current instant is
        // subtracted from the next matching zoned occurrence to produce a duration for the timer.
        Instant nowInstant = Clock.NowInstant;
        if (UtcTimeOfDaySchedule)
        {
            // UTC schedule: use today's UTC calendar date, attach _targetTimeOfDay, map through UTC.
            // If that instant is still on or before now, use tomorrow's UTC date instead.
            ZonedDateTime utcZonedNow = Clock.UtcNowInstant;
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
        ZonedDateTime localZonedNow = Clock.LocalZonedNowInstant;
        return DayTimeNodaLocalWallTimeScheduling.GetDelayUntilNextLocalDayTime(nowInstant,
            localZonedNow.Zone, _targetTimeOfDay, SkippedTimeBehavior, DuplicateTimeBehavior);
    }
    //----------------------------------------------------------------------------
    /// <summary>
    ///   Computes the delay from now until the next time-of-day occurrence for the underlying timer.
    /// </summary>
    /// <returns>The delay as a <see cref="TimeSpan"/>.</returns>
    private partial TimeSpan GetDelayUntilNextForTimer () => GetDelayUntilNextAsTimeSpan();
    //----------------------------------------------------------------------------
    /// <summary>
    ///   Records when the next callback is expected from the given delay.
    /// </summary>
    /// <param name="delay">Delay from now until the next fire.</param>
    private partial void SetNextCallbackScheduledFromDelay (TimeSpan delay) =>
        _nextCallbackInstant = Clock.NowInstant + Duration.FromTimeSpan(delay);
    //----------------------------------------------------------------------------
    /// <summary>
    ///   Clears the next-callback schedule and records the start of the current tick.
    /// </summary>
    private partial void RecordDayTimeCallbackTickStarted ()
    {
        _nextCallbackInstant = null;
        _lastCallbackInstant = Clock.NowInstant;
    }
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
        if (CallbacksRunning > 0)
            return 0;
        Instant nowInstant = Clock.NowInstant;
        if (lastCallbackStartInstant >= nowInstant)
            return 0;
        return (long)(nowInstant - lastCallbackStartInstant).TotalMilliseconds;
    }
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
        Instant nowInstant = Clock.NowInstant;
        if (nextCallbackInstant <= nowInstant)
            return 0;
        return (long)(nextCallbackInstant - nowInstant).TotalMilliseconds;
    }
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
    /// <exception cref="OutOfMemoryException">
    ///   Thrown when the shared finish step cannot allocate the underlying <see cref="Timer"/> for the first schedule.
    /// </exception>
#if NET
    [SetsRequiredMembers]
#endif
    internal ClockDayTimeTimerRegistration (IPrimeClock clock, bool utcTimeOfDaySchedule,
        LocalTime targetTimeOfDay, TimerCallbackKind callbackKind,
        Delegate callback, object? callbackState, DayTimeTimerOptions? options,
        CancellationToken cancellationToken)
    {
        _targetTimeOfDay = targetTimeOfDay;
        FinishConstruction(clock, utcTimeOfDaySchedule, callbackKind, callback, callbackState, options, cancellationToken);
    }
    //----------------------------------------------------------------------------

    #endregion Constructors/Finalizers

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
        lock (Gate)
        {
            if (Disposed || State == TimerState.Cancelled)
                return false;
            _targetTimeOfDay = targetTimeOfDay;
            if (!InternalEnabled)
                return true;
            ScheduleNext();
            return true;
        }
    }
    //----------------------------------------------------------------------------

    #endregion IClockDayTimeTimer Implementation

    #endregion Interface Implementations
}
//################################################################################
