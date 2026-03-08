namespace KZDev.PrimeTime
{
    //################################################################################
    /// <summary>
    ///   Production implementation of <see cref="IPrimeSystemClock"/> that delegates "now"
    ///   to a <see cref="TimeProvider"/> time source (e.g. <see cref="TimeProvider.System"/>),
    ///   analogous to the NodaTime stack using an IClock abstraction.
    /// </summary>
    /// <remarks>
    ///   Delay and time-cancellation members (Sleep, DelayAsync, GetTimeCancellationToken,
    ///   LinkTimeCancellationToken from <see cref="IPrimeTime"/>) are not yet implemented
    ///   and throw <see cref="NotSupportedException"/> until implemented in a later phase.
    /// </remarks>
    public sealed class PrimeSystemClock : IPrimeSystemClock
    {
        private const string DelayNotImplementedMessage =
            "Sleep, DelayAsync, and time-based cancellation are not yet implemented on PrimeSystemClock. " +
            "They will be added in a later phase.";

        private readonly TimeProvider _timeProvider;

        /// <summary>
        ///   Initializes a new instance of the <see cref="PrimeSystemClock"/> class using
        ///   <see cref="TimeProvider.System"/> as the time source.
        /// </summary>
        public PrimeSystemClock ()
            : this(TimeProvider.System)
        {
        }

        /// <summary>
        ///   Initializes a new instance of the <see cref="PrimeSystemClock"/> class with the
        ///   specified time provider.
        /// </summary>
        /// <param name="timeProvider">
        ///   The time provider used to obtain the current UTC and local time.
        /// </param>
        public PrimeSystemClock (TimeProvider timeProvider)
        {
            _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        }

        #region IPrimeSystemClock Implementation

        /// <inheritdoc />
        public DateTimeOffset LocalNow => _timeProvider.GetLocalNow();

        /// <inheritdoc />
        public DateTimeOffset UtcNow => _timeProvider.GetUtcNow();

        /// <inheritdoc />
        public DateTime LocalDateTimeNow => _timeProvider.GetLocalNow().LocalDateTime;

        /// <inheritdoc />
        public DateTime UtcDateTimeNow => _timeProvider.GetUtcNow().UtcDateTime;

#if NET
        /// <inheritdoc />
        public TimeOnly LocalNowTime => TimeOnly.FromDateTime(_timeProvider.GetLocalNow().DateTime);

        /// <inheritdoc />
        public TimeOnly UtcNowTime => TimeOnly.FromDateTime(_timeProvider.GetUtcNow().DateTime);

        /// <inheritdoc />
        public DateOnly LocalNowDate => DateOnly.FromDateTime(_timeProvider.GetLocalNow().DateTime);

        /// <inheritdoc />
        public DateOnly UtcNowDate => DateOnly.FromDateTime(_timeProvider.GetUtcNow().DateTime);
#endif

        #endregion IPrimeSystemClock Implementation

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
        public CancellationToken LinkTimeCancellationToken (int cancelMilliseconds,
            CancellationToken token1,
            CancellationToken token2) =>
            throw new NotSupportedException(DelayNotImplementedMessage);

        /// <inheritdoc />
        /// <exception cref="NotSupportedException">Not implemented in this phase.</exception>
        public CancellationToken LinkTimeCancellationToken (TimeSpan cancelTime,
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
