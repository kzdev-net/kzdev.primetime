// Copyright (c) Kevin Zehrer
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using KZDev.PrimeTime;

namespace KZDev.SystemClock.PrimeTime;

/// <summary>
///   Extension methods for obtaining a <see cref="TimeProvider"/> from PrimeTime BCL clocks.
/// </summary>
public static class PrimeClockTimeProviderExtensions
{
    /// <summary>
    ///   Returns a <see cref="TimeProvider"/> that uses the given PrimeTime BCL clock
    ///   for <see cref="TimeProvider.GetUtcNow"/>, <see cref="TimeProvider.GetLocalNow"/>,
    ///   and <see cref="TimeProvider.CreateTimer"/>.
    /// </summary>
    /// <param name="clock">
    ///   The PrimeTime BCL clock (e.g. <see cref="PrimeClock"/> or
    ///   <see cref="PrimeTestClock"/>).
    /// </param>
    /// <returns>
    ///   A <see cref="TimeProvider"/> backed by <paramref name="clock"/>. When
    ///   <paramref name="clock"/> is a test clock, advancing virtual time with
    ///   <see cref="IPrimeTestClock.Advance"/> or <see cref="IPrimeTestClock.RunFor"/>
    ///   drives the returned provider's time and timers deterministically.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    ///   <paramref name="clock"/> is <c>null</c>.
    /// </exception>
    public static TimeProvider ToTimeProvider (this IPrimeClock clock)
    {
        if (clock == null)
            throw new ArgumentNullException(nameof(clock));
        return new PrimeClockTimeProviderAdapter(clock);
    }
}
