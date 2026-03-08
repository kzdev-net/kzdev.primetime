using NodaTime;

namespace KZDev.PrimeTime
{
    //################################################################################
    /// <summary>
    ///   Extends <see cref="IPrimeTime"/> with NodaTime-based "now" APIs so callers can obtain
    ///   current time from the clock abstraction using <see cref="Instant"/>,
    ///   <see cref="LocalDateTime"/>, <see cref="ZonedDateTime"/>, <see cref="LocalTime"/>,
    ///   and <see cref="LocalDate"/>.
    /// </summary>
    public interface IPrimeClock : IPrimeTime
    {
        #region IPrimeClock — Now (instant and zoned date and time)

        //--------------------------------------------------------------------------------
        /// <summary>
        ///   Gets the current instant on the global timeline (UTC).
        /// </summary>
        Instant Instant { get; }
        //--------------------------------------------------------------------------------
        /// <summary>
        ///   Gets the current local date and time in the system default time zone as a
        ///   <see cref="LocalDateTime"/> (no time zone information).
        /// </summary>
        LocalDateTime LocalNow { get; }
        //--------------------------------------------------------------------------------
        /// <summary>
        ///   Gets the current date and time in UTC as a <see cref="ZonedDateTime"/>.
        /// </summary>
        ZonedDateTime UtcNow { get; }
        //--------------------------------------------------------------------------------
        /// <summary>
        ///   Gets the current date and time in the system default time zone as a
        ///   <see cref="ZonedDateTime"/>.
        /// </summary>
        ZonedDateTime LocalZonedNow { get; }
        //--------------------------------------------------------------------------------
        /// <summary>
        ///   Gets the current date and time in UTC as a <see cref="ZonedDateTime"/>.
        ///   Equivalent to <see cref="UtcNow"/> for symmetry with <see cref="LocalZonedNow"/>.
        /// </summary>
        ZonedDateTime UtcZonedNow { get; }
        //--------------------------------------------------------------------------------

        #endregion IPrimeClock — Now (instant and zoned date and time)

        #region IPrimeClock — Now (time-only and date-only)

        //--------------------------------------------------------------------------------
        /// <summary>
        ///   Gets the current local time of day (no date component) in the system default zone.
        /// </summary>
        LocalTime LocalNowTime { get; }
        //--------------------------------------------------------------------------------
        /// <summary>
        ///   Gets the current UTC time of day (no date component).
        /// </summary>
        LocalTime UtcNowTime { get; }
        //--------------------------------------------------------------------------------
        /// <summary>
        ///   Gets the current local date (no time component) in the system default zone.
        /// </summary>
        LocalDate LocalNowDate { get; }
        //--------------------------------------------------------------------------------
        /// <summary>
        ///   Gets the current UTC date (no time component).
        /// </summary>
        LocalDate UtcNowDate { get; }
        //--------------------------------------------------------------------------------

        #endregion IPrimeClock — Now (time-only and date-only)
    }
    //################################################################################
}
