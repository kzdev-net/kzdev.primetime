// Copyright (c) Kevin Zehrer
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using NodaTime;
using NodaTime.TimeZones;

namespace KZDev.PrimeTime;

//################################################################################
/// <summary>
///   Abstract partial base class that provides shared <see cref="IPrimeTime"/>
///   functionality, including system-local time-zone resolution and BCL-based
///   delay helpers.
/// </summary>
internal abstract partial class PrimeTimeBase
{
    //----------------------------------------------------------------------------
    /// <summary>
    ///   Gets the system default time zone for use as the local zone. Prefers the BCL
    ///   provider's GetSystemDefault() when the system default is mapped; otherwise
    ///   falls back to <see cref="BclDateTimeZone.ForSystemDefault"/>, which wraps
    ///   <see cref="TimeZoneInfo.Local"/> and succeeds even when the BCL provider
    ///   has no mapping (e.g. for some Windows zones like "Mid-Atlantic Standard Time").
    /// </summary>
    /// <returns>
    ///   A <see cref="DateTimeZone"/> representing the system default (local) time zone.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    ///   The system does not provide a time zone (can be thrown by the fallback).
    /// </exception>
    private static DateTimeZone GetSystemDefaultTimeZone ()
    {
        try
        {
            return DateTimeZoneProviders.Bcl.GetSystemDefault();
        }
        catch (DateTimeZoneNotFoundException)
        {
            return BclDateTimeZone.ForSystemDefault();
        }
    }
    //----------------------------------------------------------------------------

    #region Interface Implementations

    #region IPrimeTime — Delays (Duration)

    //----------------------------------------------------------------------------
    /// <inheritdoc />
    /// <remarks>
    ///   Durations greater than <see cref="TimeSpan.MaxValue"/> are automatically clamped
    ///   to <see cref="TimeSpan.MaxValue"/> before the underlying BCL delay call. In that
    ///   case, <see cref="Thread.Sleep(TimeSpan)"/> blocks for approximately 29,000 years,
    ///   so passing unreasonably large durations is still undesirable despite the clamping.
    /// </remarks>
    public void Sleep (Duration duration)
    {
        TimeSpan timeSpan = NodaDurationBclConversions.ToTimeSpanForDelay(duration);
        Thread.Sleep(timeSpan);
    }
    //----------------------------------------------------------------------------

    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public Task DelayAsync (Duration duration)
    {
        TimeSpan timeSpan = NodaDurationBclConversions.ToTimeSpanForDelay(duration);
        return Task.Delay(timeSpan);
    }
    //----------------------------------------------------------------------------

    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public Task DelayAsync (Duration duration, CancellationToken cancellationToken)
    {
        TimeSpan timeSpan = NodaDurationBclConversions.ToTimeSpanForDelay(duration);
        return Task.Delay(timeSpan, cancellationToken);
    }
    //----------------------------------------------------------------------------

    #endregion IPrimeTime — Delays (Duration)

    #region IPrimeTime — Time cancellation (Duration)

    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public TimeCancellationTokenSource GetTimeCancellationToken (Duration cancelAfter)
    {
        TimeSpan timeSpan = NodaDurationBclConversions.ToTimeSpanForCancellationToken(cancelAfter);
        CancellationTokenSource cancellationTokenSource = new(timeSpan);
        return new TimeCancellationTokenSource(cancellationTokenSource);
    }
    //----------------------------------------------------------------------------

    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public TimeCancellationTokenSource LinkTimeCancellationToken (Duration cancelAfter, CancellationToken cancellationToken)
    {
        TimeSpan timeSpan = NodaDurationBclConversions.ToTimeSpanForCancellationToken(cancelAfter);
        CancellationTokenSource timeLimitedCancellationTokenSource = new(timeSpan);
        CancellationTokenSource linkedCancellationTokenSource =
            CancellationTokenSource.CreateLinkedTokenSource(timeLimitedCancellationTokenSource.Token, cancellationToken);
        return new TimeCancellationTokenSource(linkedCancellationTokenSource, [timeLimitedCancellationTokenSource]);
    }
    //----------------------------------------------------------------------------

    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public TimeCancellationTokenSource LinkTimeCancellationToken (Duration cancelAfter, CancellationToken firstCancellationToken,
        CancellationToken secondCancellationToken)
    {
        TimeSpan timeSpan = NodaDurationBclConversions.ToTimeSpanForCancellationToken(cancelAfter);
        CancellationTokenSource timeLimitedCancellationTokenSource = new(timeSpan);
        CancellationTokenSource linkedCancellationTokenSource =
            CancellationTokenSource.CreateLinkedTokenSource(timeLimitedCancellationTokenSource.Token, firstCancellationToken, secondCancellationToken);
        return new TimeCancellationTokenSource(linkedCancellationTokenSource, [timeLimitedCancellationTokenSource]);
    }
    //----------------------------------------------------------------------------

    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public TimeCancellationTokenSource LinkTimeCancellationToken (Duration cancelAfter,
        params CancellationToken[] cancellationTokens)
    {
        TimeSpan timeSpan = NodaDurationBclConversions.ToTimeSpanForCancellationToken(cancelAfter);
        CancellationTokenSource timeLimitedCancellationTokenSource = new(timeSpan);
        CancellationTokenSource linkedCancellationTokenSource =
            CancellationTokenSource.CreateLinkedTokenSource([timeLimitedCancellationTokenSource.Token, .. cancellationTokens]);
        return new TimeCancellationTokenSource(linkedCancellationTokenSource, [timeLimitedCancellationTokenSource]);
    }
    //----------------------------------------------------------------------------

    #endregion IPrimeTime — Time cancellation (Duration)

    #endregion Interface Implementations
}
//################################################################################

