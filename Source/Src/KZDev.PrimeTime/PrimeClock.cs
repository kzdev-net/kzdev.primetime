// Copyright (c) Kevin Zehrer. All rights reserved.
// This file is part of the PrimeTime project.

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
internal sealed partial class PrimeClock
{
    //----------------------------------------------------------------------------
    /// <summary>
    ///   Sentinel <see cref="Duration"/> matching <see cref="Timeout.InfiniteTimeSpan"/> for one-shot (non-repeating) timers.
    /// </summary>
    private static readonly Duration NoRepeatSentinel = Duration.FromTimeSpan(Timeout.InfiniteTimeSpan);

    /// <summary>
    ///   The NodaTime clock that supplies the current instant.
    /// </summary>
    private readonly IClock _clock;

    /// <summary>
    ///   The time zone used for local zoned date and time projections.
    /// </summary>
    private readonly DateTimeZone _systemDefaultZone;
    //----------------------------------------------------------------------------

#if NET
    //----------------------------------------------------------------------------
    /// <summary>
    ///   Converts a <see cref="TimeOnly"/> wall-clock value to <see cref="LocalTime"/> for Noda day-time scheduling,
    ///   preserving full <see cref="TimeOnly.Ticks"/> resolution (100 nanoseconds per tick).
    /// </summary>
    /// <param name="t">The wall-clock time of day.</param>
    /// <returns>
    ///   The equivalent Noda <see cref="LocalTime"/> from <see cref="LocalTime.FromTicksSinceMidnight"/>.
    /// </returns>
    private static LocalTime TimeOnlyToLocalTime (TimeOnly t) =>
        LocalTime.FromTicksSinceMidnight(t.Ticks);
    //----------------------------------------------------------------------------

#endif

    #region Constructors/Finalizers

    //----------------------------------------------------------------------------
    /// <summary>
    ///   Initializes a new instance of the <see cref="PrimeClock"/> class using
    ///   <see cref="SystemClock.Instance"/> and the BCL system default time zone.
    /// </summary>
    public PrimeClock ()
        : this(SystemClock.Instance, GetSystemDefaultTimeZone())
    {
    }
    //----------------------------------------------------------------------------

    //----------------------------------------------------------------------------
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
    /// <exception cref="ArgumentNullException">
    ///   <paramref name="clock"/> or <paramref name="systemDefaultZone"/> is <c>null</c>.
    /// </exception>
    public PrimeClock (IClock clock, DateTimeZone systemDefaultZone)
    {
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _systemDefaultZone = systemDefaultZone ?? throw new ArgumentNullException(nameof(systemDefaultZone));
    }
    //----------------------------------------------------------------------------

    #endregion Constructors/Finalizers

    //----------------------------------------------------------------------------
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
    //----------------------------------------------------------------------------

    #region Interface Implementations

    #region IPrimeClock Implementation

    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public Instant NowInstant => _clock.GetCurrentInstant();
    //----------------------------------------------------------------------------

    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public ZonedDateTime UtcNow => _clock.GetCurrentInstant().InUtc();
    //----------------------------------------------------------------------------

    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public ZonedDateTime LocalZonedNow => _clock.GetCurrentInstant().InZone(_systemDefaultZone);
    //----------------------------------------------------------------------------

    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public ZonedDateTime UtcZonedNow => UtcNow;
    //----------------------------------------------------------------------------

    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public LocalDateTime LocalNow => LocalZonedNow.LocalDateTime;
    //----------------------------------------------------------------------------

    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public LocalTime LocalNowTime => LocalZonedNow.TimeOfDay;
    //----------------------------------------------------------------------------

    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public LocalTime UtcNowTime => UtcNow.TimeOfDay;
    //----------------------------------------------------------------------------

    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public LocalDate LocalNowDate => LocalZonedNow.Date;
    //----------------------------------------------------------------------------

    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public LocalDate UtcNowDate => UtcNow.Date;
    //----------------------------------------------------------------------------

    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public DateTimeOffset LocalNowOffset => LocalZonedNow.ToDateTimeOffset();
    //----------------------------------------------------------------------------

    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public DateTimeOffset UtcNowOffset => UtcNow.ToDateTimeOffset();
    //----------------------------------------------------------------------------

    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public DateTime LocalNowDateTime => LocalNowOffset.LocalDateTime;
    //----------------------------------------------------------------------------

    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public DateTime UtcNowDateTime => UtcNowOffset.UtcDateTime;
    //----------------------------------------------------------------------------

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

    //----------------------------------------------------------------------------
    /// <inheritdoc />
    /// <remarks>
    ///   Durations greater than <see cref="TimeSpan.MaxValue"/> are automatically clamped
    ///   to <see cref="TimeSpan.MaxValue"/> before the underlying BCL delay call. In that
    ///   case, <see cref="Thread.Sleep(TimeSpan)"/> blocks for approximately 29,000 years,
    ///   so passing unreasonably large durations is still undesirable despite the clamping.
    /// </remarks>
    public void Sleep (Duration duration)
    {
        TimeSpan ts = NodaDurationBclConversions.ToTimeSpanForDelay(duration);
        Thread.Sleep(ts);
    }
    //----------------------------------------------------------------------------

    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public Task DelayAsync (Duration duration)
    {
        TimeSpan ts = NodaDurationBclConversions.ToTimeSpanForDelay(duration);
        return Task.Delay(ts);
    }
    //----------------------------------------------------------------------------

    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public Task DelayAsync (Duration duration, CancellationToken cancellationToken)
    {
        TimeSpan ts = NodaDurationBclConversions.ToTimeSpanForDelay(duration);
        return Task.Delay(ts, cancellationToken);
    }
    //----------------------------------------------------------------------------

    #endregion IPrimeClock — Delays (Duration)

    #region IPrimeClock — Time cancellation (Duration)

    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public TimeCancellationTokenSource GetTimeCancellationToken (Duration cancelAfter)
    {
        TimeSpan ts = NodaDurationBclConversions.ToTimeSpanForCancellationToken(cancelAfter);
        CancellationTokenSource cts = new(ts);
        return new TimeCancellationTokenSource(cts);
    }
    //----------------------------------------------------------------------------

    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public TimeCancellationTokenSource LinkTimeCancellationToken (Duration cancelAfter, CancellationToken cancellationToken)
    {
        TimeSpan ts = NodaDurationBclConversions.ToTimeSpanForCancellationToken(cancelAfter);
        CancellationTokenSource timeCts = new(ts);
        CancellationTokenSource linkedCts =
            CancellationTokenSource.CreateLinkedTokenSource(timeCts.Token, cancellationToken);
        return new TimeCancellationTokenSource(linkedCts, [timeCts]);
    }
    //----------------------------------------------------------------------------

    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public TimeCancellationTokenSource LinkTimeCancellationToken (Duration cancelAfter, CancellationToken token1,
        CancellationToken token2)
    {
        TimeSpan ts = NodaDurationBclConversions.ToTimeSpanForCancellationToken(cancelAfter);
        CancellationTokenSource timeCts = new(ts);
        CancellationTokenSource linkedCts =
            CancellationTokenSource.CreateLinkedTokenSource(timeCts.Token, token1, token2);
        return new TimeCancellationTokenSource(linkedCts, [timeCts]);
    }
    //----------------------------------------------------------------------------

    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public TimeCancellationTokenSource LinkTimeCancellationToken (Duration cancelAfter,
        params CancellationToken[] cancellationTokens)
    {
        TimeSpan ts = NodaDurationBclConversions.ToTimeSpanForCancellationToken(cancelAfter);
        CancellationTokenSource timeCts = new(ts);
        CancellationTokenSource linkedCts =
            CancellationTokenSource.CreateLinkedTokenSource([timeCts.Token, .. cancellationTokens]);
        return new TimeCancellationTokenSource(linkedCts, [timeCts]);
    }
    //----------------------------------------------------------------------------

    #endregion IPrimeClock — Time cancellation (Duration)

    #region IPrimeClock — Interval timers (RegisterTimer)

    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public IClockIntervalTimer RegisterTimer (Duration callbackTime,
        Action callback,
        CancellationToken cancellationToken,
        bool repeat = false,
        IntervalTimerOptions? timerOptions = null)
    {
        TimeSpan callbackPeriod = NodaDurationBclConversions.ToTimeSpanForTimerInterval(callbackTime);
        return new ClockIntervalTimerRegistration(this,
            callbackPeriod,
            repeat ? callbackPeriod : NoRepeatSentinel.ToTimeSpan(),
            IntervalTimerCallbackKind.SimpleAction,
            callback,
            null,
            timerOptions,
            cancellationToken);
    }
    //----------------------------------------------------------------------------

    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public IClockIntervalTimer RegisterTimer (Duration callbackTime,
        Action<ClockTimerCallbackContext> callback,
        CancellationToken cancellationToken,
        object? state = null,
        bool repeat = false,
        IntervalTimerOptions? timerOptions = null)
    {
        TimeSpan callbackPeriod = NodaDurationBclConversions.ToTimeSpanForTimerInterval(callbackTime);
        return new ClockIntervalTimerRegistration(this,
            callbackPeriod,
            repeat ? callbackPeriod : NoRepeatSentinel.ToTimeSpan(),
            IntervalTimerCallbackKind.ContextAction,
            callback,
            state,
            timerOptions,
            cancellationToken);
    }
    //----------------------------------------------------------------------------

    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public IClockIntervalTimer RegisterTimer (Duration callbackTime,
        Action<ClockTimerCallbackContext, CancellationToken> callback,
        CancellationToken cancellationToken,
        object? state = null,
        bool repeat = false,
        IntervalTimerOptions? timerOptions = null) =>
        new ClockIntervalTimerRegistration(this,
            NodaDurationBclConversions.ToTimeSpanForTimerInterval(callbackTime),
            NodaDurationBclConversions.ToTimeSpanForTimerInterval(repeat ? callbackTime : NoRepeatSentinel),
            IntervalTimerCallbackKind.ContextActionWithToken,
            callback,
            state,
            timerOptions,
            cancellationToken);
    //----------------------------------------------------------------------------

    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public IClockIntervalTimer RegisterAsyncTimer (Duration callbackTime,
        Func<CancellationToken, ValueTask> callback,
        CancellationToken cancellationToken,
        bool repeat = false,
        IntervalTimerOptions? timerOptions = null)
    {
        TimeSpan callbackPeriod = NodaDurationBclConversions.ToTimeSpanForTimerInterval(callbackTime);
        return new ClockIntervalTimerRegistration(this,
            callbackPeriod,
            repeat ? callbackPeriod : NoRepeatSentinel.ToTimeSpan(),
            IntervalTimerCallbackKind.SimpleAsync,
            callback,
            null,
            timerOptions,
            cancellationToken);
    }
    //----------------------------------------------------------------------------

    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public IClockIntervalTimer RegisterAsyncTimer (Duration callbackTime,
        Func<ClockTimerCallbackContext, CancellationToken, ValueTask> callback,
        CancellationToken cancellationToken,
        object? state = null,
        bool repeat = false,
        IntervalTimerOptions? timerOptions = null)
    {
        TimeSpan callbackPeriod = NodaDurationBclConversions.ToTimeSpanForTimerInterval(callbackTime);
        return new ClockIntervalTimerRegistration(this,
            callbackPeriod,
            repeat ? callbackPeriod : NoRepeatSentinel.ToTimeSpan(),
            IntervalTimerCallbackKind.ContextAsync,
            callback,
            state,
            timerOptions,
            cancellationToken);
    }
    //----------------------------------------------------------------------------

    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public IClockIntervalTimer RegisterTimer (Duration callbackTime,
        Duration repeatInterval,
        Action callback,
        CancellationToken cancellationToken,
        IntervalTimerOptions? timerOptions = null) =>
        new ClockIntervalTimerRegistration(this,
            NodaDurationBclConversions.ToTimeSpanForTimerInterval(callbackTime),
            NodaDurationBclConversions.ToTimeSpanForTimerInterval(repeatInterval),
            IntervalTimerCallbackKind.SimpleAction,
            callback,
            null,
            timerOptions,
            cancellationToken);
    //----------------------------------------------------------------------------

    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public IClockIntervalTimer RegisterTimer (Duration callbackTime,
        Duration repeatInterval,
        Action<ClockTimerCallbackContext> callback,
        CancellationToken cancellationToken,
        object? state = null,
        IntervalTimerOptions? timerOptions = null) =>
        new ClockIntervalTimerRegistration(this,
            NodaDurationBclConversions.ToTimeSpanForTimerInterval(callbackTime),
            NodaDurationBclConversions.ToTimeSpanForTimerInterval(repeatInterval),
            IntervalTimerCallbackKind.ContextAction,
            callback,
            state,
            timerOptions,
            cancellationToken);
    //----------------------------------------------------------------------------

    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public IClockIntervalTimer RegisterTimer (Duration callbackTime,
        Duration repeatInterval,
        Action<ClockTimerCallbackContext, CancellationToken> callback,
        CancellationToken cancellationToken,
        object? state = null,
        IntervalTimerOptions? timerOptions = null) =>
        new ClockIntervalTimerRegistration(this,
            NodaDurationBclConversions.ToTimeSpanForTimerInterval(callbackTime),
            NodaDurationBclConversions.ToTimeSpanForTimerInterval(repeatInterval),
            IntervalTimerCallbackKind.ContextActionWithToken,
            callback,
            state,
            timerOptions,
            cancellationToken);
    //----------------------------------------------------------------------------

    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public IClockIntervalTimer RegisterAsyncTimer (Duration callbackTime,
        Duration repeatInterval,
        Func<CancellationToken, ValueTask> callback,
        CancellationToken cancellationToken,
        IntervalTimerOptions? timerOptions = null) =>
        new ClockIntervalTimerRegistration(this,
            NodaDurationBclConversions.ToTimeSpanForTimerInterval(callbackTime),
            NodaDurationBclConversions.ToTimeSpanForTimerInterval(repeatInterval),
            IntervalTimerCallbackKind.SimpleAsync,
            callback,
            null,
            timerOptions,
            cancellationToken);
    //----------------------------------------------------------------------------

    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public IClockIntervalTimer RegisterAsyncTimer (Duration callbackTime,
        Duration repeatInterval,
        Func<ClockTimerCallbackContext, CancellationToken, ValueTask> callback,
        CancellationToken cancellationToken,
        object? state = null,
        IntervalTimerOptions? timerOptions = null) =>
        new ClockIntervalTimerRegistration(this,
            NodaDurationBclConversions.ToTimeSpanForTimerInterval(callbackTime),
            NodaDurationBclConversions.ToTimeSpanForTimerInterval(repeatInterval),
            IntervalTimerCallbackKind.ContextAsync,
            callback,
            state,
            timerOptions,
            cancellationToken);
    //----------------------------------------------------------------------------

    #endregion IPrimeClock — Interval timers (RegisterTimer)

    #region IPrimeClock — Time-of-day timers (RegisterTimeOfDay)

#if NET
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
        new ClockDayTimeTimerRegistration(this,
            true,
            TimeOnlyToLocalTime(timeOfDay.Value),
            IntervalTimerCallbackKind.SimpleAction,
            callback,
            null,
            timerOptions,
            cancellationToken);

    /// <inheritdoc />
    public IClockDayTimeTimer RegisterTimeOfDay (UtcTimeOfDay timeOfDay,
        Action<ClockTimerCallbackContext> callback,
        CancellationToken cancellationToken,
        object? state = null,
        DayTimeTimerOptions? timerOptions = null) =>
        new ClockDayTimeTimerRegistration(this,
            true,
            TimeOnlyToLocalTime(timeOfDay.Value),
            IntervalTimerCallbackKind.ContextAction,
            callback,
            state,
            timerOptions,
            cancellationToken);

    /// <inheritdoc />
    public IClockDayTimeTimer RegisterTimeOfDay (UtcTimeOfDay timeOfDay,
        Action<ClockTimerCallbackContext, CancellationToken> callback,
        CancellationToken cancellationToken,
        object? state = null,
        DayTimeTimerOptions? timerOptions = null) =>
        new ClockDayTimeTimerRegistration(this,
            true,
            TimeOnlyToLocalTime(timeOfDay.Value),
            IntervalTimerCallbackKind.ContextActionWithToken,
            callback,
            state,
            timerOptions,
            cancellationToken);

    /// <inheritdoc />
    public IClockDayTimeTimer RegisterAsyncTimeOfDay (UtcTimeOfDay timeOfDay,
        Func<CancellationToken, ValueTask> callback,
        CancellationToken cancellationToken,
        DayTimeTimerOptions? timerOptions = null) =>
        new ClockDayTimeTimerRegistration(this,
            true,
            TimeOnlyToLocalTime(timeOfDay.Value),
            IntervalTimerCallbackKind.SimpleAsync,
            callback,
            null,
            timerOptions,
            cancellationToken);

    /// <inheritdoc />
    public IClockDayTimeTimer RegisterAsyncTimeOfDay (UtcTimeOfDay timeOfDay,
        Func<ClockTimerCallbackContext, CancellationToken, ValueTask> callback,
        CancellationToken cancellationToken,
        object? state = null,
        DayTimeTimerOptions? timerOptions = null) =>
        new ClockDayTimeTimerRegistration(this,
            true,
            TimeOnlyToLocalTime(timeOfDay.Value),
            IntervalTimerCallbackKind.ContextAsync,
            callback,
            state,
            timerOptions,
            cancellationToken);

    /// <inheritdoc />
    public IClockDayTimeTimer RegisterTimeOfDay (LocalTime timeOfDay,
        Action callback,
        CancellationToken cancellationToken,
        DayTimeTimerOptions? timerOptions = null) =>
        new ClockDayTimeTimerRegistration(this,
            false,
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
        new ClockDayTimeTimerRegistration(this,
            false,
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
        new ClockDayTimeTimerRegistration(this,
            false,
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
        new ClockDayTimeTimerRegistration(this,
            false,
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
        new ClockDayTimeTimerRegistration(this,
            false,
            timeOfDay,
            IntervalTimerCallbackKind.ContextAsync,
            callback,
            state,
            timerOptions,
            cancellationToken);
#else
    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public IClockDayTimeTimer RegisterTimeOfDay (LocalTime timeOfDay,
        Action callback,
        CancellationToken cancellationToken,
        DayTimeTimerOptions? timerOptions = null) =>
        new ClockDayTimeTimerRegistration(this,
            false,
            timeOfDay,
            IntervalTimerCallbackKind.SimpleAction,
            callback,
            null,
            timerOptions,
            cancellationToken);
    //----------------------------------------------------------------------------

    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public IClockDayTimeTimer RegisterTimeOfDay (LocalTime timeOfDay,
        Action<ClockTimerCallbackContext> callback,
        CancellationToken cancellationToken,
        object? state = null,
        DayTimeTimerOptions? timerOptions = null) =>
        new ClockDayTimeTimerRegistration(this,
            false,
            timeOfDay,
            IntervalTimerCallbackKind.ContextAction,
            callback,
            state,
            timerOptions,
            cancellationToken);
    //----------------------------------------------------------------------------

    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public IClockDayTimeTimer RegisterTimeOfDay (LocalTime timeOfDay,
        Action<ClockTimerCallbackContext, CancellationToken> callback,
        CancellationToken cancellationToken,
        object? state = null,
        DayTimeTimerOptions? timerOptions = null) =>
        new ClockDayTimeTimerRegistration(this,
            false,
            timeOfDay,
            IntervalTimerCallbackKind.ContextActionWithToken,
            callback,
            state,
            timerOptions,
            cancellationToken);
    //----------------------------------------------------------------------------

    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public IClockDayTimeTimer RegisterAsyncTimeOfDay (LocalTime timeOfDay,
        Func<CancellationToken, ValueTask> callback,
        CancellationToken cancellationToken,
        DayTimeTimerOptions? timerOptions = null) =>
        new ClockDayTimeTimerRegistration(this,
            false,
            timeOfDay,
            IntervalTimerCallbackKind.SimpleAsync,
            callback,
            null,
            timerOptions,
            cancellationToken);
    //----------------------------------------------------------------------------

    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public IClockDayTimeTimer RegisterAsyncTimeOfDay (LocalTime timeOfDay,
        Func<ClockTimerCallbackContext, CancellationToken, ValueTask> callback,
        CancellationToken cancellationToken,
        object? state = null,
        DayTimeTimerOptions? timerOptions = null) =>
        new ClockDayTimeTimerRegistration(this,
            false,
            timeOfDay,
            IntervalTimerCallbackKind.ContextAsync,
            callback,
            state,
            timerOptions,
            cancellationToken);
    //----------------------------------------------------------------------------
#endif

    #endregion IPrimeClock — Time-of-day timers (RegisterTimeOfDay)

    #endregion Interface Implementations
}
//################################################################################

