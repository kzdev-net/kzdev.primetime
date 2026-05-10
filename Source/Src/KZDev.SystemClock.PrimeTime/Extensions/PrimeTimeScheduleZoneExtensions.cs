// Copyright (c) Kevin Zehrer
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

namespace KZDev.SystemClock.PrimeTime;

//################################################################################
/// <summary>
///   Projects absolute instants into the local schedule time zone carried by an
///   <see cref="IPrimeClock"/> instance, using the same <see cref="TimeZoneInfo"/> as
///   <see cref="IPrimeClock.LocalScheduleTimeZone"/>.
/// </summary>
/// <remarks>
///   <para>
///     These extensions interpret the <see cref="IPrimeTime"/> receiver as <see cref="IPrimeClock"/> to read
///     <see cref="IPrimeClock.LocalScheduleTimeZone"/>. Production and test clocks implement
///     <see cref="IPrimeClock"/>; an <see cref="IPrimeTime"/> that is not a clock cannot resolve
///     a schedule zone and is rejected.
///   </para>
/// </remarks>
public static class PrimeTimeScheduleZoneExtensions
{
    #region DateTimeOffset → schedule zone

    //----------------------------------------------------------------------------
    /// <summary>
    ///   Converts an absolute <see cref="DateTimeOffset"/> to the same instant expressed with the offset of the
    ///   receiver's <see cref="IPrimeClock.LocalScheduleTimeZone"/>.
    /// </summary>
    /// <param name="time">The time service used as context for the schedule zone.</param>
    /// <param name="instant">An absolute point in time (any offset input is normalized to the same instant).</param>
    /// <returns>
    ///   A <see cref="DateTimeOffset"/> whose offset matches the schedule zone at that instant.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    ///   <paramref name="time"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    ///   <paramref name="time"/> is not an <see cref="IPrimeClock"/> and the schedule zone cannot be resolved.
    /// </exception>
    public static DateTimeOffset ToScheduleDateTimeOffset (this IPrimeTime time, DateTimeOffset instant)
    {
        if (time is null)
            throw new ArgumentNullException(nameof(time));
        IPrimeClock clock = RequirePrimeClock(time, nameof(time));
        return TimeZoneInfo.ConvertTime(instant, clock.LocalScheduleTimeZone);
    }
    //----------------------------------------------------------------------------
    /// <summary>
    ///   Converts a UTC <see cref="DateTime"/> to a <see cref="DateTimeOffset"/> in the receiver's
    ///   <see cref="IPrimeClock.LocalScheduleTimeZone"/>.
    /// </summary>
    /// <param name="time">The time service used as context for the schedule zone.</param>
    /// <param name="utcDateTime">
    ///   An absolute UTC timeline value; must use <see cref="DateTimeKind.Utc"/>.
    /// </param>
    /// <returns>
    ///   A <see cref="DateTimeOffset"/> whose offset matches the schedule zone at that instant.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    ///   <paramref name="time"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    ///   <paramref name="utcDateTime"/> does not use <see cref="DateTimeKind.Utc"/>, or <paramref name="time"/> is not
    ///   an <see cref="IPrimeClock"/>.
    /// </exception>
    public static DateTimeOffset ToScheduleDateTimeOffset (this IPrimeTime time, DateTime utcDateTime)
    {
        if (utcDateTime.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException(
                "The value must use DateTimeKind.Utc so it represents an unambiguous absolute instant on the UTC timeline.",
                nameof(utcDateTime));
        }

        DateTimeOffset instant = new(utcDateTime, TimeSpan.Zero);
        return time.ToScheduleDateTimeOffset(instant);
    }
    //----------------------------------------------------------------------------
    /// <summary>
    ///   Converts an absolute <see cref="DateTimeOffset"/> to the schedule-local wall-clock
    ///   <see cref="DateTime"/> (unspecified kind) in the receiver's <see cref="IPrimeClock.LocalScheduleTimeZone"/>.
    /// </summary>
    /// <param name="time">The time service used as context for the schedule zone.</param>
    /// <param name="instant">An absolute point in time.</param>
    /// <returns>
    ///   Local wall date and time in the schedule zone; <see cref="DateTime.Kind"/> is <see cref="DateTimeKind.Unspecified"/>.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    ///   <paramref name="time"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    ///   <paramref name="time"/> is not an <see cref="IPrimeClock"/> and the schedule zone cannot be resolved.
    /// </exception>
    public static DateTime ToScheduleLocalWallDateTime (this IPrimeTime time, DateTimeOffset instant)
    {
        DateTimeOffset inZone = time.ToScheduleDateTimeOffset(instant);
        return inZone.DateTime;
    }
    //----------------------------------------------------------------------------
    /// <summary>
    ///   Converts a UTC <see cref="DateTime"/> to the schedule-local wall-clock <see cref="DateTime"/> in the
    ///   receiver's <see cref="IPrimeClock.LocalScheduleTimeZone"/>.
    /// </summary>
    /// <param name="time">The time service used as context for the schedule zone.</param>
    /// <param name="utcDateTime">
    ///   An absolute UTC timeline value; must use <see cref="DateTimeKind.Utc"/>.
    /// </param>
    /// <returns>
    ///   Local wall date and time in the schedule zone.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    ///   <paramref name="time"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    ///   <paramref name="utcDateTime"/> does not use <see cref="DateTimeKind.Utc"/>, or <paramref name="time"/> is not
    ///   an <see cref="IPrimeClock"/>.
    /// </exception>
    public static DateTime ToScheduleLocalWallDateTime (this IPrimeTime time, DateTime utcDateTime)
    {
        DateTimeOffset inZone = time.ToScheduleDateTimeOffset(utcDateTime);
        return inZone.DateTime;
    }
    //----------------------------------------------------------------------------
#if NET
    /// <summary>
    ///   Converts an absolute <see cref="DateTimeOffset"/> to a <see cref="DateOnly"/> in the receiver's
    ///   <see cref="IPrimeClock.LocalScheduleTimeZone"/>.
    /// </summary>
    /// <param name="time">The time service used as context for the schedule zone.</param>
    /// <param name="instant">An absolute point in time.</param>
    /// <returns>
    ///   The calendar date in the schedule zone.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    ///   <paramref name="time"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    ///   <paramref name="time"/> is not an <see cref="IPrimeClock"/> and the schedule zone cannot be resolved.
    /// </exception>
    public static DateOnly ToScheduleDateOnly (this IPrimeTime time, DateTimeOffset instant)
    {
        DateTime wall = time.ToScheduleLocalWallDateTime(instant);
        return DateOnly.FromDateTime(wall);
    }
    //----------------------------------------------------------------------------
    /// <summary>
    ///   Converts a UTC <see cref="DateTime"/> to a <see cref="DateOnly"/> in the receiver's
    ///   <see cref="IPrimeClock.LocalScheduleTimeZone"/>.
    /// </summary>
    /// <param name="time">The time service used as context for the schedule zone.</param>
    /// <param name="utcDateTime">
    ///   An absolute UTC timeline value; must use <see cref="DateTimeKind.Utc"/>.
    /// </param>
    /// <returns>
    ///   The calendar date in the schedule zone.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    ///   <paramref name="time"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    ///   <paramref name="utcDateTime"/> does not use <see cref="DateTimeKind.Utc"/>, or <paramref name="time"/> is not
    ///   an <see cref="IPrimeClock"/>.
    /// </exception>
    public static DateOnly ToScheduleDateOnly (this IPrimeTime time, DateTime utcDateTime)
    {
        DateTime wall = time.ToScheduleLocalWallDateTime(utcDateTime);
        return DateOnly.FromDateTime(wall);
    }
    //----------------------------------------------------------------------------
    /// <summary>
    ///   Converts an absolute <see cref="DateTimeOffset"/> to a <see cref="TimeOnly"/> in the receiver's
    ///   <see cref="IPrimeClock.LocalScheduleTimeZone"/>.
    /// </summary>
    /// <param name="time">The time service used as context for the schedule zone.</param>
    /// <param name="instant">An absolute point in time.</param>
    /// <returns>
    ///   The wall time of day in the schedule zone.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    ///   <paramref name="time"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    ///   <paramref name="time"/> is not an <see cref="IPrimeClock"/> and the schedule zone cannot be resolved.
    /// </exception>
    public static TimeOnly ToScheduleTimeOnly (this IPrimeTime time, DateTimeOffset instant)
    {
        DateTime wall = time.ToScheduleLocalWallDateTime(instant);
        return TimeOnly.FromDateTime(wall);
    }
    //----------------------------------------------------------------------------
    /// <summary>
    ///   Converts a UTC <see cref="DateTime"/> to a <see cref="TimeOnly"/> in the receiver's
    ///   <see cref="IPrimeClock.LocalScheduleTimeZone"/>.
    /// </summary>
    /// <param name="time">The time service used as context for the schedule zone.</param>
    /// <param name="utcDateTime">
    ///   An absolute UTC timeline value; must use <see cref="DateTimeKind.Utc"/>.
    /// </param>
    /// <returns>
    ///   The wall time of day in the schedule zone.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    ///   <paramref name="time"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    ///   <paramref name="utcDateTime"/> does not use <see cref="DateTimeKind.Utc"/>, or <paramref name="time"/> is not
    ///   an <see cref="IPrimeClock"/>.
    /// </exception>
    public static TimeOnly ToScheduleTimeOnly (this IPrimeTime time, DateTime utcDateTime)
    {
        DateTime wall = time.ToScheduleLocalWallDateTime(utcDateTime);
        return TimeOnly.FromDateTime(wall);
    }
    //----------------------------------------------------------------------------
#endif

    #endregion DateTimeOffset → schedule zone

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
            "Schedule-zone projection requires IPrimeClock so LocalScheduleTimeZone can be resolved. "
            + "Use a clock implementation such as PrimeClock or PrimeTestClock; IPrimeTime-only contexts are not supported.",
            paramName);
    }
    //----------------------------------------------------------------------------
}
//################################################################################
