namespace KZDev.PrimeTime
{
    //################################################################################
    /// <summary>
    ///   Extends <see cref="IPrimeTime"/> with BCL-based "now" APIs so callers can obtain current
    ///   time from the clock abstraction using <see cref="DateTimeOffset"/> and <see cref="DateTime"/>.
    ///   On .NET 6 and later, time-only and date-only "now" members (LocalNowTime, UtcNowTime,
    ///   LocalNowDate, UtcNowDate) are also available.
    /// </summary>
    public interface IPrimeSystemClock : IPrimeTime
    {
        #region IPrimeSystemClock — Now (date and time)

        //--------------------------------------------------------------------------------
        /// <summary>
        ///   Gets the current local date and time as a <see cref="DateTimeOffset"/>.
        /// </summary>
        DateTimeOffset LocalNow { get; }
        //--------------------------------------------------------------------------------
        /// <summary>
        ///   Gets the current UTC date and time as a <see cref="DateTimeOffset"/>.
        /// </summary>
        DateTimeOffset UtcNow { get; }
        //--------------------------------------------------------------------------------
        /// <summary>
        ///   Gets the current local date and time as a <see cref="DateTime"/> with
        ///   <see cref="DateTime.Kind"/> equal to <see cref="DateTimeKind.Local"/>.
        /// </summary>
        DateTime LocalDateTimeNow { get; }
        //--------------------------------------------------------------------------------
        /// <summary>
        ///   Gets the current UTC date and time as a <see cref="DateTime"/> with
        ///   <see cref="DateTime.Kind"/> equal to <see cref="DateTimeKind.Utc"/>.
        /// </summary>
        DateTime UtcDateTimeNow { get; }
        //--------------------------------------------------------------------------------

        #endregion IPrimeSystemClock — Now (date and time)

#if NET
        #region IPrimeSystemClock — Now (time-only and date-only)

        //--------------------------------------------------------------------------------
        /// <summary>
        ///   Gets the current local time of day (no date component).
        /// </summary>
        TimeOnly LocalNowTime { get; }
        //--------------------------------------------------------------------------------
        /// <summary>
        ///   Gets the current UTC time of day (no date component).
        /// </summary>
        TimeOnly UtcNowTime { get; }
        //--------------------------------------------------------------------------------
        /// <summary>
        ///   Gets the current local date (no time component).
        /// </summary>
        DateOnly LocalNowDate { get; }
        //--------------------------------------------------------------------------------
        /// <summary>
        ///   Gets the current UTC date (no time component).
        /// </summary>
        DateOnly UtcNowDate { get; }
        //--------------------------------------------------------------------------------

        #endregion IPrimeSystemClock — Now (time-only and date-only)
#endif
    }
    //################################################################################
}
