using NodaTime;

namespace KZDev.PrimeTime
{
    //################################################################################
    /// <summary>
    ///   Production implementation of <see cref="IPrimeClock"/> that delegates "now" to
    ///   NodaTime's <see cref="SystemClock"/> and derives local time using the system
    ///   default time zone.
    /// </summary>
    /// <remarks>
    ///   Delay and time-cancellation members (Sleep, DelayAsync, GetTimeCancellationToken,
    ///   LinkTimeCancellationToken from <see cref="IPrimeTime"/>) are not yet implemented
    ///   and throw <see cref="NotSupportedException"/> until implemented in a later phase.
    /// </remarks>
    public sealed class PrimeClock : IPrimeClock
    {
        private const string DelayNotImplementedMessage =
            "Sleep, DelayAsync, and time-based cancellation are not yet implemented on PrimeClock. " +
            "They will be added in a later phase.";

        private readonly IClock _clock;
        private readonly DateTimeZone _systemDefaultZone;

        /// <summary>
        ///   Initializes a new instance of the <see cref="PrimeClock"/> class using
        ///   <see cref="SystemClock.Instance"/> and the BCL system default time zone.
        /// </summary>
        public PrimeClock ()
            : this(SystemClock.Instance, DateTimeZoneProviders.Bcl.GetSystemDefault())
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

        #region IPrimeClock Implementation

        /// <inheritdoc />
        public Instant Instant => _clock.GetCurrentInstant();

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

        #endregion IPrimeClock Implementation

        #region IPrimeTime Implementation

        /// <inheritdoc />
        /// <exception cref="NotSupportedException">Not implemented in this phase.</exception>
        public void Sleep (TimeSpan sleepTime) => throw new NotSupportedException(DelayNotImplementedMessage);

        /// <inheritdoc />
        /// <exception cref="NotSupportedException">Not implemented in this phase.</exception>
        public void Sleep (int sleepMilliseconds) => throw new NotSupportedException(DelayNotImplementedMessage);

        /// <inheritdoc />
        /// <exception cref="NotSupportedException">Not implemented in this phase.</exception>
        public Task DelayAsync (TimeSpan delayTime) => throw new NotSupportedException(DelayNotImplementedMessage);

        /// <inheritdoc />
        /// <exception cref="NotSupportedException">Not implemented in this phase.</exception>
        public Task DelayAsync (int millisecondsDelay) => throw new NotSupportedException(DelayNotImplementedMessage);

        /// <inheritdoc />
        /// <exception cref="NotSupportedException">Not implemented in this phase.</exception>
        public Task DelayAsync (TimeSpan delayTime, CancellationToken cancellationToken) =>
            throw new NotSupportedException(DelayNotImplementedMessage);

        /// <inheritdoc />
        /// <exception cref="NotSupportedException">Not implemented in this phase.</exception>
        public Task DelayAsync (int millisecondsDelay, CancellationToken cancellationToken) =>
            throw new NotSupportedException(DelayNotImplementedMessage);

        /// <inheritdoc />
        /// <exception cref="NotSupportedException">Not implemented in this phase.</exception>
        public CancellationToken GetTimeCancellationToken (TimeSpan cancelTime) =>
            throw new NotSupportedException(DelayNotImplementedMessage);

        /// <inheritdoc />
        /// <exception cref="NotSupportedException">Not implemented in this phase.</exception>
        public CancellationToken GetTimeCancellationToken (int cancelMilliseconds) =>
            throw new NotSupportedException(DelayNotImplementedMessage);

        /// <inheritdoc />
        /// <exception cref="NotSupportedException">Not implemented in this phase.</exception>
        public CancellationToken LinkTimeCancellationToken (TimeSpan cancelTime, CancellationToken cancellationToken) =>
            throw new NotSupportedException(DelayNotImplementedMessage);

        /// <inheritdoc />
        /// <exception cref="NotSupportedException">Not implemented in this phase.</exception>
        public CancellationToken LinkTimeCancellationToken (int cancelMilliseconds, CancellationToken cancellationToken) =>
            throw new NotSupportedException(DelayNotImplementedMessage);

        /// <inheritdoc />
        /// <exception cref="NotSupportedException">Not implemented in this phase.</exception>
        public CancellationToken LinkTimeCancellationToken (
            int cancelMilliseconds,
            CancellationToken token1,
            CancellationToken token2) =>
            throw new NotSupportedException(DelayNotImplementedMessage);

        /// <inheritdoc />
        /// <exception cref="NotSupportedException">Not implemented in this phase.</exception>
        public CancellationToken LinkTimeCancellationToken (
            TimeSpan cancelTime,
            CancellationToken token1,
            CancellationToken token2) =>
            throw new NotSupportedException(DelayNotImplementedMessage);

        /// <inheritdoc />
        /// <exception cref="NotSupportedException">Not implemented in this phase.</exception>
        public CancellationToken LinkTimeCancellationToken (TimeSpan cancelTime, params CancellationToken[] cancellationTokens) =>
            throw new NotSupportedException(DelayNotImplementedMessage);

        /// <inheritdoc />
        /// <exception cref="NotSupportedException">Not implemented in this phase.</exception>
        public CancellationToken LinkTimeCancellationToken (int cancelMilliseconds, params CancellationToken[] cancellationTokens) =>
            throw new NotSupportedException(DelayNotImplementedMessage);

        #endregion IPrimeTime Implementation
    }
    //################################################################################
}
