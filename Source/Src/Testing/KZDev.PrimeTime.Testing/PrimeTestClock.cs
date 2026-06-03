// Copyright (c) Kevin Zehrer
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Diagnostics;

using NodaTime;

namespace KZDev.PrimeTime.Testing;

//################################################################################
/// <summary>
///   NodaTime partial of <see cref="PrimeTestClock"/>: <see cref="Instant"/> storage, zone-aware "now" surfaces,
///   and <see cref="Duration"/> overloads that forward to the shared march and runner implementation.
/// </summary>
/// <remarks>
///   <para>
///     <see cref="SetInstant"/>, <see cref="SetLocalTime"/>, and Noda "now" members share semantics with the BCL
///     partial documented on <see cref="IPrimeTestClock"/>. Use a <see cref="DateTimeZone"/> constructor when tests
///     need explicit zone rules (for example daylight saving).
///   </para>
/// </remarks>
public sealed partial class PrimeTestClock
{
    #region Constructors/Finalizers

    //----------------------------------------------------------------------------
    /// <summary>
    ///   Initializes a new instance with virtual time set to the current system instant.
    /// </summary>
    public PrimeTestClock ()
    {
    }
    //----------------------------------------------------------------------------
    /// <summary>
    ///   Initializes a new instance with the specified initial instant and system default zone.
    /// </summary>
    /// <param name="initialInstant">The initial virtual instant.</param>
    public PrimeTestClock (Instant initialInstant) : base(initialInstant)
    {
    }
    //----------------------------------------------------------------------------
    /// <summary>
    ///   Initializes a new instance with the specified initial instant and time zone.
    /// </summary>
    /// <param name="initialInstant">The initial virtual instant.</param>
    /// <param name="zone">The zone used for local "now" values.</param>
    /// <exception cref="ArgumentNullException">
    ///   <paramref name="zone"/> is <c>null</c>.
    /// </exception>
    public PrimeTestClock (Instant initialInstant, DateTimeZone zone) : base(initialInstant, zone)
    {
    }
    //----------------------------------------------------------------------------

    #endregion Constructors/Finalizers

    //----------------------------------------------------------------------------
    /// <summary>
    ///   Gets the virtual UTC observation instant with persist-on-read applied when the automatic runner is active.
    /// </summary>
    /// <returns>The observed virtual instant in UTC.</returns>
    private Instant GetObservationVirtualInstant ()
    {
        DateTimeOffset utcObservation = GetObservationVirtualUtcDateTimeOffset();
        return Instant.FromDateTimeUtc(utcObservation.UtcDateTime);
    }
    //----------------------------------------------------------------------------

    #region IPrimeClock Implementation — Now (Noda)

    //----------------------------------------------------------------------------
    /// <inheritdoc />
    /// <remarks>
    ///   While <see cref="IPrimeTestTime.IsRunning"/> is <c>true</c>, uses the same persist-on-read projection and
    ///   march as <see cref="IPrimeClock.UtcNowDateTimeOffset"/> on this clock. While stopped, returns the persisted
    ///   instant only.
    /// </remarks>
    public Instant NowInstant
    {
        get
        {
            return GetObservationVirtualInstant();
        }
    }
    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public ZonedDateTime UtcNowInstant
    {
        get
        {
            Instant instant = GetObservationVirtualInstant();
            return instant.InUtc();
        }
    }
    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public ZonedDateTime LocalZonedNowInstant
    {
        get
        {
            Instant instant = GetObservationVirtualInstant();
            return instant.InZone(TimeZone);
        }
    }
    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public ZonedDateTime UtcZonedNowInstant { [DebuggerStepThrough] get => UtcNowInstant; }
    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public DateTimeOffset LocalNowDateTimeOffset
    {
        get
        {
            Instant instant = GetObservationVirtualInstant();
            return instant.InZone(TimeZone).ToDateTimeOffset();
        }
    }
    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public DateTimeOffset UtcNowDateTimeOffset
    {
        get => GetObservationVirtualUtcDateTimeOffset();
    }
    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public DateTime LocalNowDateTime
    {
        get
        {
            Instant instant = GetObservationVirtualInstant();
            return instant.InZone(TimeZone).ToDateTimeOffset().LocalDateTime;
        }
    }
    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public DateTime UtcNowDateTime
    {
        get => GetObservationVirtualUtcDateTimeOffset().UtcDateTime;
    }
    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public LocalDateTime LocalNowInstant
    {
        get
        {
            Instant instant = GetObservationVirtualInstant();
            return instant.InZone(TimeZone).LocalDateTime;
        }
    }
    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public LocalTime LocalNowTime
    {
        get
        {
            Instant instant = GetObservationVirtualInstant();
            return instant.InZone(TimeZone).TimeOfDay;
        }
    }
    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public LocalTime UtcNowTime
    {
        get
        {
            Instant instant = GetObservationVirtualInstant();
            return instant.InUtc().TimeOfDay;
        }
    }
    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public LocalDate LocalNowDate
    {
        get
        {
            Instant instant = GetObservationVirtualInstant();
            return instant.InZone(TimeZone).Date;
        }
    }
    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public LocalDate UtcNowDate
    {
        get
        {
            Instant instant = GetObservationVirtualInstant();
            return instant.InUtc().Date;
        }
    }
    //----------------------------------------------------------------------------

#if NET
    /// <inheritdoc />
    /// <remarks>
    ///   <para>
    ///     Only supported in .NET 8+. Not supported in NetStandard 2.0.
    ///   </para>
    /// </remarks>
    public TimeOnly LocalNowTimeOnly
    {
        get
        {
            Instant instant = GetObservationVirtualInstant();
            return TimeOnly.FromDateTime(instant.InZone(TimeZone).ToDateTimeOffset().LocalDateTime);
        }
    }

    /// <inheritdoc />
    /// <remarks>
    ///   <para>
    ///     Only supported in .NET 8+. Not supported in NetStandard 2.0.
    ///   </para>
    /// </remarks>
    public TimeOnly UtcNowTimeOnly
    {
        get
        {
            return TimeOnly.FromDateTime(GetObservationVirtualInstant().ToDateTimeUtc());
        }
    }

    /// <inheritdoc />
    /// <remarks>
    ///   <para>
    ///     Only supported in .NET 8+. Not supported in NetStandard 2.0.
    ///   </para>
    /// </remarks>
    public DateOnly LocalNowDateOnly
    {
        get
        {
            Instant instant = GetObservationVirtualInstant();
            return DateOnly.FromDateTime(instant.InZone(TimeZone).ToDateTimeOffset().LocalDateTime);
        }
    }

    /// <inheritdoc />
    /// <remarks>
    ///   <para>
    ///     Only supported in .NET 8+. Not supported in NetStandard 2.0.
    ///   </para>
    /// </remarks>
    public DateOnly UtcNowDateOnly
    {
        get
        {
            return DateOnly.FromDateTime(GetObservationVirtualInstant().ToDateTimeUtc());
        }
    }
#endif

    #endregion IPrimeClock Implementation — Now (Noda)

    #region IPrimeClock Implementation — Local schedule zone

    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public TimeZoneInfo LocalScheduleTimeZone
    {
        [DebuggerStepThrough]
        get =>
        NodaDateTimeZoneBclConversion.GetLocalScheduleTimeZoneInfo(TimeZone);
    }
    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public DateTimeZone LocalScheduleDateTimeZone { [DebuggerStepThrough] get => TimeZone; }
    //----------------------------------------------------------------------------

    #endregion IPrimeClock Implementation — Local schedule zone

    //----------------------------------------------------------------------------
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
    //----------------------------------------------------------------------------
    /// <summary>
    ///   Projects a UTC instant as a <see cref="DateTimeOffset"/> in this clock's configured zone.
    /// </summary>
    /// <param name="utcNowOffset">The virtual instant in UTC.</param>
    /// <returns>
    ///   The same instant with an offset according to the clock's zone.
    /// </returns>
    private partial DateTimeOffset ToLocalOffset (DateTimeOffset utcNowOffset)
    {
        Instant instant = Instant.FromDateTimeUtc(utcNowOffset.UtcDateTime);
        return instant.InZone(TimeZone).ToDateTimeOffset();
    }
    //----------------------------------------------------------------------------
    /// <summary>
    ///   Gets the UTC offset of this clock's configured zone for an unspecified-kind local wall-clock value.
    /// </summary>
    /// <param name="localUnspecified">The local date and time without a <see cref="DateTime.Kind"/>.</param>
    /// <returns>
    ///   The offset applied when interpreting <paramref name="localUnspecified"/> in that zone.
    /// </returns>
    private partial TimeSpan GetLocalWallClockUtcOffset (DateTime localUnspecified)
    {
        LocalDateTime ldt = LocalDateTime.FromDateTime(DateTime.SpecifyKind(localUnspecified, DateTimeKind.Unspecified));
        return TimeZone.AtLeniently(ldt).Offset.ToTimeSpan();
    }
    //----------------------------------------------------------------------------
    /// <summary>
    ///   Sets the virtual instant from a UTC <see cref="DateTimeOffset"/>. The caller must hold the gate lock.
    /// </summary>
    /// <param name="utcNowOffset">The new virtual UTC time.</param>
    private partial void SetVirtualUtcNowLocked (DateTimeOffset utcNowOffset) =>
        Now = Instant.FromDateTimeUtc(utcNowOffset.UtcDateTime);
    //----------------------------------------------------------------------------
    /// <summary>
    ///   Advances virtual time. The caller must hold the gate lock.
    /// </summary>
    /// <param name="duration">The virtual elapsed time to add.</param>
    private partial void AddVirtualTimeLocked (TimeSpan duration) =>
        Now += Duration.FromTimeSpan(duration);
    //----------------------------------------------------------------------------
    /// <summary>
    ///   Creates a <see cref="PrimeTestClockNewTimeEvent"/> for <see cref="IPrimeTestClock.ClockEvents"/>.
    /// </summary>
    /// <param name="clockInstant">Virtual instant when the event is raised.</param>
    /// <param name="runRateDuration">Active runner rate when running; otherwise <see langword="null"/>.</param>
    /// <returns>The event payload.</returns>
    private static PrimeTestClockNewTimeEvent CreateNewTimeEvent (Instant clockInstant, Duration? runRateDuration) =>
        new(clockInstant, runRateDuration);
    //----------------------------------------------------------------------------
    /// <summary>
    ///   Creates a <see cref="PrimeTestClockStartedEvent"/> for <see cref="IPrimeTestClock.ClockEvents"/>.
    /// </summary>
    /// <param name="clockInstant">Committed virtual instant when the runner started.</param>
    /// <param name="runRateDuration">Active runner rate for the new run.</param>
    /// <returns>The event payload.</returns>
    private static PrimeTestClockStartedEvent CreateStartedEvent (Instant clockInstant, Duration runRateDuration) =>
        new(clockInstant, runRateDuration);
    //----------------------------------------------------------------------------
    /// <summary>
    ///   Creates a <see cref="PrimeTestClockStoppedEvent"/> for <see cref="IPrimeTestClock.ClockEvents"/>.
    /// </summary>
    /// <param name="clockInstant">Final committed virtual instant after the runner stopped.</param>
    /// <param name="runRateDuration">Runner rate that was active before stop.</param>
    /// <returns>The event payload.</returns>
    private static PrimeTestClockStoppedEvent CreateStoppedEvent (Instant clockInstant, Duration? runRateDuration) =>
        new(clockInstant, runRateDuration);
    //----------------------------------------------------------------------------
    /// <summary>
    ///   Raises <see cref="PrimeTestClockEventType.NewTime"/> on <see cref="IPrimeTestClock.ClockEvents"/>.
    /// </summary>
    /// <param name="utcNowDateTimeOffset">
    ///   Virtual UTC reported by the shared march path (required for the shared partial signature). The event payload
    ///   snapshots the persisted virtual instant and run rate under <see cref="PrimeTestTimeBase.Gate"/> instead of
    ///   converting this value.
    /// </param>
    private partial void RaiseNewTimeEvent (DateTimeOffset utcNowDateTimeOffset)
    {
        PrimeTestClockEventHandler? handlers = ClockEvents;
        if (handlers is null)
            return;

        // Required by the shared partial signature; payload uses Now under Gate, not this argument.
        _ = utcNowDateTimeOffset;

        Instant clockInstant;
        Duration? runRateDuration;
        lock (Gate)
        {
            clockInstant = Now;
            runRateDuration = InternalIsRunning ? Duration.FromTimeSpan(_runRate) : null;
        }

        handlers.Invoke(this, CreateNewTimeEvent(clockInstant, runRateDuration));
    }
    //----------------------------------------------------------------------------
    /// <summary>
    ///   Raises <see cref="PrimeTestClockEventType.ClockStarted"/> on <see cref="IPrimeTestClock.ClockEvents"/>.
    /// </summary>
    /// <param name="startUtc">Committed virtual UTC when the runner started.</param>
    /// <param name="runRateTimeSpan">Active runner rate for the new run.</param>
    private partial void RaiseClockStartedEvent (DateTimeOffset startUtc, TimeSpan runRateTimeSpan)
    {
        PrimeTestClockEventHandler? handlers = ClockEvents;
        if (handlers is null)
            return;

        Instant clockInstant = Instant.FromDateTimeUtc(startUtc.UtcDateTime);
        Duration runRateDuration = Duration.FromTimeSpan(runRateTimeSpan);
        handlers.Invoke(this, CreateStartedEvent(clockInstant, runRateDuration));
    }
    //----------------------------------------------------------------------------
    /// <summary>
    ///   Raises <see cref="PrimeTestClockEventType.ClockStopped"/> on <see cref="IPrimeTestClock.ClockEvents"/>.
    /// </summary>
    /// <param name="finalUtc">Final committed virtual UTC after the runner stopped.</param>
    /// <param name="runRateTimeSpan">Runner rate that was active before stop.</param>
    private partial void RaiseClockStoppedEvent (DateTimeOffset finalUtc, TimeSpan? runRateTimeSpan)
    {
        PrimeTestClockEventHandler? handlers = ClockEvents;
        if (handlers is null)
            return;

        Instant clockInstant = Instant.FromDateTimeUtc(finalUtc.UtcDateTime);
        Duration? runRateDuration = runRateTimeSpan is { } rate ? Duration.FromTimeSpan(rate) : null;
        handlers.Invoke(this, CreateStoppedEvent(clockInstant, runRateDuration));
    }
    //----------------------------------------------------------------------------

    #region Interface Implementations

    #region IPrimeTestClock Implementation — Noda

    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public void SetInstant (Instant instant)
    {
        DateTimeOffset utc = new DateTimeOffset(instant.ToDateTimeUtc(), TimeSpan.Zero);
        SetTime(utc);
    }
    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public void SetLocalTime (LocalDateTime localDateTime)
    {
        Instant instant = localDateTime.InZoneLeniently(TimeZone).ToInstant();
        DateTimeOffset utc = new DateTimeOffset(instant.ToDateTimeUtc(), TimeSpan.Zero);
        SetTime(utc);
    }
    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public void Advance (Duration duration) =>
        Advance(NodaDurationBclConversion.ToTimeSpanForTimerInterval(duration));
    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public bool RunFor (Duration duration) =>
        RunFor(NodaDurationBclConversion.ToTimeSpanForTimerInterval(duration));
    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public bool RunFor (Duration duration, Duration perSecondRate) =>
        RunFor(NodaDurationBclConversion.ToTimeSpanForTimerInterval(duration),
            NodaDurationBclConversion.ToTimeSpanForTimerInterval(perSecondRate));
    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public void Start (Duration? perSecondRate = null) =>
        Start(perSecondRate is { } duration
            ? NodaDurationBclConversion.ToTimeSpanForTimerInterval(duration)
            : null);
    //----------------------------------------------------------------------------

    #endregion IPrimeTestClock Implementation — Noda

    #region IPrimeClock Implementation — Interval timers (Duration)

    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public IClockIntervalTimer RegisterTimer (Duration callbackTime,
        Duration repeatInterval,
        Action<ClockTimerCallbackContext> callback,
        CancellationToken cancellationToken,
        object? state = null,
        IntervalTimerOptions? timerOptions = null) =>
        RegisterTimer(NodaDurationBclConversion.ToTimeSpanForTimerInterval(callbackTime),
            NodaDurationBclConversion.ToTimeSpanForTimerInterval(repeatInterval),
            callback,
            cancellationToken,
            state,
            timerOptions);
    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public IClockIntervalTimer RegisterAsyncTimer (Duration callbackTime,
        Duration repeatInterval,
        Func<ClockTimerCallbackContext, CancellationToken, ValueTask> callback,
        CancellationToken cancellationToken,
        object? state = null,
        IntervalTimerOptions? timerOptions = null) =>
        RegisterAsyncTimer(NodaDurationBclConversion.ToTimeSpanForTimerInterval(callbackTime),
            NodaDurationBclConversion.ToTimeSpanForTimerInterval(repeatInterval),
            callback,
            cancellationToken,
            state,
            timerOptions);
    //----------------------------------------------------------------------------

    #endregion IPrimeClock Implementation — Interval timers (Duration)

    #region IPrimeClock Implementation — Time-of-day timers (LocalTime)

    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public IClockDayTimeTimer RegisterTimeOfDay (LocalTime timeOfDay,
        Action<ClockTimerCallbackContext> callback,
        CancellationToken cancellationToken,
        object? state = null,
        DayTimeTimerOptions? timerOptions = null) =>
        RegisterTimeOfDayLocal(LocalTimeToTargetTimeOfDay(timeOfDay), TimerCallbackKind.ContextAction, callback, state,
            timerOptions, cancellationToken);
    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public IClockDayTimeTimer RegisterAsyncTimeOfDay (LocalTime timeOfDay,
        Func<ClockTimerCallbackContext, CancellationToken, ValueTask> callback,
        CancellationToken cancellationToken,
        object? state = null,
        DayTimeTimerOptions? timerOptions = null) =>
        RegisterTimeOfDayLocal(LocalTimeToTargetTimeOfDay(timeOfDay), TimerCallbackKind.ContextAsync, callback, state,
            timerOptions, cancellationToken);
    //----------------------------------------------------------------------------

    #endregion IPrimeClock Implementation — Time-of-day timers (LocalTime)

    #endregion Interface Implementations
}
//################################################################################
