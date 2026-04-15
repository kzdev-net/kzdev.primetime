// Copyright (c) Kevin Zehrer
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace KZDev.SystemClock.PrimeTime;

//################################################################################
/// <summary>
///   Implementation of <see cref="IClockIntervalTimer"/> used by <see cref="PrimeClock"/>.
/// </summary>
internal sealed partial class ClockIntervalTimerRegistration
{
    //----------------------------------------------------------------------------
    /// <summary>
    ///   Instant captured for <see cref="RegisteredTime"/> (local or UTC per representation).
    /// </summary>
    private DateTimeOffset _registeredTime;

    /// <summary>
    ///   Initial due-time basis backing <see cref="InitialCallbackTimeSpan"/>.
    /// </summary>
    private TimeSpan _initialCallbackTime;

    /// <summary>
    ///   Repeat interval backing <see cref="RepeatTimeSpanInterval"/>.
    /// </summary>
    private TimeSpan _repeatInterval;

    /// <summary>
    ///   Next scheduled callback instant (UTC-offset), backing <see cref="NextCallbackUtc"/>.
    /// </summary>
    private DateTimeOffset? _nextCallbackUtc;

    /// <summary>
    ///   Last callback start instant (UTC-offset), backing <see cref="LastCallbackUtc"/>.
    /// </summary>
    private DateTimeOffset? _lastCallbackUtc;
    //----------------------------------------------------------------------------
    /// <summary>
    ///   Initial delay and current per-tick delay basis (stack-specific partial).
    /// </summary>
    private partial TimeSpan InitialCallbackTimeSpan { [DebuggerStepThrough] get => _initialCallbackTime; [DebuggerStepThrough] set => _initialCallbackTime = value; }
    //----------------------------------------------------------------------------
    /// <summary>
    ///   Repeat interval between callbacks, or infinite for one-shot.
    /// </summary>
    private partial TimeSpan RepeatTimeSpanInterval { [DebuggerStepThrough] get => _repeatInterval; [DebuggerStepThrough] set => _repeatInterval = value; }
    //----------------------------------------------------------------------------
    /// <summary>
    ///   Next scheduled callback instant in UTC-offset form (partial).
    /// </summary>
    private partial DateTimeOffset? NextCallbackUtc { [DebuggerStepThrough] get => _nextCallbackUtc; [DebuggerStepThrough] set => _nextCallbackUtc = value; }
    //----------------------------------------------------------------------------
    /// <summary>
    ///   Last callback start instant in UTC-offset form (partial).
    /// </summary>
    private partial DateTimeOffset? LastCallbackUtc { [DebuggerStepThrough] get => _lastCallbackUtc; [DebuggerStepThrough] set => _lastCallbackUtc = value; }
    //----------------------------------------------------------------------------
    /// <summary>
    ///   Whether this registration repeats after the first fire.
    /// </summary>
    private partial bool IsRepeatingTimer { [DebuggerStepThrough] get => _repeatInterval != Timeout.InfiniteTimeSpan && _repeatInterval > TimeSpan.Zero; }
    //----------------------------------------------------------------------------
    /// <summary>
    ///   Captures <see cref="RegisteredTime"/> from the clock per local/UTC option (partial).
    /// </summary>
    private partial void CaptureRegisteredTime () => _registeredTime = IsLocalTimeRepresentation ? _clock.LocalNowDateTimeOffset : _clock.UtcNowDateTimeOffset;
    //----------------------------------------------------------------------------
    /// <summary>
    ///   Returns <see cref="RegisteredTime"/> in the registration time basis (partial).
    /// </summary>
    /// <returns>
    ///   The registered instant in the configured time representation (local or UTC per <see cref="IsLocalTimeRepresentation"/>).
    /// </returns>
    private partial DateTimeOffset GetRegisteredTime () => _registeredTime;
    //----------------------------------------------------------------------------
    /// <summary>
    ///   Computes elapsed milliseconds since last callback per contract (partial).
    /// </summary>
    /// <returns>
    ///   Elapsed milliseconds since the last callback, <c>0</c> when a callback is in flight or last is not before now, or <c>-1</c> when there is no last callback.
    /// </returns>
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
    /// <summary>
    ///   Records the logical next callback instant/offset as <c>now + delay</c> for the active time basis.
    /// </summary>
    /// <param name="delay">
    ///   Delay until the next scheduled callback.
    /// </param>
    private partial void SetNextCallbackScheduledForDelay (TimeSpan delay) => NextCallbackUtc = _clock.UtcNowDateTimeOffset + delay;
    //----------------------------------------------------------------------------
    /// <summary>
    ///   Marks the start of a timer callback: records &quot;last callback&quot; and, when
    ///   <paramref name="resetIntervalBeforeCallback"/> is <c>false</c>, clears the next-callback marker.
    /// </summary>
    /// <param name="resetIntervalBeforeCallback">
    ///   When <c>true</c>, the next callback instant was already scheduled and is left unchanged.
    /// </param>
    private partial void RecordIntervalCallbackStarted (bool resetIntervalBeforeCallback)
    {
        LastCallbackUtc = _clock.UtcNowDateTimeOffset;
        if (!resetIntervalBeforeCallback)
            NextCallbackUtc = null;
    }
    //----------------------------------------------------------------------------
    /// <summary>
    ///   Computes <see cref="TimeUntilNextCallback"/> while <see cref="_gate"/> is held.
    /// </summary>
    /// <returns>
    ///   Milliseconds until the next callback, <c>0</c> when due or overdue, or <c>-1</c> when not applicable or not scheduled.
    /// </returns>
    private partial long GetTimeUntilNextCallbackMillisecondsWhileLocked ()
    {
        if (!IsRepeating && LastCallbackUtc.HasValue)
            return -1;
        if (NextCallbackUtc is not { } next)
            return -1;
        DateTimeOffset now = _clock.UtcNowDateTimeOffset;
        if (next <= now)
            return 0;
        return (long)(next - now).TotalMilliseconds;
    }
    //----------------------------------------------------------------------------
}
//################################################################################
