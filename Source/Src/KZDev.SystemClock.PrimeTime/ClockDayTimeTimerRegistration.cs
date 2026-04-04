#if NET

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
namespace KZDev.SystemClock.PrimeTime;

/// <summary>
///   BCL time-basis partial for <see cref="ClockDayTimeTimerRegistration"/>.
/// </summary>
internal sealed partial class ClockDayTimeTimerRegistration
{
    private static readonly TimeSpan RunSequentiallyRetryDelay = TimeSpan.FromMilliseconds(30);

    private readonly bool _isLocal;
    private TimeOnly _targetTimeOfDay;
    private DateTimeOffset _registeredTime;
    private DateTimeOffset? _nextCallbackUtc;
    private DateTimeOffset? _lastCallbackUtc;

    /// <summary>
    ///   Initializes a new instance for a local-time day-time timer.
    /// </summary>
#if NET
    [SetsRequiredMembers]
#endif
    internal ClockDayTimeTimerRegistration (IPrimeClock clock,
        LocalTimeOfDay timeOfDay,
        IntervalTimerCallbackKind callbackKind,
        Delegate callback,
        object? callbackState,
        DayTimeTimerOptions? options,
        CancellationToken cancellationToken)
        : this(clock, true, timeOfDay.Value, callbackKind, callback, callbackState, options, cancellationToken)
    {
    }

    /// <summary>
    ///   Initializes a new instance for a UTC day-time timer.
    /// </summary>
#if NET
    [SetsRequiredMembers]
#endif
    internal ClockDayTimeTimerRegistration (IPrimeClock clock,
        UtcTimeOfDay timeOfDay,
        IntervalTimerCallbackKind callbackKind,
        Delegate callback,
        object? callbackState,
        DayTimeTimerOptions? options,
        CancellationToken cancellationToken)
        : this(clock, false, timeOfDay.Value, callbackKind, callback, callbackState, options, cancellationToken)
    {
    }

#if NET
    [SetsRequiredMembers]
#endif
    private ClockDayTimeTimerRegistration (IPrimeClock clock,
        bool isLocal,
        TimeOnly targetTimeOfDay,
        IntervalTimerCallbackKind callbackKind,
        Delegate callback,
        object? callbackState,
        DayTimeTimerOptions? options,
        CancellationToken cancellationToken)
    {
        _isLocal = isLocal;
        _targetTimeOfDay = targetTimeOfDay;
        FinishConstruction(clock, callbackKind, callback, callbackState, options, cancellationToken);
    }

    private partial void CaptureRegisteredTimeForDayTimer () =>
        _registeredTime = _clock.UtcNowOffset;

    private partial DateTimeOffset GetRegisteredTimeOffset () => _registeredTime;

    private partial bool GetIsLocalTimeRepresentation () => _isLocal;

    private partial TimeSpan GetDelayUntilNextForTimer ()
    {
        DateTimeOffset now = _isLocal ? _clock.LocalNowOffset : _clock.UtcNowOffset;
        DateOnly today = DateOnly.FromDateTime(now.DateTime);
        DateTime nextDt = today.ToDateTime(_targetTimeOfDay);
        if (nextDt <= now.DateTime)
            nextDt = today.AddDays(1).ToDateTime(_targetTimeOfDay);
        DateTimeOffset nextOff = _isLocal
            ? new DateTimeOffset(nextDt, now.Offset)
            : new DateTimeOffset(nextDt, TimeSpan.Zero);
        TimeSpan delay = nextOff - now;
        if (delay <= TimeSpan.Zero)
            delay = TimeSpan.FromDays(1);
        return delay;
    }

    private partial void SetNextCallbackScheduledFromDelay (TimeSpan delay)
    {
        DateTimeOffset now = _isLocal ? _clock.LocalNowOffset : _clock.UtcNowOffset;
        _nextCallbackUtc = now + delay;
    }

    private partial void RecordDayTimeCallbackTickStarted ()
    {
        _nextCallbackUtc = null;
        _lastCallbackUtc = _isLocal ? _clock.LocalNowOffset : _clock.UtcNowOffset;
    }

    private partial long GetDayTimeElapsedMillisecondsWhileLocked ()
    {
        if (_lastCallbackUtc is not { } last)
            return -1;
        if (_callbacksRunning > 0)
            return 0;
        DateTimeOffset now = _isLocal ? _clock.LocalNowOffset : _clock.UtcNowOffset;
        if (last >= now)
            return 0;
        return (long)(now - last).TotalMilliseconds;
    }

    private partial long GetDayTimeTimeUntilNextMillisecondsWhileLocked ()
    {
        if (_nextCallbackUtc is not { } next)
            return -1;
        DateTimeOffset now = _isLocal ? _clock.LocalNowOffset : _clock.UtcNowOffset;
        if (next <= now)
            return 0;
        return (long)(next - now).TotalMilliseconds;
    }

    private partial bool TryGetTimerMillisecondsFromDelay (TimeSpan delay, out int milliseconds)
    {
        long msLong = (long)Math.Min(delay.TotalMilliseconds, int.MaxValue);
        if (msLong < 0)
            msLong = 0;
        milliseconds = (int)msLong;
        return true;
    }

    private partial int GetRunSequentiallyRetryMilliseconds () =>
        (int)Math.Min(RunSequentiallyRetryDelay.TotalMilliseconds, int.MaxValue);

    private partial bool IsLocalDayTimeSchedule => _isLocal;

    private partial bool IsUtcDayTimeSchedule => !_isLocal;

    private partial void ApplyLocalScheduleTimeOfDay (TimeOnly value) => _targetTimeOfDay = value;

    private partial void ApplyUtcScheduleTimeOfDay (TimeOnly value) => _targetTimeOfDay = value;
}
#endif
