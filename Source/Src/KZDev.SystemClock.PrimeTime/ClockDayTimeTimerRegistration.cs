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
    private DateTimeOffset? _nextCallbackUtc;

    /// <summary>
    ///   Start of the most recent callback, if any.
    /// </summary>
    private DateTimeOffset? _lastCallbackUtc;
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
    /// <param name="timeOfDay">Local time of day for recurring fires.</param>
    /// <param name="callbackKind">Shape of the user callback.</param>
    /// <param name="callback">User callback delegate.</param>
    /// <param name="callbackState">Optional state forwarded to context callbacks.</param>
    /// <param name="options">Optional timer behavior options.</param>
    /// <param name="cancellationToken">Token that cancels scheduling and callbacks.</param>
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
        : this(clock, false, timeOfDay.Value, callbackKind, callback, callbackState, options, cancellationToken)
    {
    }
    //----------------------------------------------------------------------------

    //----------------------------------------------------------------------------
    /// <summary>
    ///   Initializes a new instance for a UTC day-time timer.
    /// </summary>
    /// <param name="clock">Clock used for scheduling and reading the current time.</param>
    /// <param name="timeOfDay">UTC time of day for recurring fires.</param>
    /// <param name="callbackKind">Shape of the user callback.</param>
    /// <param name="callback">User callback delegate.</param>
    /// <param name="callbackState">Optional state forwarded to context callbacks.</param>
    /// <param name="options">Optional timer behavior options.</param>
    /// <param name="cancellationToken">Token that cancels scheduling and callbacks.</param>
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
        : this(clock, true, timeOfDay.Value, callbackKind, callback, callbackState, options, cancellationToken)
    {
    }
    //----------------------------------------------------------------------------

    #endregion Constructors/Finalizers

    //----------------------------------------------------------------------------
    /// <summary>
    ///   Gets the clock's current time in the schedule basis (UTC vs local calendar day).
    /// </summary>
    /// <returns>
    ///   <see cref="IPrimeClock.UtcNowOffset"/> for UTC calendar-day scheduling; otherwise <see cref="IPrimeClock.LocalNowOffset"/>.
    /// </returns>
    private DateTimeOffset GetScheduleNowOffset () =>
        _utcTimeOfDaySchedule ? _clock.UtcNowOffset : _clock.LocalNowOffset;
    //----------------------------------------------------------------------------

    //----------------------------------------------------------------------------
    /// <summary>
    ///   Persists the clock's current instant as this registration's creation time.
    /// </summary>
    private partial void CaptureRegisteredTimeForDayTimer () =>
        _registeredTime = _clock.UtcNowOffset;
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
        DateTimeOffset now = GetScheduleNowOffset();
        DateOnly today = DateOnly.FromDateTime(now.DateTime);
        DateTime nextDt = today.ToDateTime(_targetTimeOfDay);
        if (nextDt <= now.DateTime)
            nextDt = today.AddDays(1).ToDateTime(_targetTimeOfDay);
        DateTimeOffset nextOff = _utcTimeOfDaySchedule
            ? new DateTimeOffset(nextDt, TimeSpan.Zero)
            : new DateTimeOffset(nextDt, now.Offset);
        TimeSpan delay = nextOff - now;
        if (delay <= TimeSpan.Zero)
            delay = TimeSpan.FromDays(1);
        return delay;
    }
    //----------------------------------------------------------------------------

    //----------------------------------------------------------------------------
    /// <summary>
    ///   Records when the next callback is expected from the given delay.
    /// </summary>
    /// <param name="delay">Delay from now until the next fire.</param>
    private partial void SetNextCallbackScheduledFromDelay (TimeSpan delay) =>
        _nextCallbackUtc = GetScheduleNowOffset() + delay;
    //----------------------------------------------------------------------------

    //----------------------------------------------------------------------------
    /// <summary>
    ///   Clears the next-callback schedule and records the start of the current tick.
    /// </summary>
    private partial void RecordDayTimeCallbackTickStarted ()
    {
        _nextCallbackUtc = null;
        _lastCallbackUtc = GetScheduleNowOffset();
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
        if (_lastCallbackUtc is not { } last)
            return -1;
        if (_callbacksRunning > 0)
            return 0;
        DateTimeOffset now = GetScheduleNowOffset();
        if (last >= now)
            return 0;
        return (long)(now - last).TotalMilliseconds;
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
        if (_nextCallbackUtc is not { } next)
            return -1;
        DateTimeOffset now = GetScheduleNowOffset();
        if (next <= now)
            return 0;
        return (long)(next - now).TotalMilliseconds;
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
        long msLong = (long)Math.Min(delay.TotalMilliseconds, int.MaxValue);
        if (msLong < 0)
            msLong = 0;
        milliseconds = (int)msLong;
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
        int ms = (int)Math.Min(RunSequentiallyRetryDelay.TotalMilliseconds, int.MaxValue);
        if (ms <= 0)
            return 1;
        return ms;
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
    /// <param name="value">New time of day.</param>
    private partial void ApplyLocalScheduleTimeOfDay (TimeOnly value) => _targetTimeOfDay = value;

    /// <summary>
    ///   Applies a UTC <see cref="TimeOnly"/> schedule after a dynamic change.
    /// </summary>
    /// <param name="value">New time of day.</param>
    private partial void ApplyUtcScheduleTimeOfDay (TimeOnly value) => _targetTimeOfDay = value;
}
//################################################################################
#endif
