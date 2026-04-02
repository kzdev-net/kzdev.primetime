// Copyright (c) Kevin Zehrer. All rights reserved.
// This file is part of the PrimeTime project.

using NodaTime;
using NodaTime.TimeZones;

namespace KZDev.PrimeTime;

/// <summary>
///   NodaTime virtual instant storage, zone mapping, and Noda-specific surface for
///   <see cref="PrimeTestClock"/>.
/// </summary>
public sealed partial class PrimeTestClock
{
    private static readonly Duration MaxDurationForDelay = Duration.FromTimeSpan(TimeSpan.MaxValue);
    private static readonly Duration MaxDurationForCancellationToken = Duration.FromMilliseconds(int.MaxValue);

    private Instant _now;
    private readonly DateTimeZone _zone;

    #region Constructors/Finalizers

    /// <summary>
    ///   Initializes a new instance with virtual time set to the current system instant.
    /// </summary>
    public PrimeTestClock ()
    {
        _now = SystemClock.Instance.GetCurrentInstant();
        _zone = GetSystemDefaultTimeZone();
    }

    /// <summary>
    ///   Initializes a new instance with the specified initial instant and system default zone.
    /// </summary>
    /// <param name="initialInstant">The initial virtual instant.</param>
    public PrimeTestClock (Instant initialInstant)
    {
        _now = initialInstant;
        _zone = GetSystemDefaultTimeZone();
    }

    /// <summary>
    ///   Initializes a new instance with the specified initial instant and time zone.
    /// </summary>
    /// <param name="initialInstant">The initial virtual instant.</param>
    /// <param name="zone">The zone used for local "now" values.</param>
    public PrimeTestClock (Instant initialInstant, DateTimeZone zone)
    {
        _now = initialInstant;
        _zone = zone ?? throw new ArgumentNullException(nameof(zone));
    }

    #endregion Constructors/Finalizers

    #region Virtual time hooks

    private partial DateTimeOffset ToLocalOffset (DateTimeOffset utcNowOffset)
    {
        Instant instant = Instant.FromDateTimeUtc(utcNowOffset.UtcDateTime);
        return instant.InZone(_zone).ToDateTimeOffset();
    }

    private partial TimeSpan GetLocalWallClockUtcOffset (DateTime localUnspecified)
    {
        LocalDateTime ldt = LocalDateTime.FromDateTime(DateTime.SpecifyKind(localUnspecified, DateTimeKind.Unspecified));
        return _zone.AtLeniently(ldt).Offset.ToTimeSpan();
    }

    private partial void SetVirtualUtcNowLocked (DateTimeOffset utcNowOffset) =>
        _now = Instant.FromDateTimeUtc(utcNowOffset.UtcDateTime);

    private partial DateTimeOffset ReadVirtualUtcNowLocked () =>
        new DateTimeOffset(_now.ToDateTimeUtc(), TimeSpan.Zero);

    private partial void AddVirtualTimeLocked (TimeSpan duration) =>
        _now += Duration.FromTimeSpan(duration);

    private partial void RaiseClockEventsAfterVirtualUtcChange (DateTimeOffset utcNowOffset)
    {
        Instant snapshot;
        lock (_gate)
            snapshot = _now;

        ClockEvents?.Invoke(this, new NodaClockTimeChangedEventArgs(snapshot));
    }

    #endregion Virtual time hooks

    private static DateTimeZone GetSystemDefaultTimeZone ()
    {
        try
        {
            return DateTimeZoneProviders.Bcl.GetSystemDefault();
        }
        catch (DateTimeZoneNotFoundException)
        {
            return BclDateTimeZone.ForSystemDefault();
        }
    }

    /// <summary>
    ///   Converts <see cref="LocalTime"/> to a time-of-day <see cref="TimeSpan"/> since midnight.
    /// </summary>
    /// <param name="t">The local time of day.</param>
    /// <returns>Elapsed time since midnight matching <paramref name="t"/>.</returns>
    private static TimeSpan LocalTimeToTargetTimeOfDay (LocalTime t)
    {
        Period sinceMidnight = Period.Between(LocalTime.Midnight, t);
        return sinceMidnight.ToDuration().ToTimeSpan();
    }

    /// <summary>
    ///   Clamps <paramref name="duration"/> for BCL delay APIs (virtual sleep / delay).
    /// </summary>
    private static TimeSpan ToTimeSpanForDelay (Duration duration)
    {
        if (duration <= Duration.Zero)
            return TimeSpan.Zero;

        if (duration >= MaxDurationForDelay)
            return TimeSpan.MaxValue;

        return duration.ToTimeSpan();
    }

    /// <summary>
    ///   Clamps <paramref name="duration"/> for <see cref="CancellationTokenSource"/> delay limits.
    /// </summary>
    private static TimeSpan ToTimeSpanForCancellationToken (Duration duration)
    {
        if (duration <= Duration.Zero)
            return TimeSpan.Zero;

        if (duration >= MaxDurationForCancellationToken)
            return TimeSpan.FromMilliseconds(int.MaxValue);

        return duration.ToTimeSpan();
    }

    #region IPrimeTestClock Implementation — Noda

    /// <inheritdoc />
    public void SetInstant (Instant instant)
    {
        DateTimeOffset utc = new DateTimeOffset(instant.ToDateTimeUtc(), TimeSpan.Zero);
        lock (_gate)
            SetVirtualUtcNowLocked(utc);

        RaiseClockEventsAfterVirtualUtcChange(utc);
    }

    /// <inheritdoc />
    public void SetLocalTime (LocalDateTime localDateTime)
    {
        Instant instant = localDateTime.InZoneLeniently(_zone).ToInstant();
        DateTimeOffset utc = new DateTimeOffset(instant.ToDateTimeUtc(), TimeSpan.Zero);
        lock (_gate)
            SetVirtualUtcNowLocked(utc);

        RaiseClockEventsAfterVirtualUtcChange(utc);
    }

    /// <inheritdoc />
    public void Advance (Duration duration) =>
        Advance(duration.ToTimeSpan());

    /// <inheritdoc />
    public void RunFor (Duration duration) =>
        RunFor(duration.ToTimeSpan());

    /// <inheritdoc />
    public void Start (Duration? rate = null) =>
        Start(rate?.ToTimeSpan());

    #endregion IPrimeTestClock Implementation — Noda

    #region IPrimeClock Implementation — Now (Noda)

    /// <inheritdoc />
    public Instant NowInstant
    {
        get
        {
            lock (_gate)
                return _now;
        }
    }

    /// <inheritdoc />
    public ZonedDateTime UtcNow
    {
        get
        {
            lock (_gate)
                return _now.InUtc();
        }
    }

    /// <inheritdoc />
    public ZonedDateTime LocalZonedNow
    {
        get
        {
            lock (_gate)
                return _now.InZone(_zone);
        }
    }

    /// <inheritdoc />
    public ZonedDateTime UtcZonedNow => UtcNow;

    /// <inheritdoc />
    public DateTimeOffset LocalNowOffset => LocalZonedNow.ToDateTimeOffset();

    /// <inheritdoc />
    public DateTimeOffset UtcNowOffset => UtcNow.ToDateTimeOffset();

    /// <inheritdoc />
    public DateTime LocalNowDateTime => LocalNowOffset.LocalDateTime;

    /// <inheritdoc />
    public DateTime UtcNowDateTime => UtcNowOffset.UtcDateTime;

    /// <inheritdoc />
    public LocalDateTime LocalNow => LocalZonedNow.LocalDateTime;

    /// <inheritdoc />
    public LocalTime LocalNowTime => LocalZonedNow.TimeOfDay;

    /// <inheritdoc />
    public LocalTime UtcNowTime => UtcNow.TimeOfDay;

    /// <inheritdoc />
    public LocalDate LocalNowDate => LocalZonedNow.Date;

    /// <inheritdoc />
    public LocalDate UtcNowDate => UtcNow.Date;

#if NET
    /// <inheritdoc />
    public TimeOnly LocalNowTimeOnly => TimeOnly.FromDateTime(LocalNowDateTime);

    /// <inheritdoc />
    public TimeOnly UtcNowTimeOnly => TimeOnly.FromDateTime(UtcNowDateTime);

    /// <inheritdoc />
    public DateOnly LocalNowDateOnly => DateOnly.FromDateTime(LocalNowDateTime);

    /// <inheritdoc />
    public DateOnly UtcNowDateOnly => DateOnly.FromDateTime(UtcNowDateTime);
#endif

    #endregion IPrimeClock Implementation — Now (Noda)

    #region IPrimeTime / IPrimeClock — Delays (Duration)

    /// <inheritdoc />
    public void Sleep (Duration duration) =>
        Sleep(ToTimeSpanForDelay(duration));

    /// <inheritdoc />
    public Task DelayAsync (Duration duration) =>
        DelayAsync(ToTimeSpanForDelay(duration));

    /// <inheritdoc />
    public Task DelayAsync (Duration duration, CancellationToken cancellationToken) =>
        DelayAsync(ToTimeSpanForDelay(duration), cancellationToken);

    #endregion IPrimeTime / IPrimeClock — Delays (Duration)

    #region IPrimeTime / IPrimeClock — Time cancellation (Duration)

    /// <inheritdoc />
    public TimeCancellationTokenSource GetTimeCancellationToken (Duration cancelAfter) =>
        GetTimeCancellationToken(ToTimeSpanForCancellationToken(cancelAfter));

    /// <inheritdoc />
    public TimeCancellationTokenSource LinkTimeCancellationToken (Duration cancelAfter, CancellationToken cancellationToken) =>
        LinkTimeCancellationToken(ToTimeSpanForCancellationToken(cancelAfter), cancellationToken);

    /// <inheritdoc />
    public TimeCancellationTokenSource LinkTimeCancellationToken (Duration cancelAfter, CancellationToken token1,
        CancellationToken token2) =>
        LinkTimeCancellationToken(ToTimeSpanForCancellationToken(cancelAfter), token1, token2);

    /// <inheritdoc />
    public TimeCancellationTokenSource LinkTimeCancellationToken (Duration cancelAfter,
        params CancellationToken[] cancellationTokens) =>
        LinkTimeCancellationToken(ToTimeSpanForCancellationToken(cancelAfter), cancellationTokens);

    #endregion IPrimeTime / IPrimeClock — Time cancellation (Duration)

    #region IPrimeClock Implementation — Interval timers (Duration)

    /// <inheritdoc />
    public IClockIntervalTimer RegisterTimer (Duration callbackTime,
        Action callback,
        CancellationToken cancellationToken,
        bool repeat = false,
        IntervalTimerOptions? timerOptions = null) =>
        RegisterTimer(callbackTime.ToTimeSpan(), callback, cancellationToken, repeat, timerOptions);

    /// <inheritdoc />
    public IClockIntervalTimer RegisterTimer (Duration callbackTime,
        Action<ClockTimerCallbackContext> callback,
        CancellationToken cancellationToken,
        object? state = null,
        bool repeat = false,
        IntervalTimerOptions? timerOptions = null) =>
        RegisterTimer(callbackTime.ToTimeSpan(), callback, cancellationToken, state, repeat, timerOptions);

    /// <inheritdoc />
    public IClockIntervalTimer RegisterTimer (Duration callbackTime,
        Action<ClockTimerCallbackContext, CancellationToken> callback,
        CancellationToken cancellationToken,
        object? state = null,
        bool repeat = false,
        IntervalTimerOptions? timerOptions = null) =>
        RegisterTimer(callbackTime.ToTimeSpan(), callback, cancellationToken, state, repeat, timerOptions);

    /// <inheritdoc />
    public IClockIntervalTimer RegisterAsyncTimer (Duration callbackTime,
        Func<CancellationToken, ValueTask> callback,
        CancellationToken cancellationToken,
        bool repeat = false,
        IntervalTimerOptions? timerOptions = null) =>
        RegisterAsyncTimer(callbackTime.ToTimeSpan(), callback, cancellationToken, repeat, timerOptions);

    /// <inheritdoc />
    public IClockIntervalTimer RegisterAsyncTimer (Duration callbackTime,
        Func<ClockTimerCallbackContext, CancellationToken, ValueTask> callback,
        CancellationToken cancellationToken,
        object? state = null,
        bool repeat = false,
        IntervalTimerOptions? timerOptions = null) =>
        RegisterAsyncTimer(callbackTime.ToTimeSpan(), callback, cancellationToken, state, repeat, timerOptions);

    /// <inheritdoc />
    public IClockIntervalTimer RegisterTimer (Duration callbackTime,
        Duration repeatInterval,
        Action callback,
        CancellationToken cancellationToken,
        IntervalTimerOptions? timerOptions = null) =>
        RegisterTimer(callbackTime.ToTimeSpan(), repeatInterval.ToTimeSpan(), callback, cancellationToken, timerOptions);

    /// <inheritdoc />
    public IClockIntervalTimer RegisterTimer (Duration callbackTime,
        Duration repeatInterval,
        Action<ClockTimerCallbackContext> callback,
        CancellationToken cancellationToken,
        object? state = null,
        IntervalTimerOptions? timerOptions = null) =>
        RegisterTimer(callbackTime.ToTimeSpan(), repeatInterval.ToTimeSpan(), callback, cancellationToken, state, timerOptions);

    /// <inheritdoc />
    public IClockIntervalTimer RegisterTimer (Duration callbackTime,
        Duration repeatInterval,
        Action<ClockTimerCallbackContext, CancellationToken> callback,
        CancellationToken cancellationToken,
        object? state = null,
        IntervalTimerOptions? timerOptions = null) =>
        RegisterTimer(callbackTime.ToTimeSpan(), repeatInterval.ToTimeSpan(), callback, cancellationToken, state, timerOptions);

    /// <inheritdoc />
    public IClockIntervalTimer RegisterAsyncTimer (Duration callbackTime,
        Duration repeatInterval,
        Func<CancellationToken, ValueTask> callback,
        CancellationToken cancellationToken,
        IntervalTimerOptions? timerOptions = null) =>
        RegisterAsyncTimer(callbackTime.ToTimeSpan(), repeatInterval.ToTimeSpan(), callback, cancellationToken, timerOptions);

    /// <inheritdoc />
    public IClockIntervalTimer RegisterAsyncTimer (Duration callbackTime,
        Duration repeatInterval,
        Func<ClockTimerCallbackContext, CancellationToken, ValueTask> callback,
        CancellationToken cancellationToken,
        object? state = null,
        IntervalTimerOptions? timerOptions = null) =>
        RegisterAsyncTimer(callbackTime.ToTimeSpan(), repeatInterval.ToTimeSpan(), callback, cancellationToken, state, timerOptions);

    #endregion IPrimeClock Implementation — Interval timers (Duration)

    #region IPrimeClock Implementation — Time-of-day timers (LocalTime)

    /// <inheritdoc />
    public IClockDayTimeTimer RegisterTimeOfDay (LocalTime timeOfDay,
        Action callback,
        CancellationToken cancellationToken,
        DayTimeTimerOptions? timerOptions = null) =>
        RegisterTimeOfDayLocal(LocalTimeToTargetTimeOfDay(timeOfDay), IntervalTimerCallbackKind.SimpleAction, callback, null,
            timerOptions, cancellationToken);

    /// <inheritdoc />
    public IClockDayTimeTimer RegisterTimeOfDay (LocalTime timeOfDay,
        Action<ClockTimerCallbackContext> callback,
        CancellationToken cancellationToken,
        object? state = null,
        DayTimeTimerOptions? timerOptions = null) =>
        RegisterTimeOfDayLocal(LocalTimeToTargetTimeOfDay(timeOfDay), IntervalTimerCallbackKind.ContextAction, callback, state,
            timerOptions, cancellationToken);

    /// <inheritdoc />
    public IClockDayTimeTimer RegisterTimeOfDay (LocalTime timeOfDay,
        Action<ClockTimerCallbackContext, CancellationToken> callback,
        CancellationToken cancellationToken,
        object? state = null,
        DayTimeTimerOptions? timerOptions = null) =>
        RegisterTimeOfDayLocal(LocalTimeToTargetTimeOfDay(timeOfDay), IntervalTimerCallbackKind.ContextActionWithToken, callback,
            state, timerOptions, cancellationToken);

    /// <inheritdoc />
    public IClockDayTimeTimer RegisterAsyncTimeOfDay (LocalTime timeOfDay,
        Func<CancellationToken, ValueTask> callback,
        CancellationToken cancellationToken,
        DayTimeTimerOptions? timerOptions = null) =>
        RegisterTimeOfDayLocal(LocalTimeToTargetTimeOfDay(timeOfDay), IntervalTimerCallbackKind.SimpleAsync, callback, null,
            timerOptions, cancellationToken);

    /// <inheritdoc />
    public IClockDayTimeTimer RegisterAsyncTimeOfDay (LocalTime timeOfDay,
        Func<ClockTimerCallbackContext, CancellationToken, ValueTask> callback,
        CancellationToken cancellationToken,
        object? state = null,
        DayTimeTimerOptions? timerOptions = null) =>
        RegisterTimeOfDayLocal(LocalTimeToTargetTimeOfDay(timeOfDay), IntervalTimerCallbackKind.ContextAsync, callback, state,
            timerOptions, cancellationToken);

    /// <summary>
    ///   Noda <see cref="IClockDayTimeTimer"/> change overloads for shared virtual day-time timers.
    /// </summary>
    private abstract partial class VirtualDayTimeTimerBase
    {
        #region IClockTimer (Noda) Implementation

        /// <inheritdoc />
        public Instant RegisteredInstant => Instant.FromDateTimeOffset(RegisteredTime);

        #endregion IClockTimer (Noda) Implementation

        #region IClockDayTimeTimer (Noda) Implementation

        /// <inheritdoc />
        public bool Change (LocalTime timeOfDay)
        {
            if (!IsLocal)
                return false;

            lock (Gate)
            {
                if (Disposed || State == TimerState.Cancelled)
                    return false;
                TargetTimeOfDay = LocalTimeToTargetTimeOfDay(timeOfDay);
                if (Enabled)
                    NextDueUtc = ComputeNextDue(Clock.UtcNowOffset);

                return true;
            }
        }

        /// <inheritdoc />
        public bool Change (Duration interval) => false;

        #endregion IClockDayTimeTimer (Noda) Implementation
    }

    #endregion IPrimeClock Implementation — Time-of-day timers (LocalTime)
}
