// Copyright (c) Kevin Zehrer
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

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
    /// <summary>
    /// Sentinel duration used to represent a non-repeating timer interval.
    /// </summary>
    private static readonly Duration NoRepeatSentinel = Duration.FromTimeSpan(Timeout.InfiniteTimeSpan);

    /// <summary>
    /// Initial delay before the next callback executes.
    /// </summary>
    private Duration _initialCallbackDuration;
    /// <summary>
    /// Repeat interval used for subsequent callbacks.
    /// </summary>
    private Duration _repeatInterval;
    /// <summary>
    /// Absolute instant when the next callback is scheduled to execute.
    /// </summary>
    private Instant? _nextCallbackInstant;
    /// <summary>
    /// Absolute instant when the last callback started execution.
    /// </summary>
    private Instant? _lastCallbackInstant;
    //----------------------------------------------------------------------------
    /// <summary>
    /// Gets or sets the initial callback delay as a BCL <see cref="TimeSpan"/>.
    /// </summary>
    private partial TimeSpan InitialCallbackTimeSpan { [DebuggerStepThrough] get => _initialCallbackDuration.ToTimeSpan(); [DebuggerStepThrough] set => _initialCallbackDuration = Duration.FromTimeSpan(value); }
    //----------------------------------------------------------------------------
    /// <summary>
    /// Gets or sets the repeat interval as a BCL <see cref="TimeSpan"/>.
    /// </summary>
    private partial TimeSpan RepeatTimeSpanInterval { [DebuggerStepThrough] get => _repeatInterval.ToTimeSpan(); [DebuggerStepThrough] set => _repeatInterval = Duration.FromTimeSpan(value); }
    //----------------------------------------------------------------------------
    /// <summary>
    /// Gets or sets the next callback instant as UTC.
    /// </summary>
    private partial DateTimeOffset? NextCallbackUtc { [DebuggerStepThrough] get => _nextCallbackInstant?.ToDateTimeOffset(); [DebuggerStepThrough] set => _nextCallbackInstant = value.HasValue ? Instant.FromDateTimeOffset(value.Value) : null; }
    //----------------------------------------------------------------------------
    /// <summary>
    /// Gets or sets the last callback instant as UTC.
    /// </summary>
    private partial DateTimeOffset? LastCallbackUtc { [DebuggerStepThrough] get => _lastCallbackInstant?.ToDateTimeOffset(); [DebuggerStepThrough] set => _lastCallbackInstant = value.HasValue ? Instant.FromDateTimeOffset(value.Value) : null; }
    //----------------------------------------------------------------------------
    /// <summary>
    /// Gets a value indicating whether this registration is configured as repeating.
    /// </summary>
    private partial bool IsRepeatingTimer { [DebuggerStepThrough] get => _repeatInterval > Duration.Zero && _repeatInterval != NoRepeatSentinel; }
    //----------------------------------------------------------------------------
    /// <summary>
    /// Gets the elapsed milliseconds since the last callback started.
    /// </summary>
    /// <returns>
    /// Elapsed milliseconds since the last callback, <c>0</c> while a callback is running,
    /// or <c>-1</c> when no callback has run.
    /// </returns>
    private partial long GetElapsedTime ()
    {
        lock (Gate)
        {
            if (_lastCallbackInstant is not { } last)
                return -1;
            if (CallbacksRunning > 0)
                return 0;
            Instant now = Clock.NowInstant;
            if (last >= now)
                return 0;
            return (long)(now - last).TotalMilliseconds;
        }
    }
    //----------------------------------------------------------------------------
    /// <summary>
    /// Schedules the next callback to execute after the specified delay.
    /// </summary>
    /// <param name="delay">
    /// Delay interval before the next callback should execute.
    /// </param>
    private partial void SetNextCallbackScheduledForDelay (TimeSpan delay) =>
            _nextCallbackInstant = Clock.NowInstant + Duration.FromTimeSpan(delay);
    //----------------------------------------------------------------------------
    /// <summary>
    /// Records that an interval callback has started and updates scheduling state.
    /// </summary>
    /// <param name="resetIntervalBeforeCallback">
    /// <see langword="true"/> when the next callback has already been reset before callback execution;
    /// otherwise, <see langword="false"/>.
    /// </param>
    private partial void RecordIntervalCallbackStarted (bool resetIntervalBeforeCallback)
    {
        _lastCallbackInstant = Clock.NowInstant;
        if (!resetIntervalBeforeCallback)
            _nextCallbackInstant = null;
    }
    //----------------------------------------------------------------------------
    /// <summary>
    /// Gets the milliseconds until the next callback while the registration lock is held.
    /// </summary>
    /// <returns>
    /// Milliseconds until the next callback, <c>0</c> when the callback is due now,
    /// or <c>-1</c> when no callback is currently scheduled.
    /// </returns>
    private partial long GetTimeUntilNextCallbackMillisecondsWhileLocked ()
    {
        if (!IsRepeating && _lastCallbackInstant.HasValue)
            return -1;
        if (_nextCallbackInstant is not { } next)
            return -1;
        Instant now = Clock.NowInstant;
        if (next <= now)
            return 0;
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
        return ChangeNextAndRepeat(NodaDurationBclConversion.ToTimeSpanForTimerInterval(interval),
            NodaDurationBclConversion.ToTimeSpanForTimerInterval(repeat));
    }
    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public bool Change (Duration nextInterval, Duration repeatInterval)
    {
        TimeSpan nextTs = NodaDurationBclConversion.ToTimeSpanForTimerInterval(nextInterval);
        TimeSpan repeatTs = repeatInterval == NoRepeatSentinel
            ? Timeout.InfiniteTimeSpan
            : NodaDurationBclConversion.ToTimeSpanForTimerInterval(repeatInterval);
        return ChangeNextAndRepeat(nextTs, repeatTs);
    }
    //----------------------------------------------------------------------------

    #endregion IClockIntervalTimer Implementation

    #endregion Interface Implementations
}
//################################################################################
