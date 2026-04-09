// Copyright (c) Kevin Zehrer
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Diagnostics;
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
#if NET
    //----------------------------------------------------------------------------
    /// <summary>
    ///   Converts a <see cref="TimeOnly"/> wall-clock value to <see cref="LocalTime"/> for Noda day-time scheduling,
    ///   preserving full <see cref="TimeOnly.Ticks"/> resolution (100 nanoseconds per tick).
    /// </summary>
    /// <param name="timeOnly">The wall-clock time of day.</param>
    /// <returns>
    ///   The equivalent Noda <see cref="LocalTime"/> from <see cref="LocalTime.FromTicksSinceMidnight"/>.
    /// </returns>
    private static LocalTime TimeOnlyToLocalTime (TimeOnly timeOnly) =>
        LocalTime.FromTicksSinceMidnight(timeOnly.Ticks);
    //----------------------------------------------------------------------------

#endif

    //----------------------------------------------------------------------------
    /// <summary>
    ///   The NodaTime clock that supplies the current instant.
    /// </summary>
    private readonly IClock _clock;

    /// <summary>
    ///   The time zone used for local zoned date and time projections.
    /// </summary>
    private readonly DateTimeZone _systemDefaultZone;
    //----------------------------------------------------------------------------

    #region Constructors/Finalizers

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
    internal PrimeClock (IClock clock, DateTimeZone systemDefaultZone)
    {
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _systemDefaultZone = systemDefaultZone ?? throw new ArgumentNullException(nameof(systemDefaultZone));
    }
    //----------------------------------------------------------------------------
    /// <summary>
    ///   Initializes a new instance of the <see cref="PrimeClock"/> class using
    ///   <see cref="SystemClock.Instance"/> and the BCL system default time zone.
    /// </summary>
    internal PrimeClock ()
        : this(SystemClock.Instance)
    {
    }
    //----------------------------------------------------------------------------
    /// <summary>
    ///   Initializes a new instance of the <see cref="PrimeClock"/> class with the
    ///   specified clock and system default time zone.
    /// </summary>
    /// <param name="clock">
    ///   The NodaTime clock used to obtain the current instant.
    /// </param>
    /// <exception cref="ArgumentNullException">
    ///   <paramref name="clock"/> is <c>null</c>.
    /// </exception>
    public PrimeClock (IClock clock) :
            this(clock, GetSystemDefaultTimeZone())
    {
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
    public Instant NowInstant { [DebuggerStepThrough] get => _clock.GetCurrentInstant(); }
    //----------------------------------------------------------------------------

    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public ZonedDateTime UtcNowInstant { [DebuggerStepThrough] get => _clock.GetCurrentInstant().InUtc(); }
    //----------------------------------------------------------------------------

    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public ZonedDateTime LocalZonedNowInstant { [DebuggerStepThrough] get => _clock.GetCurrentInstant().InZone(_systemDefaultZone); }
    //----------------------------------------------------------------------------

    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public ZonedDateTime UtcZonedNowInstant { [DebuggerStepThrough] get => UtcNowInstant; }
    //----------------------------------------------------------------------------

    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public LocalDateTime LocalNowInstant { [DebuggerStepThrough] get => LocalZonedNowInstant.LocalDateTime; }
    //----------------------------------------------------------------------------

    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public LocalTime LocalNowTime { [DebuggerStepThrough] get => LocalZonedNowInstant.TimeOfDay; }
    //----------------------------------------------------------------------------

    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public LocalTime UtcNowTime { [DebuggerStepThrough] get => UtcNowInstant.TimeOfDay; }
    //----------------------------------------------------------------------------

    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public LocalDate LocalNowDate { [DebuggerStepThrough] get => LocalZonedNowInstant.Date; }
    //----------------------------------------------------------------------------

    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public LocalDate UtcNowDate { [DebuggerStepThrough] get => UtcNowInstant.Date; }
    //----------------------------------------------------------------------------

    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public DateTimeOffset LocalNowDateTimeOffset { [DebuggerStepThrough] get => LocalZonedNowInstant.ToDateTimeOffset(); }
    //----------------------------------------------------------------------------

    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public DateTimeOffset UtcNowDateTimeOffset { [DebuggerStepThrough] get => UtcNowInstant.ToDateTimeOffset(); }
    //----------------------------------------------------------------------------

    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public DateTime LocalNowDateTime { [DebuggerStepThrough] get => LocalNowDateTimeOffset.LocalDateTime; }
    //----------------------------------------------------------------------------

    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public DateTime UtcNowDateTime { [DebuggerStepThrough] get => UtcNowDateTimeOffset.UtcDateTime; }
    //----------------------------------------------------------------------------

#if NET
    /// <inheritdoc />
    public TimeOnly LocalNowTimeOnly { [DebuggerStepThrough] get => TimeOnly.FromDateTime(LocalNowDateTime); }

    /// <inheritdoc />
    public TimeOnly UtcNowTimeOnly { [DebuggerStepThrough] get => TimeOnly.FromDateTime(UtcNowDateTime); }

    /// <inheritdoc />
    public DateOnly LocalNowDateOnly { [DebuggerStepThrough] get => DateOnly.FromDateTime(LocalNowDateTime); }

    /// <inheritdoc />
    public DateOnly UtcNowDateOnly { [DebuggerStepThrough] get => DateOnly.FromDateTime(UtcNowDateTime); }
#endif

    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public TimeZoneInfo LocalScheduleTimeZone { [DebuggerStepThrough] get =>
        NodaDateTimeZoneBclInterop.GetLocalScheduleTimeZoneInfo(_systemDefaultZone); }
    //----------------------------------------------------------------------------

    #endregion IPrimeClock Implementation

    #region IPrimeClock — Interval timers (RegisterTimer)

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
        Action<ClockTimerCallbackContext> callback,
        CancellationToken cancellationToken,
        object? state = null,
        DayTimeTimerOptions? timerOptions = null) =>
        RegisterTimeOfDay(TimeOnlyToLocalTime(timeOfDay.Value), callback, cancellationToken, state, timerOptions);

    /// <inheritdoc />
    public IClockDayTimeTimer RegisterAsyncTimeOfDay (LocalTimeOfDay timeOfDay,
        Func<ClockTimerCallbackContext, CancellationToken, ValueTask> callback,
        CancellationToken cancellationToken,
        object? state = null,
        DayTimeTimerOptions? timerOptions = null) =>
        RegisterAsyncTimeOfDay(TimeOnlyToLocalTime(timeOfDay.Value), callback, cancellationToken, state, timerOptions);

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

#endif

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

    #endregion IPrimeClock — Time-of-day timers (RegisterTimeOfDay)

    #endregion Interface Implementations
}
//################################################################################

