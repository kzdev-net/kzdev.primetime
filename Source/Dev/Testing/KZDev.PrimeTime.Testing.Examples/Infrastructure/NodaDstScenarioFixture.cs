// Copyright (c) Kevin Zehrer
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using KZDev.PrimeTime;

using NodaTime;

namespace KZDev.PrimeTime.Testing.Examples.Infrastructure;

/// <summary>
/// Provides a fixed IANA timezone for deterministic DST demonstrations independent of the host's
/// <see cref="TimeZoneInfo.Local"/> configuration.
/// </summary>
public static class NodaDstScenarioFixture
{
    /// <summary>
    /// Gets a stable North-American eastern timezone from the TZDB provider.
    /// </summary>
    public static DateTimeZone UsEastern { get; } = DateTimeZoneProviders.Tzdb["America/New_York"];

    /// <summary>
    /// Creates a <see cref="PrimeTestClock"/> pinned to <see cref="UsEastern"/> at the given instant.
    /// </summary>
    /// <param name="instant">The initial virtual UTC instant.</param>
    /// <returns>A new test clock instance.</returns>
    public static PrimeTestClock CreateClock (Instant instant) =>
        new(instant, UsEastern);
}
