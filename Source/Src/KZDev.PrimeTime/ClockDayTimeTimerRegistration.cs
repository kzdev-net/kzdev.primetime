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
private static readonly Duration RunSequentiallyRetryDelay = Duration.FromMilliseconds(30);

    /// <summary>
    ///   Indicates whether the configured time of day is interpreted in UTC or local zone per day.
    /// </summary>
    private readonly bool _utcTimeOfDaySchedule;
    private LocalTime _targetTimeOfDay;
    private Instant? _nextCallbackInstant;
    private Instant? _lastCallbackInstant;
    //----------------------------------------------------------------------------

    #region Constructors/Finalizers

    //----------------------------------------------------------------------------
    /// <summary>
    ///   Initializes a new instance with Noda <see cref="LocalTime"/> scheduling.
    /// </summary>
    /// <param name="utcTimeOfDaySchedule">
    ///   <c>true</c> for UTC calendar-day scheduling; <c>false</c> for local zone days.
    /// </param>
#if NET
    [SetsRequiredMembers]
#endif
    internal ClockDayTimeTimerRegistration (IPrimeClock clock,
        LocalTime timeOfDay,
        IntervalTimerCallbackKind callbackKind,
        Delegate callback,
        object? callbackState,
        DayTimeTimerOptions? options,
        CancellationToken cancellationToken,
        bool utcTimeOfDaySchedule = false)
    {
        _utcTimeOfDaySchedule = utcTimeOfDaySchedule;
        _targetTimeOfDay = timeOfDay;
        FinishConstruction(clock, callbackKind, callback, callbackState, options, cancellationToken);
    }
    //----------------------------------------------------------------------------

    #endregion Constructors/Finalizers

    //----------------------------------------------------------------------------
    /// <summary>
    ///   Gets the instant at which this registration was created.
    /// </summary>
    public Instant RegisteredInstant { [DebuggerStepThrough] get; [DebuggerStepThrough] private set; }
    //----------------------------------------------------------------------------

        //----------------------------------------------------------------------------
private static int DurationToTimerMilliseconds (Duration duration)
    {
        if (duration <= Duration.Zero)
            return Timeout.Infinite;
        try
        {
            TimeSpan ts = duration.ToTimeSpan();
            long msLong = (long)Math.Min(ts.TotalMilliseconds, int.MaxValue);
            if (msLong <= 0)
                return Timeout.Infinite;
            return (int)msLong;
        }
        catch (OverflowException)
        {
            return int.MaxValue;
        }
    }
    //----------------------------------------------------------------------------

        //----------------------------------------------------------------------------
private partial void CaptureRegisteredTimeForDayTimer () => RegisteredInstant = _clock.NowInstant;
    //----------------------------------------------------------------------------

        //----------------------------------------------------------------------------
private partial DateTimeOffset GetRegisteredTimeOffset () => RegisteredInstant.ToDateTimeOffset();
    //----------------------------------------------------------------------------

        //----------------------------------------------------------------------------
private partial bool GetIsLocalTimeRepresentation () => !_utcTimeOfDaySchedule;
    //----------------------------------------------------------------------------

        //----------------------------------------------------------------------------
private TimeSpan GetDelayUntilNextAsTimeSpan ()
    {
        Duration d = GetDelayUntilNextDuration();
        try
        {
            return d.ToTimeSpan();
        }
        catch (OverflowException)
        {
            return TimeSpan.FromDays(1);
        }
    }
    //----------------------------------------------------------------------------

        //----------------------------------------------------------------------------
private Duration GetDelayUntilNextDuration ()
    {
        Instant now = _clock.NowInstant;
        if (_utcTimeOfDaySchedule)
        {
            ZonedDateTime nowZ = _clock.UtcNow;
            LocalDate today = nowZ.Date;
            LocalDateTime nextLdt = today.At(_targetTimeOfDay);
            ZonedDateTime nextZdt = nextLdt.InZoneLeniently(DateTimeZone.Utc);
            if (nextZdt.ToInstant() <= now)
            {
                nextLdt = today.PlusDays(1).At(_targetTimeOfDay);
                nextZdt = nextLdt.InZoneLeniently(DateTimeZone.Utc);
            }

            return nextZdt.ToInstant() - now;
        }

        ZonedDateTime nowLocalZ = _clock.LocalZonedNow;
        LocalDate todayLocal = nowLocalZ.Date;
        LocalDateTime nextLocalLdt = todayLocal.At(_targetTimeOfDay);
        ZonedDateTime nextLocalZdt = nextLocalLdt.InZoneLeniently(nowLocalZ.Zone);
        if (nextLocalZdt.ToInstant() <= now)
        {
            nextLocalLdt = todayLocal.PlusDays(1).At(_targetTimeOfDay);
            nextLocalZdt = nextLocalLdt.InZoneLeniently(nowLocalZ.Zone);
        }

        return nextLocalZdt.ToInstant() - now;
    }
    //----------------------------------------------------------------------------

        //----------------------------------------------------------------------------
private partial TimeSpan GetDelayUntilNextForTimer () => GetDelayUntilNextAsTimeSpan();
    //----------------------------------------------------------------------------

        //----------------------------------------------------------------------------
private partial void SetNextCallbackScheduledFromDelay (TimeSpan delay) =>
        _nextCallbackInstant = _clock.NowInstant + Duration.FromTimeSpan(delay);
    //----------------------------------------------------------------------------

        //----------------------------------------------------------------------------
private partial void RecordDayTimeCallbackTickStarted ()
    {
        _nextCallbackInstant = null;
        _lastCallbackInstant = _clock.NowInstant;
    }
    //----------------------------------------------------------------------------

        //----------------------------------------------------------------------------
private partial long GetDayTimeElapsedMillisecondsWhileLocked ()
    {
        if (_lastCallbackInstant is not { } last)
            return -1;
        if (_callbacksRunning > 0)
            return 0;
        Instant now = _clock.NowInstant;
        if (last >= now)
            return 0;
        return (long)(now - last).TotalMilliseconds;
    }
    //----------------------------------------------------------------------------

        //----------------------------------------------------------------------------
private partial long GetDayTimeTimeUntilNextMillisecondsWhileLocked ()
    {
        if (_nextCallbackInstant is not { } next)
            return -1;
        Instant now = _clock.NowInstant;
        if (next <= now)
            return 0;
        return (long)(next - now).TotalMilliseconds;
    }
    //----------------------------------------------------------------------------

        //----------------------------------------------------------------------------
private partial bool TryGetTimerMillisecondsFromDelay (TimeSpan delay, out int milliseconds)
    {
        Duration d = Duration.FromTimeSpan(delay);
        int ms = DurationToTimerMilliseconds(d);
        if (ms == Timeout.Infinite)
        {
            milliseconds = 0;
            return false;
        }
        milliseconds = ms;
        return true;
    }
    //----------------------------------------------------------------------------

        //----------------------------------------------------------------------------
private partial int GetRunSequentiallyRetryMilliseconds ()
    {
        int ms = (int)Math.Min(RunSequentiallyRetryDelay.TotalMilliseconds, int.MaxValue);
        if (ms <= 0)
            return 1;
        return ms;
    }
    //----------------------------------------------------------------------------

#if NET
    private partial bool IsLocalDayTimeSchedule => !_utcTimeOfDaySchedule;

    private partial bool IsUtcDayTimeSchedule => _utcTimeOfDaySchedule;

    private partial void ApplyLocalScheduleTimeOfDay (TimeOnly value) =>
        _targetTimeOfDay = new LocalTime(value.Hour, value.Minute, value.Second, value.Millisecond);

    private partial void ApplyUtcScheduleTimeOfDay (TimeOnly value) =>
        _targetTimeOfDay = new LocalTime(value.Hour, value.Minute, value.Second, value.Millisecond);
#endif

    #region Interface Implementations

    #region IClockDayTimeTimer Implementation

    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public bool Change (LocalTime timeOfDay)
    {
        lock (_gate)
        {
            if (_disposed || _state == TimerState.Cancelled)
                return false;
            _targetTimeOfDay = timeOfDay;
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
