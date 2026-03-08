namespace KZDev.PrimeTime
{
    //################################################################################
    /// <summary>
    /// The interface for the PrimeTime library for time passing and timer services
    /// </summary>
    public interface IPrimeTime
    {
        #region Delays

        //--------------------------------------------------------------------------------
        /// <summary>
        /// Suspends the current thread for the specified amount of time.
        /// </summary>
        /// <param name="sleepTime">
        /// The amount of time for which the thread is suspended. If the value of the sleepTime
        /// argument is Zero, the thread relinquishes the remainder of its time slice to any 
        /// thread of equal priority that is ready to run. If there are no other threads of
        /// equal priority that are ready to run, execution of the current thread
        /// is not suspended.
        /// </param>
        /// <seealso cref="Thread.Sleep(TimeSpan)"/>
        void Sleep (TimeSpan sleepTime);
        //--------------------------------------------------------------------------------
        /// <summary>
        /// Suspends the current thread for the specified number of milliseconds.
        /// </summary>
        /// <param name="sleepMilliseconds">
        /// The number of milliseconds for which the thread is suspended. If the value of the 
        /// sleepMilliseconds argument is zero, the thread relinquishes the remainder
        /// of its time slice to any thread of equal priority that is ready to run. 
        /// If there are no other threads of equal priority that are ready to run, 
        /// execution of the current thread is not suspended.
        /// </param>
        /// <seealso cref="Thread.Sleep(int)"/>
        void Sleep (int sleepMilliseconds);
        //--------------------------------------------------------------------------------
        /// <summary>
        /// Creates a task that completes after a specified time interval.
        /// </summary>
        /// <param name="delayTime">
        /// The time span to wait before completing the returned task, or 
        /// Timeout.InfiniteTimeSpan to wait indefinitely.
        /// </param>
        Task DelayAsync (TimeSpan delayTime);
        //--------------------------------------------------------------------------------
        /// <summary>
        /// Creates a task that completes after a specified number of milliseconds.
        /// </summary>
        /// <param name="millisecondsDelay">
        /// The number of milliseconds to wait before completing the returned task, or -1 to wait indefinitely.
        /// </param>
        Task DelayAsync (int millisecondsDelay);
        //--------------------------------------------------------------------------------
        /// <summary>
        /// Creates a task that completes after a specified time interval.
        /// </summary>
        /// <param name="delayTime">
        /// The time span to wait before completing the returned task, or 
        /// Timeout.InfiniteTimeSpan to wait indefinitely.
        /// </param>
        /// <param name="cancellationToken">
        /// A cancellation token to observe while waiting for the task to complete.
        /// </param>
        Task DelayAsync (TimeSpan delayTime, CancellationToken cancellationToken);
        //--------------------------------------------------------------------------------
        /// <summary>
        /// Creates a task that completes after a specified number of milliseconds.
        /// </summary>
        /// <param name="millisecondsDelay">
        /// The number of milliseconds to wait before completing the returned task, or -1 to wait indefinitely.
        /// </param>
        /// <param name="cancellationToken">
        /// A cancellation token to observe while waiting for the task to complete.
        /// </param>
        Task DelayAsync (int millisecondsDelay, CancellationToken cancellationToken);
        //--------------------------------------------------------------------------------

        #endregion Delays

        #region CancellationToken

        //--------------------------------------------------------------------------------
        /// <summary>
        ///   Returns a cancellation token source whose token expires after the specified time.
        ///   The caller is responsible for disposing the returned source when no longer needed.
        /// </summary>
        /// <param name="cancelTime">
        ///   The time to wait before cancelling the token.
        /// </param>
        /// <returns>
        ///   A cancellation token source that will cancel after the specified time. Caller must dispose.
        /// </returns>
        CancellationTokenSource GetTimeCancellationToken (TimeSpan cancelTime);
        //--------------------------------------------------------------------------------
        /// <summary>
        ///   Returns a cancellation token source whose token expires after the specified time.
        ///   The caller is responsible for disposing the returned source when no longer needed.
        /// </summary>
        /// <param name="cancelMilliseconds">
        ///   The number of milliseconds to wait before cancelling the token.
        /// </param>
        /// <returns>
        ///   A cancellation token source that will cancel after the specified time. Caller must dispose.
        /// </returns>
        CancellationTokenSource GetTimeCancellationToken (int cancelMilliseconds);
        //--------------------------------------------------------------------------------
        /// <summary>
        ///   Returns a cancellation token source whose token is cancelled when either the
        ///   specified time elapses or the given cancellation token is cancelled. The caller
        ///   is responsible for disposing the returned source when no longer needed.
        ///   Implementations create an internal time-based source that is not disposed when
        ///   the returned linked source is disposed (BCL limitation); be aware of this when
        ///   creating many linked time tokens.
        /// </summary>
        /// <param name="cancelTime">
        ///   The time to wait before cancelling the token.
        /// </param>
        /// <param name="cancellationToken">
        ///   The cancellation token to link; when it is cancelled, the returned source is cancelled.
        /// </param>
        /// <returns>
        ///   A cancellation token source. Caller must dispose.
        /// </returns>
        CancellationTokenSource LinkTimeCancellationToken (TimeSpan cancelTime, CancellationToken cancellationToken);
        //--------------------------------------------------------------------------------
        /// <summary>
        ///   Returns a cancellation token source whose token is cancelled when either the
        ///   specified time elapses or the given cancellation token is cancelled. The caller
        ///   is responsible for disposing the returned source when no longer needed.
        ///   Implementations create an internal time-based source that is not disposed when
        ///   the returned linked source is disposed (BCL limitation); be aware of this when
        ///   creating many linked time tokens.
        /// </summary>
        /// <param name="cancelMilliseconds">
        ///   The number of milliseconds to wait before cancelling the token.
        /// </param>
        /// <param name="cancellationToken">
        ///   The cancellation token to link; when it is cancelled, the returned source is cancelled.
        /// </param>
        /// <returns>
        ///   A cancellation token source. Caller must dispose.
        /// </returns>
        CancellationTokenSource LinkTimeCancellationToken (int cancelMilliseconds, CancellationToken cancellationToken);
        //--------------------------------------------------------------------------------
        /// <summary>
        ///   Returns a cancellation token source whose token is cancelled when the specified
        ///   time elapses or any of the given tokens is cancelled. The caller is responsible
        ///   for disposing the returned source when no longer needed. Implementations create
        ///   an internal time-based source that is not disposed when the returned linked source
        ///   is disposed (BCL limitation); be aware of this when creating many linked time tokens.
        /// </summary>
        /// <param name="cancelMilliseconds">
        ///   The number of milliseconds to wait before cancelling the token.
        /// </param>
        /// <param name="token1">
        ///   The first cancellation token to link.
        /// </param>
        /// <param name="token2">
        ///   The second cancellation token to link.
        /// </param>
        /// <returns>
        ///   A cancellation token source. Caller must dispose.
        /// </returns>
        CancellationTokenSource LinkTimeCancellationToken (int cancelMilliseconds,
            CancellationToken token1, CancellationToken token2);
        //--------------------------------------------------------------------------------
        /// <summary>
        ///   Returns a cancellation token source whose token is cancelled when the specified
        ///   time elapses or any of the given tokens is cancelled. The caller is responsible
        ///   for disposing the returned source when no longer needed. Implementations create
        ///   an internal time-based source that is not disposed when the returned linked source
        ///   is disposed (BCL limitation); be aware of this when creating many linked time tokens.
        /// </summary>
        /// <param name="cancelTime">
        ///   The time to wait before cancelling the token.
        /// </param>
        /// <param name="token1">
        ///   The first cancellation token to link.
        /// </param>
        /// <param name="token2">
        ///   The second cancellation token to link.
        /// </param>
        /// <returns>
        ///   A cancellation token source. Caller must dispose.
        /// </returns>
        CancellationTokenSource LinkTimeCancellationToken (TimeSpan cancelTime,
            CancellationToken token1, CancellationToken token2);
        //--------------------------------------------------------------------------------
        /// <summary>
        ///   Returns a cancellation token source whose token is cancelled when the specified
        ///   time elapses or any of the given tokens is cancelled. The caller is responsible
        ///   for disposing the returned source when no longer needed. Implementations create
        ///   an internal time-based source that is not disposed when the returned linked source
        ///   is disposed (BCL limitation); be aware of this when creating many linked time tokens.
        /// </summary>
        /// <param name="cancelTime">
        ///   The time to wait before cancelling the token.
        /// </param>
        /// <param name="cancellationTokens">
        ///   The cancellation tokens to link.
        /// </param>
        /// <returns>
        ///   A cancellation token source. Caller must dispose.
        /// </returns>
        CancellationTokenSource LinkTimeCancellationToken (TimeSpan cancelTime,
            params CancellationToken[] cancellationTokens);
        //--------------------------------------------------------------------------------
        /// <summary>
        ///   Returns a cancellation token source whose token is cancelled when the specified
        ///   time elapses or any of the given tokens is cancelled. The caller is responsible
        ///   for disposing the returned source when no longer needed. Implementations create
        ///   an internal time-based source that is not disposed when the returned linked source
        ///   is disposed (BCL limitation); be aware of this when creating many linked time tokens.
        /// </summary>
        /// <param name="cancelMilliseconds">
        ///   The number of milliseconds to wait before cancelling the token.
        /// </param>
        /// <param name="cancellationTokens">
        ///   The cancellation tokens to link.
        /// </param>
        /// <returns>
        ///   A cancellation token source. Caller must dispose.
        /// </returns>
        CancellationTokenSource LinkTimeCancellationToken (int cancelMilliseconds,
            params CancellationToken[] cancellationTokens);
        //--------------------------------------------------------------------------------

        #endregion CancellationToken
    }
    //################################################################################
}
