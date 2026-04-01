// Copyright (c) Kevin Zehrer. All rights reserved.
// This file is part of the PrimeTime project.

using System.Diagnostics;

namespace KZDev.SystemClock.PrimeTime;

/// <summary>
///   Implementation of <see cref="IClockIntervalTimer"/> used by <see cref="PrimeClock"/>
///   for interval timers.
/// </summary>
internal sealed partial class ClockIntervalTimerRegistration
{
    private DateTimeOffset _registeredTime;
    private TimeSpan _initialCallbackTime;
    private TimeSpan _repeatInterval;

    private DateTimeOffset? _nextCallbackUtc;
    private DateTimeOffset? _lastCallbackUtc;

    private partial void CaptureRegisteredTime () => _registeredTime = IsLocalTimeRepresentation ? _clock.LocalNowOffset : _clock.UtcNowOffset;

    private partial DateTimeOffset GetRegisteredTime () => _registeredTime;

    private partial long GetElapsedTime ()
    {
        lock (_gate)
        {
            if (_lastCallbackUtc is not { } last)
                return -1;
            if (_callbacksRunning > 0)
                return 0;
            DateTimeOffset now = _clock.UtcNowOffset;
            if (last >= now)
                return 0;
            return (long)(now - last).TotalMilliseconds;
        }
    }

    private partial TimeSpan InitialCallbackTimeSpan { get => _initialCallbackTime; set => _initialCallbackTime = value; }

    private partial TimeSpan RepeatTimeSpanInterval { get => _repeatInterval; set => _repeatInterval = value; }

    private partial DateTimeOffset? NextCallbackUtc { get => _nextCallbackUtc; set => _nextCallbackUtc = value; }

    private partial DateTimeOffset? LastCallbackUtc { get => _lastCallbackUtc; set => _lastCallbackUtc = value; }

    private partial bool IsRepeatingTimer => _repeatInterval != Timeout.InfiniteTimeSpan && _repeatInterval > TimeSpan.Zero;
}
