// Copyright (c) Kevin Zehrer
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using NodaTime;

namespace KZDev.PrimeTime;

//################################################################################
/// <summary>
///   Projects absolute instants into the local schedule time zone carried by an
///   <see cref="IPrimeClock"/> instance, using the same <see cref="DateTimeZone"/> as
///   <see cref="IPrimeClock.LocalScheduleDateTimeZone"/>.
/// </summary>
/// <remarks>
///   <para>
///     These extensions interpret the <see cref="IPrimeTime"/> receiver as <see cref="IPrimeClock"/> to read
///     <see cref="IPrimeClock.LocalScheduleDateTimeZone"/>. Production and test clocks implement
///     <see cref="IPrimeClock"/>; an <see cref="IPrimeTime"/> that is not a clock cannot resolve
///     a schedule zone and is rejected.
///   </para>
/// </remarks>
public static class PrimeTimeScheduleZoneExtensions
{
    #region Instant → schedule zone

    //----------------------------------------------------------------------------
    /// <param name="time">The time service used as context for the schedule zone.</param>
    extension(IPrimeTime time)
    {
        /// <summary>
        ///   Maps an <see cref="Instant"/> to the local wall-clock <see cref="LocalDateTime"/> in the receiver's
        ///   <see cref="IPrimeClock.LocalScheduleDateTimeZone"/>.
        /// </summary>
        /// <param name="instant">The absolute instant on the UTC timeline.</param>
        /// <returns>
        ///   Local date and time in the schedule zone (no zone id on the value).
        /// </returns>
        /// <exception cref="ArgumentNullException">
        ///   <paramref name="time"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="ArgumentException">
        ///   <paramref name="time"/> is not an <see cref="IPrimeClock"/> and the schedule zone cannot be resolved.
        /// </exception>
        private LocalDateTime ToScheduleLocalDateTime (Instant instant)
        {
            ZonedDateTime zoned = time.ToScheduleZonedDateTime(instant);
            return zoned.LocalDateTime;
        }
        /// <summary>
        ///   Maps an <see cref="Instant"/> to a <see cref="ZonedDateTime"/> in the receiver's
        ///   <see cref="IPrimeClock.LocalScheduleDateTimeZone"/>.
        /// </summary>
        /// <param name="instant">The absolute instant on the UTC timeline.</param>
        /// <returns>
        ///   The instant expressed in the local schedule zone.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        ///   <paramref name="time"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="ArgumentException">
        ///   <paramref name="time"/> is not an <see cref="IPrimeClock"/> and the schedule zone cannot be resolved.
        /// </exception>
        public ZonedDateTime ToScheduleZonedDateTime (Instant instant)
        {
            if (time is null)
                throw new ArgumentNullException(nameof(time));
            IPrimeClock clock = RequirePrimeClock(time, nameof(time));
            return instant.InZone(clock.LocalScheduleDateTimeZone);
        }
        /// <summary>
        ///   Maps an <see cref="Instant"/> to the local calendar <see cref="LocalDate"/> in the receiver's
        ///   <see cref="IPrimeClock.LocalScheduleDateTimeZone"/>.
        /// </summary>
        /// <param name="instant">The absolute instant on the UTC timeline.</param>
        /// <returns>
        ///   The calendar date in the schedule zone.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        ///   <paramref name="time"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="ArgumentException">
        ///   <paramref name="time"/> is not an <see cref="IPrimeClock"/> and the schedule zone cannot be resolved.
        /// </exception>
        public LocalDate ToScheduleLocalDate (Instant instant)
        {
            ZonedDateTime zoned = time.ToScheduleZonedDateTime(instant);
            return zoned.Date;
        }
        /// <summary>
        ///   Maps an <see cref="Instant"/> to the local time-of-day <see cref="LocalTime"/> in the receiver's
        ///   <see cref="IPrimeClock.LocalScheduleDateTimeZone"/>.
        /// </summary>
        /// <param name="instant">The absolute instant on the UTC timeline.</param>
        /// <returns>
        ///   The wall-clock time of day in the schedule zone.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        ///   <paramref name="time"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="ArgumentException">
        ///   <paramref name="time"/> is not an <see cref="IPrimeClock"/> and the schedule zone cannot be resolved.
        /// </exception>
        public LocalTime ToScheduleLocalTime (Instant instant)
        {
            ZonedDateTime zoned = time.ToScheduleZonedDateTime(instant);
            return zoned.TimeOfDay;
        }
        /// <summary>
        ///   Maps an <see cref="Instant"/> to a <see cref="DateTimeOffset"/> for the wall time in the receiver's
        ///   <see cref="IPrimeClock.LocalScheduleDateTimeZone"/>.
        /// </summary>
        /// <param name="instant">The absolute instant on the UTC timeline.</param>
        /// <returns>
        ///   A <see cref="DateTimeOffset"/> with the correct offset for the schedule zone at that instant.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        ///   <paramref name="time"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="ArgumentException">
        ///   <paramref name="time"/> is not an <see cref="IPrimeClock"/> and the schedule zone cannot be resolved.
        /// </exception>
        public DateTimeOffset ToScheduleDateTimeOffset (Instant instant)
        {
            ZonedDateTime zoned = time.ToScheduleZonedDateTime(instant);
            return zoned.ToDateTimeOffset();
        }
        /// <summary>
        ///   Maps an <see cref="Instant"/> to a <see cref="DateTime"/> wall-clock value (unspecified kind) in the
        ///   receiver's <see cref="IPrimeClock.LocalScheduleDateTimeZone"/>.
        /// </summary>
        /// <param name="instant">The absolute instant on the UTC timeline.</param>
        /// <returns>
        ///   Local wall date and time as <see cref="DateTime"/>, suitable for persistence patterns that store
        ///   schedule-local wall time without <see cref="DateTimeKind"/>.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        ///   <paramref name="time"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="ArgumentException">
        ///   <paramref name="time"/> is not an <see cref="IPrimeClock"/> and the schedule zone cannot be resolved.
        /// </exception>
        public DateTime ToScheduleLocalWallDateTime (Instant instant)
        {
            LocalDateTime local = time.ToScheduleLocalDateTime(instant);
            return local.ToDateTimeUnspecified();
        }
#if NET
        /// <summary>
        ///   Maps an <see cref="Instant"/> to a <see cref="DateOnly"/> in the receiver's
        ///   <see cref="IPrimeClock.LocalScheduleDateTimeZone"/>.
        /// </summary>
        /// <param name="instant">The absolute instant on the UTC timeline.</param>
        /// <returns>
        ///   The calendar date in the schedule zone.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        ///   <paramref name="time"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="ArgumentException">
        ///   <paramref name="time"/> is not an <see cref="IPrimeClock"/> and the schedule zone cannot be resolved.
        /// </exception>
        /// <remarks>
        ///   <para>
        ///     Only supported in .NET 8+. Not supported in NetStandard 2.0.
        ///   </para>
        /// </remarks>
        public DateOnly ToScheduleDateOnly (Instant instant)
        {
            DateTime wall = time.ToScheduleLocalWallDateTime(instant);
            return DateOnly.FromDateTime(wall);
        }
        /// <summary>
        ///   Maps an <see cref="Instant"/> to a <see cref="TimeOnly"/> wall time in the receiver's
        ///   <see cref="IPrimeClock.LocalScheduleDateTimeZone"/>.
        /// </summary>
        /// <param name="instant">The absolute instant on the UTC timeline.</param>
        /// <returns>
        ///   The time of day in the schedule zone.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        ///   <paramref name="time"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="ArgumentException">
        ///   <paramref name="time"/> is not an <see cref="IPrimeClock"/> and the schedule zone cannot be resolved.
        /// </exception>
        /// <remarks>
        ///   <para>
        ///     Only supported in .NET 8+. Not supported in NetStandard 2.0.
        ///   </para>
        /// </remarks>
        public TimeOnly ToScheduleTimeOnly (Instant instant)
        {
            DateTime wall = time.ToScheduleLocalWallDateTime(instant);
            return TimeOnly.FromDateTime(wall);
        }
#endif
    }
    //----------------------------------------------------------------------------

    #endregion Instant → schedule zone

    #region ZonedDateTime → schedule zone

    //----------------------------------------------------------------------------
    /// <param name="time">The time service used as context for the schedule zone.</param>
    extension(IPrimeTime time)
    {
        /// <summary>
        ///   Converts a <see cref="ZonedDateTime"/> to an <see cref="Instant"/> and maps it into the receiver's
        ///   <see cref="IPrimeClock.LocalScheduleDateTimeZone"/>.
        /// </summary>
        /// <param name="zonedDateTime">A zoned value in any zone; only its instant is used.</param>
        /// <returns>
        ///   The same absolute instant expressed in the local schedule zone.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        ///   <paramref name="time"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="ArgumentException">
        ///   <paramref name="time"/> is not an <see cref="IPrimeClock"/> and the schedule zone cannot be resolved.
        /// </exception>
        public ZonedDateTime ToScheduleZonedDateTime (ZonedDateTime zonedDateTime)
        {
            return time.ToScheduleZonedDateTime(zonedDateTime.ToInstant());
        }
        /// <summary>
        ///   Converts a <see cref="ZonedDateTime"/> to the local wall-clock <see cref="LocalDateTime"/> in the receiver's
        ///   <see cref="IPrimeClock.LocalScheduleDateTimeZone"/>.
        /// </summary>
        /// <param name="zonedDateTime">A zoned value in any zone; only its instant is used.</param>
        /// <returns>
        ///   Local date and time in the schedule zone.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        ///   <paramref name="time"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="ArgumentException">
        ///   <paramref name="time"/> is not an <see cref="IPrimeClock"/> and the schedule zone cannot be resolved.
        /// </exception>
        public LocalDateTime ToScheduleLocalDateTime (ZonedDateTime zonedDateTime)
        {
            return time.ToScheduleLocalDateTime(zonedDateTime.ToInstant());
        }
        /// <summary>
        ///   Converts a <see cref="ZonedDateTime"/> to the local calendar <see cref="LocalDate"/> in the receiver's
        ///   <see cref="IPrimeClock.LocalScheduleDateTimeZone"/>.
        /// </summary>
        /// <param name="zonedDateTime">A zoned value in any zone; only its instant is used.</param>
        /// <returns>
        ///   The calendar date in the schedule zone.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        ///   <paramref name="time"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="ArgumentException">
        ///   <paramref name="time"/> is not an <see cref="IPrimeClock"/> and the schedule zone cannot be resolved.
        /// </exception>
        public LocalDate ToScheduleLocalDate (ZonedDateTime zonedDateTime)
        {
            return time.ToScheduleLocalDate(zonedDateTime.ToInstant());
        }
        /// <summary>
        ///   Converts a <see cref="ZonedDateTime"/> to the local <see cref="LocalTime"/> in the receiver's
        ///   <see cref="IPrimeClock.LocalScheduleDateTimeZone"/>.
        /// </summary>
        /// <param name="zonedDateTime">A zoned value in any zone; only its instant is used.</param>
        /// <returns>
        ///   The wall-clock time of day in the schedule zone.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        ///   <paramref name="time"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="ArgumentException">
        ///   <paramref name="time"/> is not an <see cref="IPrimeClock"/> and the schedule zone cannot be resolved.
        /// </exception>
        public LocalTime ToScheduleLocalTime (ZonedDateTime zonedDateTime)
        {
            return time.ToScheduleLocalTime(zonedDateTime.ToInstant());
        }
        /// <summary>
        ///   Converts a <see cref="ZonedDateTime"/> to a <see cref="DateTimeOffset"/> for the wall time in the receiver's
        ///   <see cref="IPrimeClock.LocalScheduleDateTimeZone"/>.
        /// </summary>
        /// <param name="zonedDateTime">A zoned value in any zone; only its instant is used.</param>
        /// <returns>
        ///   A <see cref="DateTimeOffset"/> with the correct offset for the schedule zone at that instant.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        ///   <paramref name="time"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="ArgumentException">
        ///   <paramref name="time"/> is not an <see cref="IPrimeClock"/> and the schedule zone cannot be resolved.
        /// </exception>
        public DateTimeOffset ToScheduleDateTimeOffset (ZonedDateTime zonedDateTime)
        {
            return time.ToScheduleDateTimeOffset(zonedDateTime.ToInstant());
        }
        /// <summary>
        ///   Converts a <see cref="ZonedDateTime"/> to a schedule-local <see cref="DateTime"/> wall-clock value
        ///   (unspecified kind) in the receiver's <see cref="IPrimeClock.LocalScheduleDateTimeZone"/>.
        /// </summary>
        /// <param name="zonedDateTime">A zoned value in any zone; only its instant is used.</param>
        /// <returns>
        ///   Local wall date and time as <see cref="DateTime"/>.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        ///   <paramref name="time"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="ArgumentException">
        ///   <paramref name="time"/> is not an <see cref="IPrimeClock"/> and the schedule zone cannot be resolved.
        /// </exception>
        public DateTime ToScheduleLocalWallDateTime (ZonedDateTime zonedDateTime)
        {
            return time.ToScheduleLocalWallDateTime(zonedDateTime.ToInstant());
        }
#if NET
        /// <summary>
        ///   Converts a <see cref="ZonedDateTime"/> to a <see cref="DateOnly"/> in the receiver's
        ///   <see cref="IPrimeClock.LocalScheduleDateTimeZone"/>.
        /// </summary>
        /// <param name="zonedDateTime">A zoned value in any zone; only its instant is used.</param>
        /// <returns>
        ///   The calendar date in the schedule zone.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        ///   <paramref name="time"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="ArgumentException">
        ///   <paramref name="time"/> is not an <see cref="IPrimeClock"/> and the schedule zone cannot be resolved.
        /// </exception>
        /// <remarks>
        ///   <para>
        ///     Only supported in .NET 8+. Not supported in NetStandard 2.0.
        ///   </para>
        /// </remarks>
        public DateOnly ToScheduleDateOnly (ZonedDateTime zonedDateTime)
        {
            return time.ToScheduleDateOnly(zonedDateTime.ToInstant());
        }
        /// <summary>
        ///   Converts a <see cref="ZonedDateTime"/> to a <see cref="TimeOnly"/> in the receiver's
        ///   <see cref="IPrimeClock.LocalScheduleDateTimeZone"/>.
        /// </summary>
        /// <param name="zonedDateTime">A zoned value in any zone; only its instant is used.</param>
        /// <returns>
        ///   The time of day in the schedule zone.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        ///   <paramref name="time"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="ArgumentException">
        ///   <paramref name="time"/> is not an <see cref="IPrimeClock"/> and the schedule zone cannot be resolved.
        /// </exception>
        /// <remarks>
        ///   <para>
        ///     Only supported in .NET 8+. Not supported in NetStandard 2.0.
        ///   </para>
        /// </remarks>
        public TimeOnly ToScheduleTimeOnly (ZonedDateTime zonedDateTime)
        {
            return time.ToScheduleTimeOnly(zonedDateTime.ToInstant());
        }
#endif
    }
    //----------------------------------------------------------------------------

    #endregion ZonedDateTime → schedule zone

    //----------------------------------------------------------------------------
    /// <summary>
    ///   Obtains an <see cref="IPrimeClock"/> from <paramref name="time"/> or throws a consistent
    ///   <see cref="ArgumentException"/>.
    /// </summary>
    /// <param name="time">The receiver passed to the calling extension method.</param>
    /// <param name="paramName">The parameter name for the exception.</param>
    /// <returns>
    ///   The same instance as <see cref="IPrimeClock"/>.
    /// </returns>
    /// <exception cref="ArgumentException">
    ///   <paramref name="time"/> does not implement <see cref="IPrimeClock"/>.
    /// </exception>
    private static IPrimeClock RequirePrimeClock (IPrimeTime time, string paramName)
    {
        if (time is IPrimeClock clock)
            return clock;
        throw new ArgumentException(
            "Schedule-zone projection requires IPrimeClock so LocalScheduleDateTimeZone can be resolved. "
            + "Use a clock implementation such as PrimeClock or PrimeTestClock; IPrimeTime-only contexts are not supported.",
            paramName);
    }
    //----------------------------------------------------------------------------
}
//################################################################################
