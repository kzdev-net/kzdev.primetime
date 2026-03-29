using System;
using NodaTime;
using NodaTime.TimeZones;

namespace KZDev.PrimeTime;

//################################################################################
/// <summary>
///   Production implementation of <see cref="IPrimeClock"/> that delegates "now" to
///   NodaTime's <see cref="SystemClock"/> and derives local time using the system
///   default time zone. Delays and time-based cancellation use the BCL (Thread.Sleep,
///   Task.Delay, CancellationTokenSource with a timer); for deterministic tests use
///   <see cref="PrimeTestClock"/> or <see cref="IPrimeTestClock"/>.
/// </summary>
internal sealed class PrimeClock : IPrimeClock
{
    private static readonly Duration MaxDurationForDelay = Duration.FromTimeSpan(TimeSpan.MaxValue);
    private static readonly Duration MaxDurationForCancellationToken = Duration.FromMilliseconds(int.MaxValue);
    private static readonly Duration NoRepeatSentinel = Duration.FromTimeSpan(Timeout.InfiniteTimeSpan);

    private readonly IClock _clock;
    private readonly DateTimeZone _systemDefaultZone;

    #region Constructors/Finalizers

    /// <summary>
    ///   Initializes a new instance of the <see cref="PrimeClock"/> class using
    ///   <see cref="SystemClock.Instance"/> and the BCL system default time zone.
    /// </summary>
    public PrimeClock ()
        : this(SystemClock.Instance, GetSystemDefaultTimeZone())
    {
    }

    /// <summary>
    ///   Initializes a new instance of the <see cref="PrimeClock"/> class with the
    ///   specified clock and system default time zone.
    /// </summary>
    /// <param name="clock">
    ///   The NodaTime clock used to obtain the current instant.
    /// </param>
    /// <param name="systemDefaultZone">
    ///   The time zone used for local "now" values (typically the system default).
    /// </param>
    public PrimeClock (IClock clock, DateTimeZone systemDefaultZone)
    {
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _systemDefaultZone = systemDefaultZone ?? throw new ArgumentNullException(nameof(systemDefaultZone));
    }

    #endregion Constructors/Finalizers

    /// <summary>
    ///   Gets the system default time zone for use as the local zone. Prefers the BCL
    ///   provider's GetSystemDefault() when the system default is mapped; otherwise
    ///   falls back to <see cref="BclDateTimeZone.ForSystemDefault"/>, which wraps
    ///   <see cref="TimeZoneInfo.Local"/> and succeeds even when the BCL provider
    ///   has no mapping (e.g. for some Windows zones like "Mid-Atlantic Standard Time").
    /// </summary>
    /// <returns>
    ///   A <see cref="DateTimeZone"/> representing the system default (local) time zone.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    ///   The system does not provide a time zone (can be thrown by the fallback).
    /// </exception>
#if NET
    private static LocalTime TimeOnlyToLocalTime (TimeOnly t) =>
        new(t.Hour, t.Minute, t.Second, t.Millisecond);
#endif

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
    ///   Converts a <see cref="Duration"/> to <see cref="TimeSpan"/> for use with BCL
    ///   delay and cancellation APIs. Negative or zero duration maps to
    ///   <see cref="TimeSpan.Zero"/>. Durations greater than
    ///   <see cref="TimeSpan.MaxValue"/> are clamped to <see cref="TimeSpan.MaxValue"/>
    ///   to avoid <see cref="OverflowException"/> from <see cref="Duration.ToTimeSpan"/>.
    /// </summary>
    private static TimeSpan ToTimeSpanForDelay (Duration duration)
    {
        if (duration <= Duration.Zero)
        {
            return TimeSpan.Zero;
        }

        if (duration >= MaxDurationForDelay)
        {
            return TimeSpan.MaxValue;
        }

        return duration.ToTimeSpan();
    }

    /// <summary>
    ///   Converts a <see cref="Duration"/> to <see cref="TimeSpan"/> for use when creating
    ///   <see cref="CancellationTokenSource"/> instances. Negative or zero duration maps to
    ///   <see cref="TimeSpan.Zero"/>. Durations greater than <see cref="int.MaxValue"/>
    ///   milliseconds are clamped to that value (as a <see cref="TimeSpan"/>), because
    ///   <see cref="CancellationTokenSource"/> only accepts delays up to that limit.
    /// </summary>
    private static TimeSpan ToTimeSpanForCancellationToken (Duration duration)
    {
        if (duration <= Duration.Zero)
        {
            return TimeSpan.Zero;
        }

        if (duration >= MaxDurationForCancellationToken)
        {
            return TimeSpan.FromMilliseconds(int.MaxValue);
        }

        return duration.ToTimeSpan();
    }

    #region Interface Implementations

    #region IPrimeClock Implementation

    /// <inheritdoc />
    public Instant NowInstant => _clock.GetCurrentInstant();

    /// <inheritdoc />
    public ZonedDateTime UtcNow => _clock.GetCurrentInstant().InUtc();

    /// <inheritdoc />
    public ZonedDateTime LocalZonedNow => _clock.GetCurrentInstant().InZone(_systemDefaultZone);

    /// <inheritdoc />
    public ZonedDateTime UtcZonedNow => UtcNow;

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

    /// <inheritdoc />
    public DateTimeOffset LocalNowOffset => LocalZonedNow.ToDateTimeOffset();

    /// <inheritdoc />
    public DateTimeOffset UtcNowOffset => UtcNow.ToDateTimeOffset();

    /// <inheritdoc />
    public DateTime LocalNowDateTime => LocalNowOffset.LocalDateTime;

    /// <inheritdoc />
    public DateTime UtcNowDateTime => UtcNowOffset.UtcDateTime;

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

    #endregion IPrimeClock Implementation

    #region IPrimeClock — Delays (Duration)

    /// <inheritdoc />
    /// <remarks>
    ///   Durations greater than <see cref="TimeSpan.MaxValue"/> are automatically clamped
    ///   to <see cref="TimeSpan.MaxValue"/> by <see cref="ToTimeSpanForDelay"/>. In that
    ///   case, <see cref="Thread.Sleep(TimeSpan)"/> blocks for approximately 29,000 years,
    ///   so passing unreasonably large durations is still undesirable despite the clamping.
    /// </remarks>
    public void Sleep (Duration duration)
    {
        TimeSpan ts = ToTimeSpanForDelay(duration);
        Thread.Sleep(ts);
    }

    /// <inheritdoc />
    public Task DelayAsync (Duration duration)
    {
        TimeSpan ts = ToTimeSpanForDelay(duration);
        return Task.Delay(ts);
    }

    /// <inheritdoc />
    public Task DelayAsync (Duration duration, CancellationToken cancellationToken)
    {
        TimeSpan ts = ToTimeSpanForDelay(duration);
        return Task.Delay(ts, cancellationToken);
    }

    #endregion IPrimeClock — Delays (Duration)

    #region IPrimeClock — Time cancellation (Duration)

    /// <inheritdoc />
    public TimeCancellationTokenSource GetTimeCancellationToken (Duration cancelAfter)
    {
        TimeSpan ts = ToTimeSpanForCancellationToken(cancelAfter);
        CancellationTokenSource cts = new(ts);
        return new TimeCancellationTokenSource(cts);
    }

    /// <inheritdoc />
    public TimeCancellationTokenSource LinkTimeCancellationToken (Duration cancelAfter, CancellationToken cancellationToken)
    {
        TimeSpan ts = ToTimeSpanForCancellationToken(cancelAfter);
        CancellationTokenSource timeCts = new(ts);
        CancellationTokenSource linkedCts =
            CancellationTokenSource.CreateLinkedTokenSource(timeCts.Token, cancellationToken);
        return new TimeCancellationTokenSource(linkedCts, [timeCts]);
    }

    /// <inheritdoc />
    public TimeCancellationTokenSource LinkTimeCancellationToken (Duration cancelAfter, CancellationToken token1,
        CancellationToken token2)
    {
        TimeSpan ts = ToTimeSpanForCancellationToken(cancelAfter);
        CancellationTokenSource timeCts = new(ts);
        CancellationTokenSource linkedCts =
            CancellationTokenSource.CreateLinkedTokenSource(timeCts.Token, token1, token2);
        return new TimeCancellationTokenSource(linkedCts, [timeCts]);
    }

    /// <inheritdoc />
    public TimeCancellationTokenSource LinkTimeCancellationToken (Duration cancelAfter,
        params CancellationToken[] cancellationTokens)
    {
        TimeSpan ts = ToTimeSpanForCancellationToken(cancelAfter);
        CancellationTokenSource timeCts = new(ts);
        CancellationTokenSource linkedCts =
            CancellationTokenSource.CreateLinkedTokenSource([timeCts.Token, .. cancellationTokens]);
        return new TimeCancellationTokenSource(linkedCts, [timeCts]);
    }

    #endregion IPrimeClock — Time cancellation (Duration)

    #region IPrimeTime Implementation

    /// <inheritdoc />
    public void Sleep (TimeSpan sleepTime) => Sleep(Duration.FromTimeSpan(sleepTime));

    /// <inheritdoc />
    public void Sleep (int sleepMilliseconds) => Sleep(Duration.FromMilliseconds(sleepMilliseconds));

    /// <inheritdoc />
    public Task DelayAsync (TimeSpan delayTime) => DelayAsync(Duration.FromTimeSpan(delayTime));

    /// <inheritdoc />
    public Task DelayAsync (int millisecondsDelay) => DelayAsync(Duration.FromMilliseconds(millisecondsDelay));

    /// <inheritdoc />
    public Task DelayAsync (TimeSpan delayTime, CancellationToken cancellationToken) =>
        DelayAsync(Duration.FromTimeSpan(delayTime), cancellationToken);

    /// <inheritdoc />
    public Task DelayAsync (int millisecondsDelay, CancellationToken cancellationToken) =>
        DelayAsync(Duration.FromMilliseconds(millisecondsDelay), cancellationToken);

    /// <inheritdoc />
    public TimeCancellationTokenSource GetTimeCancellationToken (TimeSpan cancelTime) =>
        GetTimeCancellationToken(Duration.FromTimeSpan(cancelTime));

    /// <inheritdoc />
    public TimeCancellationTokenSource GetTimeCancellationToken (int cancelMilliseconds) =>
        GetTimeCancellationToken(Duration.FromMilliseconds(cancelMilliseconds));

    /// <inheritdoc />
    public TimeCancellationTokenSource LinkTimeCancellationToken (TimeSpan cancelTime, CancellationToken cancellationToken) =>
        LinkTimeCancellationToken(Duration.FromTimeSpan(cancelTime), cancellationToken);

    /// <inheritdoc />
    public TimeCancellationTokenSource LinkTimeCancellationToken (int cancelMilliseconds, CancellationToken cancellationToken) =>
        LinkTimeCancellationToken(Duration.FromMilliseconds(cancelMilliseconds), cancellationToken);

    /// <inheritdoc />
    public TimeCancellationTokenSource LinkTimeCancellationToken (int cancelMilliseconds, CancellationToken token1,
        CancellationToken token2) =>
        LinkTimeCancellationToken(Duration.FromMilliseconds(cancelMilliseconds), token1, token2);

    /// <inheritdoc />
    public TimeCancellationTokenSource LinkTimeCancellationToken (TimeSpan cancelTime, CancellationToken token1,
        CancellationToken token2) =>
        LinkTimeCancellationToken(Duration.FromTimeSpan(cancelTime), token1, token2);

    /// <inheritdoc />
    public TimeCancellationTokenSource LinkTimeCancellationToken (TimeSpan cancelTime,
        params CancellationToken[] cancellationTokens) =>
        LinkTimeCancellationToken(Duration.FromTimeSpan(cancelTime), cancellationTokens);

    /// <inheritdoc />
    public TimeCancellationTokenSource LinkTimeCancellationToken (int cancelMilliseconds,
        params CancellationToken[] cancellationTokens) =>
        LinkTimeCancellationToken(Duration.FromMilliseconds(cancelMilliseconds), cancellationTokens);

    #endregion IPrimeTime Implementation

    #region IPrimeClock — Interval timers (RegisterTimer)

    /// <inheritdoc />
    public IClockIntervalTimer RegisterTimer (Duration callbackTime,
        Action callback,
        CancellationToken cancellationToken,
        bool repeat = false,
        IntervalTimerOptions? timerOptions = null) =>
        new PrimeClockIntervalTimerRegistration(this,
            callbackTime,
            repeat ? callbackTime : NoRepeatSentinel,
            IntervalTimerCallbackKind.SimpleAction,
            callback,
            null,
            timerOptions,
            cancellationToken);

    /// <inheritdoc />
    public IClockIntervalTimer RegisterTimer (Duration callbackTime,
        Action<ClockTimerCallbackContext> callback,
        CancellationToken cancellationToken,
        object? state = null,
        bool repeat = false,
        IntervalTimerOptions? timerOptions = null) =>
        new PrimeClockIntervalTimerRegistration(this,
            callbackTime,
            repeat ? callbackTime : NoRepeatSentinel,
            IntervalTimerCallbackKind.ContextAction,
            callback,
            state,
            timerOptions,
            cancellationToken);

    /// <inheritdoc />
    public IClockIntervalTimer RegisterTimer (Duration callbackTime,
        Action<ClockTimerCallbackContext, CancellationToken> callback,
        CancellationToken cancellationToken,
        object? state = null,
        bool repeat = false,
        IntervalTimerOptions? timerOptions = null) =>
        new PrimeClockIntervalTimerRegistration(this,
            callbackTime,
            repeat ? callbackTime : NoRepeatSentinel,
            IntervalTimerCallbackKind.ContextActionWithToken,
            callback,
            state,
            timerOptions,
            cancellationToken);

    /// <inheritdoc />
    public IClockIntervalTimer RegisterAsyncTimer (Duration callbackTime,
        Func<CancellationToken, ValueTask> callback,
        CancellationToken cancellationToken,
        bool repeat = false,
        IntervalTimerOptions? timerOptions = null) =>
        new PrimeClockIntervalTimerRegistration(this,
            callbackTime,
            repeat ? callbackTime : NoRepeatSentinel,
            IntervalTimerCallbackKind.SimpleAsync,
            callback,
            null,
            timerOptions,
            cancellationToken);

    /// <inheritdoc />
    public IClockIntervalTimer RegisterAsyncTimer (Duration callbackTime,
        Func<ClockTimerCallbackContext, CancellationToken, ValueTask> callback,
        CancellationToken cancellationToken,
        object? state = null,
        bool repeat = false,
        IntervalTimerOptions? timerOptions = null) =>
        new PrimeClockIntervalTimerRegistration(this,
            callbackTime,
            repeat ? callbackTime : NoRepeatSentinel,
            IntervalTimerCallbackKind.ContextAsync,
            callback,
            state,
            timerOptions,
            cancellationToken);

    /// <inheritdoc />
    public IClockIntervalTimer RegisterTimer (Duration callbackTime,
        Duration repeatInterval,
        Action callback,
        CancellationToken cancellationToken,
        IntervalTimerOptions? timerOptions = null) =>
        new PrimeClockIntervalTimerRegistration(this,
            callbackTime,
            repeatInterval,
            IntervalTimerCallbackKind.SimpleAction,
            callback,
            null,
            timerOptions,
            cancellationToken);

    /// <inheritdoc />
    public IClockIntervalTimer RegisterTimer (Duration callbackTime,
        Duration repeatInterval,
        Action<ClockTimerCallbackContext> callback,
        CancellationToken cancellationToken,
        object? state = null,
        IntervalTimerOptions? timerOptions = null) =>
        new PrimeClockIntervalTimerRegistration(this,
            callbackTime,
            repeatInterval,
            IntervalTimerCallbackKind.ContextAction,
            callback,
            state,
            timerOptions,
            cancellationToken);

    /// <inheritdoc />
    public IClockIntervalTimer RegisterTimer (Duration callbackTime,
        Duration repeatInterval,
        Action<ClockTimerCallbackContext, CancellationToken> callback,
        CancellationToken cancellationToken,
        object? state = null,
        IntervalTimerOptions? timerOptions = null) =>
        new PrimeClockIntervalTimerRegistration(this,
            callbackTime,
            repeatInterval,
            IntervalTimerCallbackKind.ContextActionWithToken,
            callback,
            state,
            timerOptions,
            cancellationToken);

    /// <inheritdoc />
    public IClockIntervalTimer RegisterAsyncTimer (Duration callbackTime,
        Duration repeatInterval,
        Func<CancellationToken, ValueTask> callback,
        CancellationToken cancellationToken,
        IntervalTimerOptions? timerOptions = null) =>
        new PrimeClockIntervalTimerRegistration(this,
            callbackTime,
            repeatInterval,
            IntervalTimerCallbackKind.SimpleAsync,
            callback,
            null,
            timerOptions,
            cancellationToken);

    /// <inheritdoc />
    public IClockIntervalTimer RegisterAsyncTimer (Duration callbackTime,
        Duration repeatInterval,
        Func<ClockTimerCallbackContext, CancellationToken, ValueTask> callback,
        CancellationToken cancellationToken,
        object? state = null,
        IntervalTimerOptions? timerOptions = null) =>
        new PrimeClockIntervalTimerRegistration(this,
            callbackTime,
            repeatInterval,
            IntervalTimerCallbackKind.ContextAsync,
            callback,
            state,
            timerOptions,
            cancellationToken);

    /// <inheritdoc />
    public IClockIntervalTimer RegisterTimer (TimeSpan callbackTime,
        Action callback,
        CancellationToken cancellationToken,
        bool repeat = false,
        IntervalTimerOptions? timerOptions = null) =>
        RegisterTimer(Duration.FromTimeSpan(callbackTime), callback, cancellationToken, repeat, timerOptions);

    /// <inheritdoc />
    public IClockIntervalTimer RegisterTimer (TimeSpan callbackTime,
        Action<ClockTimerCallbackContext> callback,
        CancellationToken cancellationToken,
        object? state = null,
        bool repeat = false,
        IntervalTimerOptions? timerOptions = null) =>
        RegisterTimer(Duration.FromTimeSpan(callbackTime), callback, cancellationToken, state, repeat, timerOptions);

    /// <inheritdoc />
    public IClockIntervalTimer RegisterTimer (TimeSpan callbackTime,
        Action<ClockTimerCallbackContext, CancellationToken> callback,
        CancellationToken cancellationToken,
        object? state = null,
        bool repeat = false,
        IntervalTimerOptions? timerOptions = null) =>
        RegisterTimer(Duration.FromTimeSpan(callbackTime), callback, cancellationToken, state, repeat, timerOptions);

    /// <inheritdoc />
    public IClockIntervalTimer RegisterAsyncTimer (TimeSpan callbackTime,
        Func<CancellationToken, ValueTask> callback,
        CancellationToken cancellationToken,
        bool repeat = false,
        IntervalTimerOptions? timerOptions = null) =>
        RegisterAsyncTimer(Duration.FromTimeSpan(callbackTime), callback, cancellationToken, repeat, timerOptions);

    /// <inheritdoc />
    public IClockIntervalTimer RegisterAsyncTimer (TimeSpan callbackTime,
        Func<ClockTimerCallbackContext, CancellationToken, ValueTask> callback,
        CancellationToken cancellationToken,
        object? state = null,
        bool repeat = false,
        IntervalTimerOptions? timerOptions = null) =>
        RegisterAsyncTimer(Duration.FromTimeSpan(callbackTime), callback, cancellationToken, state, repeat, timerOptions);

    /// <inheritdoc />
    public IClockIntervalTimer RegisterTimer (TimeSpan callbackTime,
        TimeSpan repeatInterval,
        Action callback,
        CancellationToken cancellationToken,
        IntervalTimerOptions? timerOptions = null) =>
        RegisterTimer(Duration.FromTimeSpan(callbackTime),
            Duration.FromTimeSpan(repeatInterval),
            callback,
            cancellationToken,
            timerOptions);

    /// <inheritdoc />
    public IClockIntervalTimer RegisterTimer (TimeSpan callbackTime,
        TimeSpan repeatInterval,
        Action<ClockTimerCallbackContext> callback,
        CancellationToken cancellationToken,
        object? state = null,
        IntervalTimerOptions? timerOptions = null) =>
        RegisterTimer(Duration.FromTimeSpan(callbackTime),
            Duration.FromTimeSpan(repeatInterval),
            callback,
            cancellationToken,
            state,
            timerOptions);

    /// <inheritdoc />
    public IClockIntervalTimer RegisterTimer (TimeSpan callbackTime,
        TimeSpan repeatInterval,
        Action<ClockTimerCallbackContext, CancellationToken> callback,
        CancellationToken cancellationToken,
        object? state = null,
        IntervalTimerOptions? timerOptions = null) =>
        RegisterTimer(Duration.FromTimeSpan(callbackTime),
            Duration.FromTimeSpan(repeatInterval),
            callback,
            cancellationToken,
            state,
            timerOptions);

    /// <inheritdoc />
    public IClockIntervalTimer RegisterAsyncTimer (TimeSpan callbackTime,
        TimeSpan repeatInterval,
        Func<CancellationToken, ValueTask> callback,
        CancellationToken cancellationToken,
        IntervalTimerOptions? timerOptions = null) =>
        RegisterAsyncTimer(Duration.FromTimeSpan(callbackTime),
            Duration.FromTimeSpan(repeatInterval),
            callback,
            cancellationToken,
            timerOptions);

    /// <inheritdoc />
    public IClockIntervalTimer RegisterAsyncTimer (TimeSpan callbackTime,
        TimeSpan repeatInterval,
        Func<ClockTimerCallbackContext, CancellationToken, ValueTask> callback,
        CancellationToken cancellationToken,
        object? state = null,
        IntervalTimerOptions? timerOptions = null) =>
        RegisterAsyncTimer(Duration.FromTimeSpan(callbackTime),
            Duration.FromTimeSpan(repeatInterval),
            callback,
            cancellationToken,
            state,
            timerOptions);

    #endregion IPrimeClock — Interval timers (RegisterTimer)

    #region IPrimeClock — Time-of-day timers (RegisterTimeOfDay)

#if NET
    /// <inheritdoc />
    public IClockDayTimeTimer RegisterTimeOfDay (LocalTime timeOfDay,
        Action callback,
        CancellationToken cancellationToken,
        DayTimeTimerOptions? timerOptions = null) =>
        new PrimeClockDayTimeTimerRegistration(this,
            timeOfDay,
            IntervalTimerCallbackKind.SimpleAction,
            callback,
            null,
            timerOptions,
            cancellationToken);

    /// <inheritdoc />
    public IClockDayTimeTimer RegisterTimeOfDay (LocalTime timeOfDay,
        Action<ClockTimerCallbackContext> callback,
        CancellationToken cancellationToken,
        object? state = null,
        DayTimeTimerOptions? timerOptions = null) =>
        new PrimeClockDayTimeTimerRegistration(this,
            timeOfDay,
            IntervalTimerCallbackKind.ContextAction,
            callback,
            state,
            timerOptions,
            cancellationToken);

    /// <inheritdoc />
    public IClockDayTimeTimer RegisterTimeOfDay (LocalTime timeOfDay,
        Action<ClockTimerCallbackContext, CancellationToken> callback,
        CancellationToken cancellationToken,
        object? state = null,
        DayTimeTimerOptions? timerOptions = null) =>
        new PrimeClockDayTimeTimerRegistration(this,
            timeOfDay,
            IntervalTimerCallbackKind.ContextActionWithToken,
            callback,
            state,
            timerOptions,
            cancellationToken);

    /// <inheritdoc />
    public IClockDayTimeTimer RegisterAsyncTimeOfDay (LocalTime timeOfDay,
        Func<CancellationToken, ValueTask> callback,
        CancellationToken cancellationToken,
        DayTimeTimerOptions? timerOptions = null) =>
        new PrimeClockDayTimeTimerRegistration(this,
            timeOfDay,
            IntervalTimerCallbackKind.SimpleAsync,
            callback,
            null,
            timerOptions,
            cancellationToken);

    /// <inheritdoc />
    public IClockDayTimeTimer RegisterAsyncTimeOfDay (LocalTime timeOfDay,
        Func<ClockTimerCallbackContext, CancellationToken, ValueTask> callback,
        CancellationToken cancellationToken,
        object? state = null,
        DayTimeTimerOptions? timerOptions = null) =>
        new PrimeClockDayTimeTimerRegistration(this,
            timeOfDay,
            IntervalTimerCallbackKind.ContextAsync,
            callback,
            state,
            timerOptions,
            cancellationToken);

    /// <inheritdoc />
    public IClockDayTimeTimer RegisterTimeOfDay (LocalTimeOfDay timeOfDay,
        Action callback,
        CancellationToken cancellationToken,
        DayTimeTimerOptions? timerOptions = null) =>
        RegisterTimeOfDay(TimeOnlyToLocalTime(timeOfDay.Value), callback, cancellationToken, timerOptions);

    /// <inheritdoc />
    public IClockDayTimeTimer RegisterTimeOfDay (LocalTimeOfDay timeOfDay,
        Action<ClockTimerCallbackContext> callback,
        CancellationToken cancellationToken,
        object? state = null,
        DayTimeTimerOptions? timerOptions = null) =>
        RegisterTimeOfDay(TimeOnlyToLocalTime(timeOfDay.Value), callback, cancellationToken, state, timerOptions);

    /// <inheritdoc />
    public IClockDayTimeTimer RegisterTimeOfDay (LocalTimeOfDay timeOfDay,
        Action<ClockTimerCallbackContext, CancellationToken> callback,
        CancellationToken cancellationToken,
        object? state = null,
        DayTimeTimerOptions? timerOptions = null) =>
        RegisterTimeOfDay(TimeOnlyToLocalTime(timeOfDay.Value), callback, cancellationToken, state, timerOptions);

    /// <inheritdoc />
    public IClockDayTimeTimer RegisterAsyncTimeOfDay (LocalTimeOfDay timeOfDay,
        Func<CancellationToken, ValueTask> callback,
        CancellationToken cancellationToken,
        DayTimeTimerOptions? timerOptions = null) =>
        RegisterAsyncTimeOfDay(TimeOnlyToLocalTime(timeOfDay.Value), callback, cancellationToken, timerOptions);

    /// <inheritdoc />
    public IClockDayTimeTimer RegisterAsyncTimeOfDay (LocalTimeOfDay timeOfDay,
        Func<ClockTimerCallbackContext, CancellationToken, ValueTask> callback,
        CancellationToken cancellationToken,
        object? state = null,
        DayTimeTimerOptions? timerOptions = null) =>
        RegisterAsyncTimeOfDay(TimeOnlyToLocalTime(timeOfDay.Value), callback, cancellationToken, state, timerOptions);

    /// <inheritdoc />
    public IClockDayTimeTimer RegisterTimeOfDay (UtcTimeOfDay timeOfDay,
        Action callback,
        CancellationToken cancellationToken,
        DayTimeTimerOptions? timerOptions = null) =>
        new PrimeClockDayTimeTimerRegistration(this,
            TimeOnlyToLocalTime(timeOfDay.Value),
            IntervalTimerCallbackKind.SimpleAction,
            callback,
            null,
            timerOptions,
            cancellationToken,
            utcTimeOfDaySchedule: true);

    /// <inheritdoc />
    public IClockDayTimeTimer RegisterTimeOfDay (UtcTimeOfDay timeOfDay,
        Action<ClockTimerCallbackContext> callback,
        CancellationToken cancellationToken,
        object? state = null,
        DayTimeTimerOptions? timerOptions = null) =>
        new PrimeClockDayTimeTimerRegistration(this,
            TimeOnlyToLocalTime(timeOfDay.Value),
            IntervalTimerCallbackKind.ContextAction,
            callback,
            state,
            timerOptions,
            cancellationToken,
            utcTimeOfDaySchedule: true);

    /// <inheritdoc />
    public IClockDayTimeTimer RegisterTimeOfDay (UtcTimeOfDay timeOfDay,
        Action<ClockTimerCallbackContext, CancellationToken> callback,
        CancellationToken cancellationToken,
        object? state = null,
        DayTimeTimerOptions? timerOptions = null) =>
        new PrimeClockDayTimeTimerRegistration(this,
            TimeOnlyToLocalTime(timeOfDay.Value),
            IntervalTimerCallbackKind.ContextActionWithToken,
            callback,
            state,
            timerOptions,
            cancellationToken,
            utcTimeOfDaySchedule: true);

    /// <inheritdoc />
    public IClockDayTimeTimer RegisterAsyncTimeOfDay (UtcTimeOfDay timeOfDay,
        Func<CancellationToken, ValueTask> callback,
        CancellationToken cancellationToken,
        DayTimeTimerOptions? timerOptions = null) =>
        new PrimeClockDayTimeTimerRegistration(this,
            TimeOnlyToLocalTime(timeOfDay.Value),
            IntervalTimerCallbackKind.SimpleAsync,
            callback,
            null,
            timerOptions,
            cancellationToken,
            utcTimeOfDaySchedule: true);

    /// <inheritdoc />
    public IClockDayTimeTimer RegisterAsyncTimeOfDay (UtcTimeOfDay timeOfDay,
        Func<ClockTimerCallbackContext, CancellationToken, ValueTask> callback,
        CancellationToken cancellationToken,
        object? state = null,
        DayTimeTimerOptions? timerOptions = null) =>
        new PrimeClockDayTimeTimerRegistration(this,
            TimeOnlyToLocalTime(timeOfDay.Value),
            IntervalTimerCallbackKind.ContextAsync,
            callback,
            state,
            timerOptions,
            cancellationToken,
            utcTimeOfDaySchedule: true);
#else
    /// <inheritdoc />
    public IClockDayTimeTimer RegisterTimeOfDay (LocalTime timeOfDay,
        Action callback,
        CancellationToken cancellationToken,
        DayTimeTimerOptions? timerOptions = null) =>
        new PrimeClockDayTimeTimerRegistration(this,
            timeOfDay,
            IntervalTimerCallbackKind.SimpleAction,
            callback,
            null,
            timerOptions,
            cancellationToken);

    /// <inheritdoc />
    public IClockDayTimeTimer RegisterTimeOfDay (LocalTime timeOfDay,
        Action<ClockTimerCallbackContext> callback,
        CancellationToken cancellationToken,
        object? state = null,
        DayTimeTimerOptions? timerOptions = null) =>
        new PrimeClockDayTimeTimerRegistration(this,
            timeOfDay,
            IntervalTimerCallbackKind.ContextAction,
            callback,
            state,
            timerOptions,
            cancellationToken);

    /// <inheritdoc />
    public IClockDayTimeTimer RegisterTimeOfDay (LocalTime timeOfDay,
        Action<ClockTimerCallbackContext, CancellationToken> callback,
        CancellationToken cancellationToken,
        object? state = null,
        DayTimeTimerOptions? timerOptions = null) =>
        new PrimeClockDayTimeTimerRegistration(this,
            timeOfDay,
            IntervalTimerCallbackKind.ContextActionWithToken,
            callback,
            state,
            timerOptions,
            cancellationToken);

    /// <inheritdoc />
    public IClockDayTimeTimer RegisterAsyncTimeOfDay (LocalTime timeOfDay,
        Func<CancellationToken, ValueTask> callback,
        CancellationToken cancellationToken,
        DayTimeTimerOptions? timerOptions = null) =>
        new PrimeClockDayTimeTimerRegistration(this,
            timeOfDay,
            IntervalTimerCallbackKind.SimpleAsync,
            callback,
            null,
            timerOptions,
            cancellationToken);

    /// <inheritdoc />
    public IClockDayTimeTimer RegisterAsyncTimeOfDay (LocalTime timeOfDay,
        Func<ClockTimerCallbackContext, CancellationToken, ValueTask> callback,
        CancellationToken cancellationToken,
        object? state = null,
        DayTimeTimerOptions? timerOptions = null) =>
        new PrimeClockDayTimeTimerRegistration(this,
            timeOfDay,
            IntervalTimerCallbackKind.ContextAsync,
            callback,
            state,
            timerOptions,
            cancellationToken);
#endif

    #endregion IPrimeClock — Time-of-day timers (RegisterTimeOfDay)

    #endregion Interface Implementations
}
//################################################################################

