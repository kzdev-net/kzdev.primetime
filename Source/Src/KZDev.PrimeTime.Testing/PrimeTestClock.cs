// Copyright (c) Kevin Zehrer
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Diagnostics;

using NodaTime;

namespace KZDev.PrimeTime.Testing;

//################################################################################
/// <summary>
///   NodaTime virtual instant storage, zone mapping, and Noda-specific surface for
///   <see cref="PrimeTestClock"/>.
/// </summary>
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
    ///   Raises <see cref="IPrimeTestClock.ClockEvents"/> with <see cref="NodaClockTimeChangedEventArgs"/> created from
    ///   the virtual instant read under the gate lock (expected to correspond to the BCL partial&apos;s
    ///   <c>utcNowDateTimeOffset</c> argument when callers update virtual time and then raise in sequence).
    /// </summary>
    /// <param name="utcNowDateTimeOffset">
    ///   The virtual UTC time after the change (the BCL partial forwards this value to <see cref="ClockTimeChangedEventArgs"/>).
    /// </param>
    private partial void RaiseClockEventsAfterVirtualUtcChange (DateTimeOffset utcNowDateTimeOffset)
    {
        Instant snapshot;
        lock (Gate)
            snapshot = Now;

        ClockEvents?.Invoke(this, new NodaClockTimeChangedEventArgs(snapshot));
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
    public void RunFor (Duration duration) =>
        RunFor(NodaDurationBclConversion.ToTimeSpanForTimerInterval(duration));
    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public void Start (Duration? rate = null) =>
        Start(rate is { } duration ? NodaDurationBclConversion.ToTimeSpanForTimerInterval(duration) : null);
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
