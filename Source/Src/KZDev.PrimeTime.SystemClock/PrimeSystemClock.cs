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
    ///   LinkTimeCancellationToken) delegate to the BCL (Thread.Sleep, Task.Delay,
    ///   CancellationTokenSource with a timer), so they use system time rather than the provider's
    ///   time. For deterministic tests, use the test clock implementation from Phase 12.
    /// </remarks>
    public sealed class PrimeSystemClock : IPrimeSystemClock
    {
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
        public void Sleep (TimeSpan sleepTime)
        {
            Thread.Sleep(sleepTime);
        }

        /// <inheritdoc />
        public void Sleep (int sleepMilliseconds)
        {
            Thread.Sleep(sleepMilliseconds);
        }

        /// <inheritdoc />
        public Task DelayAsync (TimeSpan delayTime)
        {
            return Task.Delay(delayTime);
        }

        /// <inheritdoc />
        public Task DelayAsync (int millisecondsDelay)
        {
            return Task.Delay(millisecondsDelay);
        }

        /// <inheritdoc />
        public Task DelayAsync (TimeSpan delayTime, CancellationToken cancellationToken)
        {
            return Task.Delay(delayTime, cancellationToken);
        }

        /// <inheritdoc />
        public Task DelayAsync (int millisecondsDelay, CancellationToken cancellationToken)
        {
            return Task.Delay(millisecondsDelay, cancellationToken);
        }

        /// <inheritdoc />
        public CancellationTokenSource GetTimeCancellationToken (TimeSpan cancelTime)
        {
            return new CancellationTokenSource(cancelTime);
        }

        /// <inheritdoc />
        public CancellationTokenSource GetTimeCancellationToken (int cancelMilliseconds)
        {
            return new CancellationTokenSource(cancelMilliseconds);
        }

        /// <inheritdoc />
        /// <remarks>
        ///   The returned linked source is the only object the caller disposes. The internal
        ///   time-based source is disposed automatically when the linked token is canceled.
        /// </remarks>
        public CancellationTokenSource LinkTimeCancellationToken (TimeSpan cancelTime, CancellationToken cancellationToken)
        {
            CancellationTokenSource timeCts = new(cancelTime);
            CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(timeCts.Token, cancellationToken);
            linkedCts.Token.Register(static state => ((CancellationTokenSource)state!).Dispose(), timeCts);
            return linkedCts;
        }

        /// <inheritdoc />
        /// <remarks>
        ///   The returned linked source is the only object the caller disposes. The internal
        ///   time-based source is disposed automatically when the linked token is canceled.
        /// </remarks>
        public CancellationTokenSource LinkTimeCancellationToken (int cancelMilliseconds, CancellationToken cancellationToken)
        {
            CancellationTokenSource timeCts = new(cancelMilliseconds);
            CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(timeCts.Token, cancellationToken);
            linkedCts.Token.Register(static state => ((CancellationTokenSource)state!).Dispose(), timeCts);
            return linkedCts;
        }

        /// <inheritdoc />
        /// <remarks>
        ///   The returned linked source is the only object the caller disposes. The internal
        ///   time-based source is not disposed when the returned source is disposed (BCL limitation).
        /// </remarks>
        public CancellationTokenSource LinkTimeCancellationToken (int cancelMilliseconds,
            CancellationToken token1,
            CancellationToken token2)
        {
            CancellationTokenSource timeCts = new(cancelMilliseconds);
            return CancellationTokenSource.CreateLinkedTokenSource(timeCts.Token, token1, token2);
        }

        /// <inheritdoc />
        /// <remarks>
        ///   The returned linked source is the only object the caller disposes. The internal
        ///   time-based source is not disposed when the returned source is disposed (BCL limitation).
        /// </remarks>
        public CancellationTokenSource LinkTimeCancellationToken (TimeSpan cancelTime,
            CancellationToken token1,
            CancellationToken token2)
        {
            CancellationTokenSource timeCts = new(cancelTime);
            return CancellationTokenSource.CreateLinkedTokenSource(timeCts.Token, token1, token2);
        }

        /// <inheritdoc />
        /// <remarks>
        ///   The returned linked source is the only object the caller disposes. The internal
        ///   time-based source is not disposed when the returned source is disposed (BCL limitation).
        /// </remarks>
        public CancellationTokenSource LinkTimeCancellationToken (TimeSpan cancelTime,
            params CancellationToken[] cancellationTokens)
        {
            CancellationTokenSource timeCts = new(cancelTime);
            CancellationToken[] all = new CancellationToken[cancellationTokens.Length + 1];
            all[0] = timeCts.Token;
            Array.Copy(cancellationTokens, 0, all, 1, cancellationTokens.Length);
            return CancellationTokenSource.CreateLinkedTokenSource(all);
        }

        /// <inheritdoc />
        /// <remarks>
        ///   The returned linked source is the only object the caller disposes. The internal
        ///   time-based source is not disposed when the returned source is disposed (BCL limitation).
        /// </remarks>
        public CancellationTokenSource LinkTimeCancellationToken (int cancelMilliseconds,
            params CancellationToken[] cancellationTokens)
        {
            CancellationTokenSource timeCts = new(cancelMilliseconds);
            CancellationToken[] all = new CancellationToken[cancellationTokens.Length + 1];
            all[0] = timeCts.Token;
            Array.Copy(cancellationTokens, 0, all, 1, cancellationTokens.Length);
            return CancellationTokenSource.CreateLinkedTokenSource(all);
        }

        #endregion IPrimeTime Implementation
    }
    //################################################################################
}
