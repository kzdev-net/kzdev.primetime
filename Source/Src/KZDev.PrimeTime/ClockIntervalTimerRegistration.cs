// Copyright (c) Kevin Zehrer. All rights reserved.
// This file is part of the PrimeTime project.

using System.Diagnostics;
using NodaTime;

namespace KZDev.PrimeTime;

//################################################################################
/// <summary>
///   Implementation of <see cref="IClockIntervalTimer"/> used by <see cref="PrimeClock"/>
///   for interval timers.
/// </summary>
internal sealed partial class ClockIntervalTimerRegistration
{
        //----------------------------------------------------------------------------
private static readonly Duration NoRepeatSentinel = Duration.FromTimeSpan(Timeout.InfiniteTimeSpan);

    private Duration _initialCallbackDuration;
    private Duration _repeatInterval;
    private Instant? _nextCallbackInstant;
    private Instant? _lastCallbackInstant;
    //----------------------------------------------------------------------------

        //----------------------------------------------------------------------------
private partial TimeSpan InitialCallbackTimeSpan { [DebuggerStepThrough] get => _initialCallbackDuration.ToTimeSpan(); [DebuggerStepThrough] set => _initialCallbackDuration = Duration.FromTimeSpan(value); }
    //----------------------------------------------------------------------------

        //----------------------------------------------------------------------------
private partial TimeSpan RepeatTimeSpanInterval { [DebuggerStepThrough] get => _repeatInterval.ToTimeSpan(); [DebuggerStepThrough] set => _repeatInterval = Duration.FromTimeSpan(value); }
    //----------------------------------------------------------------------------

        //----------------------------------------------------------------------------
private partial DateTimeOffset? NextCallbackUtc { [DebuggerStepThrough] get => _nextCallbackInstant?.ToDateTimeOffset(); [DebuggerStepThrough] set => _nextCallbackInstant = value.HasValue ? Instant.FromDateTimeOffset(value.Value) : null; }
    //----------------------------------------------------------------------------

        //----------------------------------------------------------------------------
private partial DateTimeOffset? LastCallbackUtc { [DebuggerStepThrough] get => _lastCallbackInstant?.ToDateTimeOffset(); [DebuggerStepThrough] set => _lastCallbackInstant = value.HasValue ? Instant.FromDateTimeOffset(value.Value) : null; }
    //----------------------------------------------------------------------------

        //----------------------------------------------------------------------------
private partial bool IsRepeatingTimer { [DebuggerStepThrough] get => _repeatInterval > Duration.Zero && _repeatInterval != NoRepeatSentinel; }
    //----------------------------------------------------------------------------

    //----------------------------------------------------------------------------
    /// <inheritdoc />
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
private partial void CaptureRegisteredTime () => RegisteredInstant = _clock.NowInstant;
    //----------------------------------------------------------------------------

        //----------------------------------------------------------------------------
private partial DateTimeOffset GetRegisteredTime () => RegisteredInstant.ToDateTimeOffset();
    //----------------------------------------------------------------------------

        //----------------------------------------------------------------------------
private partial long GetElapsedTime ()
    {
        lock (_gate)
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
    }
    //----------------------------------------------------------------------------

        //----------------------------------------------------------------------------
private partial bool TryGetTimerMillisecondsForSchedule (TimeSpan delay, out int milliseconds)
    {
        Duration d = Duration.FromTimeSpan(delay);
        if (d <= Duration.Zero || d == NoRepeatSentinel)
        {
            milliseconds = 0;
            return false;
        }
        int ms = DurationToTimerMilliseconds(d);
        if (ms <= 0)
        {
            milliseconds = 0;
            return false;
        }
        milliseconds = ms;
        return true;
    }
    //----------------------------------------------------------------------------

        //----------------------------------------------------------------------------
private partial void SetNextCallbackScheduledForDelay (TimeSpan delay) =>
        _nextCallbackInstant = _clock.NowInstant + Duration.FromTimeSpan(delay);
    //----------------------------------------------------------------------------

        //----------------------------------------------------------------------------
private partial void RecordIntervalCallbackStarted ()
    {
        _lastCallbackInstant = _clock.NowInstant;
        _nextCallbackInstant = null;
    }
    //----------------------------------------------------------------------------

        //----------------------------------------------------------------------------
private partial long GetTimeUntilNextCallbackMillisecondsWhileLocked ()
    {
        if (!IsRepeating && _lastCallbackInstant.HasValue)
            return -1;
        if (_nextCallbackInstant is not { } next)
            return -1;
        Instant now = _clock.NowInstant;
        if (next <= now)
            return 0;
        if (_callbacksRunning > 0 && IsResetAfterCallback)
            return (long)_repeatInterval.TotalMilliseconds;
        return (long)(next - now).TotalMilliseconds;
    }
    //----------------------------------------------------------------------------

    #region Interface Implementations

    #region IClockIntervalTimer Implementation

    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public bool Change (Duration interval)
    {
        Duration repeat = IsRepeating ? interval : NoRepeatSentinel;
        return ChangeNextAndRepeat(NodaDurationBclConversions.ToTimeSpanForTimerInterval(interval),
            NodaDurationBclConversions.ToTimeSpanForTimerInterval(repeat));
    }
    //----------------------------------------------------------------------------

    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public bool Change (Duration nextInterval, Duration repeatInterval)
    {
        TimeSpan nextTs = NodaDurationBclConversions.ToTimeSpanForTimerInterval(nextInterval);
        TimeSpan repeatTs = repeatInterval == NoRepeatSentinel
            ? Timeout.InfiniteTimeSpan
            : NodaDurationBclConversions.ToTimeSpanForTimerInterval(repeatInterval);
        return ChangeNextAndRepeat(nextTs, repeatTs);
    }
    //----------------------------------------------------------------------------

    #endregion IClockIntervalTimer Implementation

    #endregion Interface Implementations
}
//################################################################################
