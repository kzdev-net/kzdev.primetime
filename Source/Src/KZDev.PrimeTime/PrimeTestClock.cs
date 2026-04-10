// Copyright (c) Kevin Zehrer. All rights reserved.
// This file is part of the PrimeTime project.

using System.Diagnostics;
using NodaTime;
using NodaTime.TimeZones;

namespace KZDev.PrimeTime;

//################################################################################
/// <summary>
///   NodaTime virtual instant storage, zone mapping, and Noda-specific surface for
///   <see cref="PrimeTestClock"/>.
/// </summary>
public sealed partial class PrimeTestClock : PrimeTestTimeBase
{
    #region Nested types

    //============================================================================
    /// <summary>
    ///   Noda <see cref="IClockDayTimeTimer"/> change overloads for shared virtual day-time timers.
    /// </summary>
    private abstract partial class VirtualDayTimeTimerBase
    {
        #region Interface Implementations

        #region IClockTimer (Noda) Implementation

        //------------------------------------------------------------------------
        /// <inheritdoc />
        public Instant RegisteredInstant { [DebuggerStepThrough] get => Instant.FromDateTimeOffset(RegisteredTime); }
        //------------------------------------------------------------------------

        #endregion IClockTimer (Noda) Implementation

        #region IClockDayTimeTimer (Noda) Implementation

        //------------------------------------------------------------------------
        /// <inheritdoc />
        public bool Change (LocalTime targetTimeOfDay)
        {
            if (!IsLocal)
                return false;

            lock (Gate)
            {
                if (Disposed || State == TimerState.Cancelled)
                    return false;
                TargetTimeOfDay = LocalTimeToTargetTimeOfDay(targetTimeOfDay);
                if (Enabled)
                    NextDueUtc = ComputeNextDue(Clock.UtcNowDateTimeOffset);

                return true;
            }
        }
        //------------------------------------------------------------------------

        //------------------------------------------------------------------------
        /// <inheritdoc />
        public bool Change (Duration interval) => false;
        //------------------------------------------------------------------------

        #endregion IClockDayTimeTimer (Noda) Implementation

        #endregion Interface Implementations
    }
    //============================================================================

    #endregion Nested types

    #region Constructors/Finalizers

    //----------------------------------------------------------------------------
    /// <summary>
    ///   Initializes a new instance with virtual time set to the current system instant.
    /// </summary>
    public PrimeTestClock ()
    {
    }
    //----------------------------------------------------------------------------

    //----------------------------------------------------------------------------
    /// <summary>
    ///   Initializes a new instance with the specified initial instant and system default zone.
    /// </summary>
    /// <param name="initialInstant">The initial virtual instant.</param>
    public PrimeTestClock (Instant initialInstant) : base (initialInstant)
    {
    }
    //----------------------------------------------------------------------------

    //----------------------------------------------------------------------------
    /// <summary>
    ///   Initializes a new instance with the specified initial instant and time zone.
    /// </summary>
    /// <param name="initialInstant">The initial virtual instant.</param>
    /// <param name="zone">The zone used for local "now" values.</param>
    /// <exception cref="ArgumentNullException">
    ///   <paramref name="zone"/> is <c>null</c>.
    /// </exception>
    public PrimeTestClock (Instant initialInstant, DateTimeZone zone) : base (initialInstant, zone)
    {
    }
    //----------------------------------------------------------------------------

    #endregion Constructors/Finalizers

    #region IPrimeClock Implementation — Now (Noda)

    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public Instant NowInstant
    {
        get
        {
            lock (_gate)
                return _now;
        }
    }
    //----------------------------------------------------------------------------

    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public ZonedDateTime UtcNowInstant
    {
        get
        {
            lock (_gate)
                return _now.InUtc();
        }
    }
    //----------------------------------------------------------------------------

    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public ZonedDateTime LocalZonedNowInstant
    {
        get
        {
            lock (_gate)
                return _now.InZone(_zone);
        }
    }
    //----------------------------------------------------------------------------

    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public ZonedDateTime UtcZonedNowInstant { [DebuggerStepThrough] get => UtcNowInstant; }
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

    #endregion IPrimeClock Implementation — Now (Noda)

    #region IPrimeClock Implementation — Local schedule zone

    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public TimeZoneInfo LocalScheduleTimeZone { [DebuggerStepThrough] get =>
        NodaDateTimeZoneBclInterop.GetLocalScheduleTimeZoneInfo(_zone); }
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
        return instant.InZone(_zone).ToDateTimeOffset();
    }
    //----------------------------------------------------------------------------

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
        return _zone.AtLeniently(ldt).Offset.ToTimeSpan();
    }
    //----------------------------------------------------------------------------

    //----------------------------------------------------------------------------
    /// <summary>
    ///   Sets the virtual instant from a UTC <see cref="DateTimeOffset"/>. The caller must hold the gate lock.
    /// </summary>
    /// <param name="utcNowOffset">The new virtual UTC time.</param>
    private partial void SetVirtualUtcNowLocked (DateTimeOffset utcNowOffset) =>
        _now = Instant.FromDateTimeUtc(utcNowOffset.UtcDateTime);
    //----------------------------------------------------------------------------

    //----------------------------------------------------------------------------
    /// <summary>
    ///   Advances virtual time. The caller must hold the gate lock.
    /// </summary>
    /// <param name="duration">The virtual elapsed time to add.</param>
    private partial void AddVirtualTimeLocked (TimeSpan duration) =>
        _now += Duration.FromTimeSpan(duration);
    //----------------------------------------------------------------------------

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
        lock (_gate)
            snapshot = _now;

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
        lock (_gate)
            SetVirtualUtcNowLocked(utc);

        RaiseClockEventsAfterVirtualUtcChange(utc);
    }
    //----------------------------------------------------------------------------

    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public void SetLocalTime (LocalDateTime localDateTime)
    {
        Instant instant = localDateTime.InZoneLeniently(_zone).ToInstant();
        DateTimeOffset utc = new DateTimeOffset(instant.ToDateTimeUtc(), TimeSpan.Zero);
        lock (_gate)
            SetVirtualUtcNowLocked(utc);

        RaiseClockEventsAfterVirtualUtcChange(utc);
    }
    //----------------------------------------------------------------------------

    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public void Advance (Duration duration) =>
        Advance(NodaDurationBclConversions.ToTimeSpanForTimerInterval(duration));
    //----------------------------------------------------------------------------

    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public void RunFor (Duration duration) =>
        RunFor(NodaDurationBclConversions.ToTimeSpanForTimerInterval(duration));
    //----------------------------------------------------------------------------

    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public void Start (Duration? rate = null) =>
        Start(rate is { } d ? NodaDurationBclConversions.ToTimeSpanForTimerInterval(d) : null);
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
        RegisterTimer(NodaDurationBclConversions.ToTimeSpanForTimerInterval(callbackTime),
            NodaDurationBclConversions.ToTimeSpanForTimerInterval(repeatInterval),
            callback,
            cancellationToken,
            state,
            timerOptions);
    //----------------------------------------------------------------------------

    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public IClockIntervalTimer RegisterAsyncTimer (Duration callbackTime,
        Duration repeatInterval,
        Func<ClockTimerCallbackContext, CancellationToken, ValueTask> callback,
        CancellationToken cancellationToken,
        object? state = null,
        IntervalTimerOptions? timerOptions = null) =>
        RegisterAsyncTimer(NodaDurationBclConversions.ToTimeSpanForTimerInterval(callbackTime),
            NodaDurationBclConversions.ToTimeSpanForTimerInterval(repeatInterval),
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
        RegisterTimeOfDayLocal(LocalTimeToTargetTimeOfDay(timeOfDay), IntervalTimerCallbackKind.ContextAction, callback, state,
            timerOptions, cancellationToken);
    //----------------------------------------------------------------------------

    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public IClockDayTimeTimer RegisterAsyncTimeOfDay (LocalTime timeOfDay,
        Func<ClockTimerCallbackContext, CancellationToken, ValueTask> callback,
        CancellationToken cancellationToken,
        object? state = null,
        DayTimeTimerOptions? timerOptions = null) =>
        RegisterTimeOfDayLocal(LocalTimeToTargetTimeOfDay(timeOfDay), IntervalTimerCallbackKind.ContextAsync, callback, state,
            timerOptions, cancellationToken);
    //----------------------------------------------------------------------------

    #endregion IPrimeClock Implementation — Time-of-day timers (LocalTime)

    #endregion Interface Implementations
}
//################################################################################
