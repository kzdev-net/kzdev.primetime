// Copyright (c) Kevin Zehrer
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
using NodaTime;

namespace KZDev.PrimeTime.Testing;

//################################################################################
/// <summary>
///   Converts <see cref="Instant"/> and <see cref="Duration"/> values to <see cref="DateTimeOffset"/> and
///   <see cref="TimeSpan"/> for <see cref="PrimeTestClockTimedEvent"/> properties that share names across packages.
/// </summary>
internal static class PrimeTestClockEventNodaConversions
{
    //----------------------------------------------------------------------------
    /// <summary>
    ///   Converts a UTC <see cref="Instant"/> to <see cref="DateTimeOffset"/>.
    /// </summary>
    /// <param name="clockInstant">
    ///   The virtual instant in UTC.
    /// </param>
    /// <returns>
    ///   The equivalent <see cref="DateTimeOffset"/> with zero offset.
    /// </returns>
    internal static DateTimeOffset ToClockTime (Instant clockInstant) =>
        new(clockInstant.ToDateTimeUtc(), TimeSpan.Zero);
    //----------------------------------------------------------------------------

    //----------------------------------------------------------------------------
    /// <summary>
    ///   Converts an optional runner <see cref="Duration"/> to <see cref="TimeSpan"/>.
    /// </summary>
    /// <param name="runRateDuration">
    ///   The virtual time advanced per real second, or <see langword="null"/> when stopped.
    /// </param>
    /// <returns>
    ///   <see langword="null"/> when <paramref name="runRateDuration"/> is <see langword="null"/>;
    ///   otherwise the equivalent <see cref="TimeSpan"/>.
    /// </returns>
    internal static TimeSpan? ToRunRateTimeSpan (Duration? runRateDuration) =>
        runRateDuration is { } duration ? duration.ToTimeSpan() : null;
    //----------------------------------------------------------------------------
}
//################################################################################
