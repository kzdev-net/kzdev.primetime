// Copyright (c) Kevin Zehrer. All rights reserved.
// This file is part of the PrimeTime project.

using System.Diagnostics;
using NodaTime;
using NodaTime.TimeZones;

namespace KZDev.PrimeTime;

//################################################################################
/// <summary>
///   NodaTime virtual instant storage, zone mapping, and Noda-specific surface for
///   <see cref="PrimeTestTimeBase"/>.
/// </summary>
public abstract partial class PrimeTestTimeBase
{
    //----------------------------------------------------------------------------
    /// <summary>
    ///   The time zone used for local zoned date and time in virtual time.
    /// </summary>
    protected readonly DateTimeZone _zone;

    /// <summary>
    ///   The current virtual instant on the UTC timeline; read and updated under the shared gate lock.
    /// </summary>
    protected Instant _now;
    //----------------------------------------------------------------------------

    #region Constructors/Finalizers

    //----------------------------------------------------------------------------
    /// <summary>
    ///   Initializes a new instance with virtual time set to the current system instant.
    /// </summary>
    protected PrimeTestTimeBase ()
    {
        _now = SystemClock.Instance.GetCurrentInstant();
        _zone = GetSystemDefaultTimeZone();
    }
    //----------------------------------------------------------------------------

    //----------------------------------------------------------------------------
    /// <summary>
    ///   Initializes a new instance with the specified initial instant and system default zone.
    /// </summary>
    /// <param name="initialInstant">The initial virtual instant.</param>
    protected PrimeTestTimeBase (Instant initialInstant)
    {
        _now = initialInstant;
        _zone = GetSystemDefaultTimeZone();
    }
    //----------------------------------------------------------------------------

    //----------------------------------------------------------------------------
    /// <summary>
    ///   Initializes a new instance with the specified initial instant and time zone.
    /// </summary>
    /// <param name="initialInstant">The initial virtual instant.</param>
    /// <param name="zone">The zone used for local "now" values.</param>
    /// <exception cref="ArgumentNullException">
    ///   <paramref name="zone"/> is <c>null</c>.
    /// </exception>
    protected PrimeTestTimeBase (Instant initialInstant, DateTimeZone zone)
    {
        _now = initialInstant;
        _zone = zone ?? throw new ArgumentNullException(nameof(zone));
    }
    //----------------------------------------------------------------------------

    #endregion Constructors/Finalizers

    //----------------------------------------------------------------------------
    /// <summary>
    ///   Resolves the system default time zone for local projections, using the BCL provider or
    ///   <see cref="BclDateTimeZone.ForSystemDefault"/> when the provider has no mapping.
    /// </summary>
    /// <returns>
    ///   A <see cref="DateTimeZone"/> representing the system default local zone.
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

    //----------------------------------------------------------------------------
    /// <summary>
    ///   Reads the virtual instant as a UTC <see cref="DateTimeOffset"/>. The caller must hold the gate lock.
    /// </summary>
    /// <returns>
    ///   The current virtual time with zero offset (UTC).
    /// </returns>
    protected partial DateTimeOffset ReadVirtualUtcNowLocked () =>
        new(_now.ToDateTimeUtc(), TimeSpan.Zero);
    //----------------------------------------------------------------------------

    #region Interface Implementations

    #region IPrimeTime / IPrimeClock — Delays (Duration)

    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public void Sleep (Duration duration) =>
        Sleep(NodaDurationBclConversions.ToTimeSpanForDelay(duration));
    //----------------------------------------------------------------------------

    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public Task DelayAsync (Duration duration) =>
        DelayAsync(NodaDurationBclConversions.ToTimeSpanForDelay(duration));
    //----------------------------------------------------------------------------

    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public Task DelayAsync (Duration duration, CancellationToken cancellationToken) =>
        DelayAsync(NodaDurationBclConversions.ToTimeSpanForDelay(duration), cancellationToken);
    //----------------------------------------------------------------------------

    #endregion IPrimeTime / IPrimeClock — Delays (Duration)

    #region IPrimeTime / IPrimeClock — Time cancellation (Duration)

    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public TimeCancellationTokenSource GetTimeCancellationToken (Duration cancelAfter) =>
        GetTimeCancellationToken(NodaDurationBclConversions.ToTimeSpanForCancellationToken(cancelAfter));
    //----------------------------------------------------------------------------

    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public TimeCancellationTokenSource LinkTimeCancellationToken (Duration cancelAfter, CancellationToken cancellationToken) =>
        LinkTimeCancellationToken(NodaDurationBclConversions.ToTimeSpanForCancellationToken(cancelAfter), cancellationToken);
    //----------------------------------------------------------------------------

    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public TimeCancellationTokenSource LinkTimeCancellationToken (Duration cancelAfter, CancellationToken firstCancellationToken,
        CancellationToken secondCancellationToken) =>
        LinkTimeCancellationToken(NodaDurationBclConversions.ToTimeSpanForCancellationToken(cancelAfter), firstCancellationToken, secondCancellationToken);
    //----------------------------------------------------------------------------

    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public TimeCancellationTokenSource LinkTimeCancellationToken (Duration cancelAfter,
        params CancellationToken[] cancellationTokens) =>
        LinkTimeCancellationToken(NodaDurationBclConversions.ToTimeSpanForCancellationToken(cancelAfter), cancellationTokens);
    //----------------------------------------------------------------------------

    #endregion IPrimeTime / IPrimeClock — Time cancellation (Duration)

    #endregion Interface Implementations
}
//################################################################################
