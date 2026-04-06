// Copyright (c) Kevin Zehrer. All rights reserved.
// This file is part of the PrimeTime project.

namespace KZDev.SystemClock.PrimeTime;

//################################################################################
/// <summary>
///   Implementation of <see cref="IClockIntervalTimer"/> used by <see cref="PrimeClock"/>
///   for interval timers.
/// </summary>
internal sealed partial class ClockIntervalTimerRegistration
{
        //----------------------------------------------------------------------------
private DateTimeOffset _registeredTime;
    private TimeSpan _initialCallbackTime;
    private TimeSpan _repeatInterval;

    private DateTimeOffset? _nextCallbackUtc;
    private DateTimeOffset? _lastCallbackUtc;
    //----------------------------------------------------------------------------

        //----------------------------------------------------------------------------
private partial TimeSpan InitialCallbackTimeSpan { get => _initialCallbackTime; set => _initialCallbackTime = value; }
    //----------------------------------------------------------------------------

        //----------------------------------------------------------------------------
private partial TimeSpan RepeatTimeSpanInterval { get => _repeatInterval; set => _repeatInterval = value; }
    //----------------------------------------------------------------------------

        //----------------------------------------------------------------------------
private partial DateTimeOffset? NextCallbackUtc { get => _nextCallbackUtc; set => _nextCallbackUtc = value; }
    //----------------------------------------------------------------------------

        //----------------------------------------------------------------------------
private partial DateTimeOffset? LastCallbackUtc { get => _lastCallbackUtc; set => _lastCallbackUtc = value; }
    //----------------------------------------------------------------------------

        //----------------------------------------------------------------------------
private partial bool IsRepeatingTimer => _repeatInterval != Timeout.InfiniteTimeSpan && _repeatInterval > TimeSpan.Zero;
    //----------------------------------------------------------------------------

        //----------------------------------------------------------------------------
private partial void CaptureRegisteredTime () => _registeredTime = IsLocalTimeRepresentation ? _clock.LocalNowDateTimeOffset : _clock.UtcNowDateTimeOffset;
    //----------------------------------------------------------------------------

        //----------------------------------------------------------------------------
private partial DateTimeOffset GetRegisteredTime () => _registeredTime;
    //----------------------------------------------------------------------------

        //----------------------------------------------------------------------------
private partial long GetElapsedTime ()
    {
        lock (_gate)
        {
            if (_lastCallbackUtc is not { } last)
                return -1;
            if (_callbacksRunning > 0)
                return 0;
            DateTimeOffset now = _clock.UtcNowDateTimeOffset;
            if (last >= now)
                return 0;
            return (long)(now - last).TotalMilliseconds;
        }
    }
    //----------------------------------------------------------------------------

        //----------------------------------------------------------------------------
private partial bool TryGetTimerMillisecondsForSchedule (TimeSpan delay, out int milliseconds)
    {
        if (delay < TimeSpan.Zero || delay == Timeout.InfiniteTimeSpan)
        {
            milliseconds = 0;
            return false;
        }
        long msLong = (long)Math.Min(delay.TotalMilliseconds, int.MaxValue);
        if (msLong < 0)
            msLong = 0;
        milliseconds = (int)msLong;
        return true;
    }
    //----------------------------------------------------------------------------

        //----------------------------------------------------------------------------
private partial void SetNextCallbackScheduledForDelay (TimeSpan delay) => NextCallbackUtc = _clock.UtcNowDateTimeOffset + delay;
    //----------------------------------------------------------------------------

        //----------------------------------------------------------------------------
private partial void RecordIntervalCallbackStarted ()
    {
        LastCallbackUtc = _clock.UtcNowDateTimeOffset;
        NextCallbackUtc = null;
    }
    //----------------------------------------------------------------------------

        //----------------------------------------------------------------------------
private partial long GetTimeUntilNextCallbackMillisecondsWhileLocked ()
    {
        if (!IsRepeating && LastCallbackUtc.HasValue)
            return -1;
        if (NextCallbackUtc is not { } next)
            return -1;
        DateTimeOffset now = _clock.UtcNowDateTimeOffset;
        if (next <= now)
            return 0;
        if (_callbacksRunning > 0 && IsResetAfterCallback)
            return (long)RepeatTimeSpanInterval.TotalMilliseconds;
        return (long)(next - now).TotalMilliseconds;
    }
    //----------------------------------------------------------------------------
}
//################################################################################
