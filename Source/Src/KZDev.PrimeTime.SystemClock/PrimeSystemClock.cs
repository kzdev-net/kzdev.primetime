namespace KZDev.PrimeTime
{
    //################################################################################
    /// <summary>
    ///   Production implementation of <see cref="IPrimeSystemClock"/> that delegates "now"
    ///   to the BCL (<see cref="DateTimeOffset.UtcNow"/>, local time, and time zone).
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

        #region IPrimeSystemClock Implementation

        /// <inheritdoc />
        public DateTimeOffset LocalNow => DateTimeOffset.Now;

        /// <inheritdoc />
        public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;

        /// <inheritdoc />
        public DateTime LocalDateTimeNow => DateTime.Now;

        /// <inheritdoc />
        public DateTime UtcDateTimeNow => DateTime.UtcNow;

#if NET
        /// <inheritdoc />
        public TimeOnly LocalNowTime => TimeOnly.FromDateTime(DateTime.Now);

        /// <inheritdoc />
        public TimeOnly UtcNowTime => TimeOnly.FromDateTime(DateTime.UtcNow);

        /// <inheritdoc />
        public DateOnly LocalNowDate => DateOnly.FromDateTime(DateTime.Now);

        /// <inheritdoc />
        public DateOnly UtcNowDate => DateOnly.FromDateTime(DateTime.UtcNow);
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
