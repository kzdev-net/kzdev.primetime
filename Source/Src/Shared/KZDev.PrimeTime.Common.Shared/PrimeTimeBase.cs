// Copyright (c) Kevin Zehrer
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

#if SYSTEMCLOCK
namespace KZDev.SystemClock.PrimeTime;
#else
namespace KZDev.PrimeTime;
#endif

#if SYSTEMCLOCK
/// <content>
///   BCL <see cref="IPrimeTime"/> and timer registration members for <see cref="PrimeTime"/>
///   in <c>KZDev.SystemClock.PrimeTime</c> (TimeProvider-based).
/// </content>
#else
//################################################################################
/// <content>
///   BCL <see cref="IPrimeTime"/> and timer registration members for <see cref="PrimeTime"/>
///   in <c>KZDev.PrimeTime</c> (NodaTime-based).
/// </content>
#endif
internal abstract partial class PrimeTimeBase : IPrimeTime
{
    #region Interface Implementations

    #region IPrimeTime Implementation

    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public void Sleep (TimeSpan sleepTime)
    {
        Thread.Sleep(sleepTime);
    }
    //----------------------------------------------------------------------------

    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public void Sleep (int sleepMilliseconds)
    {
        Thread.Sleep(sleepMilliseconds);
    }
    //----------------------------------------------------------------------------

    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public Task DelayAsync (TimeSpan delayTime)
    {
        return Task.Delay(delayTime);
    }
    //----------------------------------------------------------------------------

    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public Task DelayAsync (int millisecondsDelay)
    {
        return Task.Delay(millisecondsDelay);
    }
    //----------------------------------------------------------------------------

    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public Task DelayAsync (TimeSpan delayTime, CancellationToken cancellationToken)
    {
        return Task.Delay(delayTime, cancellationToken);
    }
    //----------------------------------------------------------------------------

    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public Task DelayAsync (int millisecondsDelay, CancellationToken cancellationToken)
    {
        return Task.Delay(millisecondsDelay, cancellationToken);
    }
    //----------------------------------------------------------------------------

    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public TimeCancellationTokenSource GetTimeCancellationToken (TimeSpan cancelTime)
    {
        CancellationTokenSource cts = new(cancelTime);
        return new TimeCancellationTokenSource(cts);
    }
    //----------------------------------------------------------------------------

    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public TimeCancellationTokenSource GetTimeCancellationToken (int cancelMilliseconds)
    {
        CancellationTokenSource cts = new(cancelMilliseconds);
        return new TimeCancellationTokenSource(cts);
    }
    //----------------------------------------------------------------------------

    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public TimeCancellationTokenSource LinkTimeCancellationToken (int cancelMilliseconds, CancellationToken token1,
        CancellationToken token2)
    {
        CancellationTokenSource timeCts = new(cancelMilliseconds);
        CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(timeCts.Token, token1, token2);
        return new TimeCancellationTokenSource(linkedCts, [timeCts]);
    }
    //----------------------------------------------------------------------------

    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public TimeCancellationTokenSource LinkTimeCancellationToken (TimeSpan cancelTime, CancellationToken token1,
        CancellationToken token2)
    {
        CancellationTokenSource timeCts = new(cancelTime);
        CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(timeCts.Token, token1, token2);
        return new TimeCancellationTokenSource(linkedCts, [timeCts]);
    }
    //----------------------------------------------------------------------------

    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public TimeCancellationTokenSource LinkTimeCancellationToken (TimeSpan cancelTime, CancellationToken cancellationToken)
    {
        CancellationTokenSource timeCts = new(cancelTime);
        CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(timeCts.Token, cancellationToken);
        return new TimeCancellationTokenSource(linkedCts, [timeCts]);
    }
    //----------------------------------------------------------------------------

    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public TimeCancellationTokenSource LinkTimeCancellationToken (int cancelMilliseconds, CancellationToken cancellationToken)
    {
        CancellationTokenSource timeCts = new(cancelMilliseconds);
        CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(timeCts.Token, cancellationToken);
        return new TimeCancellationTokenSource(linkedCts, [timeCts]);
    }
    //----------------------------------------------------------------------------

    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public TimeCancellationTokenSource LinkTimeCancellationToken (TimeSpan cancelTime, params CancellationToken[] cancellationTokens)
    {
        CancellationTokenSource timeCts = new(cancelTime);
        CancellationToken[] all = new CancellationToken[cancellationTokens.Length + 1];
        all[0] = timeCts.Token;
        Array.Copy(cancellationTokens, 0, all, 1, cancellationTokens.Length);
        CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(all);
        return new TimeCancellationTokenSource(linkedCts, [timeCts]);
    }
    //----------------------------------------------------------------------------

    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public TimeCancellationTokenSource LinkTimeCancellationToken (int cancelMilliseconds,
        params CancellationToken[] cancellationTokens)
    {
        CancellationTokenSource timeCts = new(cancelMilliseconds);
        CancellationToken[] all = new CancellationToken[cancellationTokens.Length + 1];
        all[0] = timeCts.Token;
        Array.Copy(cancellationTokens, 0, all, 1, cancellationTokens.Length);
        CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(all);
        return new TimeCancellationTokenSource(linkedCts, [timeCts]);
    }
    //----------------------------------------------------------------------------

    #endregion IPrimeTime Implementation

    #endregion Interface Implementations
}
//################################################################################
