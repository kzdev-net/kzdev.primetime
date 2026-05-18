// Copyright (c) Kevin Zehrer
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Diagnostics;

using NodaTime;
using NodaTime.TimeZones;

namespace KZDev.PrimeTime.Testing;

//################################################################################
/// <summary>
///   NodaTime virtual instant storage, zone mapping, and Noda-specific surface for
///   <see cref="PrimeTestTimeBase"/>.
/// </summary>
public abstract partial class PrimeTestTimeBase
{
    //----------------------------------------------------------------------------
    /// <summary>
    ///   The current virtual instant on the UTC timeline; read and updated under the shared gate lock.
    /// </summary>
    internal Instant Now { [DebuggerStepThrough] get; [DebuggerStepThrough] set; }

    /// <summary>
    ///   The time zone used for local zoned date and time in virtual time.
    /// </summary>
    internal DateTimeZone TimeZone { [DebuggerStepThrough] get; }
    //----------------------------------------------------------------------------

    #region Constructors/Finalizers

    //----------------------------------------------------------------------------
    /// <summary>
    ///   Initializes a new instance with virtual time set to the current system instant.
    /// </summary>
    internal PrimeTestTimeBase ()
    {
        Now = SystemClock.Instance.GetCurrentInstant();
        TimeZone = GetSystemDefaultTimeZone();
    }
    //----------------------------------------------------------------------------

    //----------------------------------------------------------------------------
    /// <summary>
    ///   Initializes a new instance with the specified initial instant and system default zone.
    /// </summary>
    /// <param name="initialInstant">The initial virtual instant.</param>
    internal PrimeTestTimeBase (Instant initialInstant)
    {
        Now = initialInstant;
        TimeZone = GetSystemDefaultTimeZone();
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
    internal PrimeTestTimeBase (Instant initialInstant, DateTimeZone zone)
    {
        Now = initialInstant;
        TimeZone = zone ?? throw new ArgumentNullException(nameof(zone));
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
    /// <summary>
    ///   Reads the virtual instant as a UTC <see cref="DateTimeOffset"/>. The caller must hold the gate lock.
    /// </summary>
    /// <returns>
    ///   The current virtual time with zero offset (UTC).
    /// </returns>
    internal partial DateTimeOffset ReadVirtualUtcNowLocked () =>
        new(Now.ToDateTimeUtc(), TimeSpan.Zero);
    //----------------------------------------------------------------------------

    #region Interface Implementations

    #region IPrimeTime / IPrimeClock — Delays (Duration)

    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public void Sleep (Duration duration) =>
        Sleep(NodaDurationBclConversion.ToTimeSpanForDelay(duration));
    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public Task DelayAsync (Duration duration) =>
        DelayAsync(NodaDurationBclConversion.ToTimeSpanForDelay(duration));
    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public Task DelayAsync (Duration duration, CancellationToken cancellationToken) =>
        DelayAsync(NodaDurationBclConversion.ToTimeSpanForDelay(duration), cancellationToken);
    //----------------------------------------------------------------------------

    #endregion IPrimeTime / IPrimeClock — Delays (Duration)

    #region IPrimeTime / IPrimeClock — Time cancellation (Duration)

    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public TimeCancellationTokenSource GetTimeCancellationToken (Duration cancelAfter) =>
        GetTimeCancellationToken(NodaDurationBclConversion.ToTimeSpanForCancellationToken(cancelAfter));
    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public TimeCancellationTokenSource LinkTimeCancellationToken (Duration cancelAfter, CancellationToken cancellationToken) =>
        LinkTimeCancellationToken(NodaDurationBclConversion.ToTimeSpanForCancellationToken(cancelAfter), cancellationToken);
    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public TimeCancellationTokenSource LinkTimeCancellationToken (Duration cancelAfter, CancellationToken firstCancellationToken,
        CancellationToken secondCancellationToken) =>
        LinkTimeCancellationToken(NodaDurationBclConversion.ToTimeSpanForCancellationToken(cancelAfter), firstCancellationToken, secondCancellationToken);
    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public TimeCancellationTokenSource LinkTimeCancellationToken (Duration cancelAfter,
        params CancellationToken[] cancellationTokens) =>
        LinkTimeCancellationToken(NodaDurationBclConversion.ToTimeSpanForCancellationToken(cancelAfter), cancellationTokens);
    //----------------------------------------------------------------------------

    #endregion IPrimeTime / IPrimeClock — Time cancellation (Duration)

    #endregion Interface Implementations
}
//################################################################################
