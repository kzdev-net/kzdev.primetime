// Copyright (c) Kevin Zehrer
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

// Phase 14: Extension methods to obtain a TimeProvider from a PrimeTime clock.

namespace KZDev.PrimeTime
{
    /// <summary>
    ///   Extension methods for obtaining a <see cref="TimeProvider"/> from PrimeTime system clocks.
    /// </summary>
    public static class PrimeSystemClockTimeProviderExtensions
    {
        /// <summary>
        ///   Returns a <see cref="TimeProvider"/> that uses the given PrimeTime system clock
        ///   for <see cref="TimeProvider.GetUtcNow"/>, <see cref="TimeProvider.GetLocalNow"/>,
        ///   and <see cref="TimeProvider.CreateTimer"/>.
        /// </summary>
        /// <param name="clock">
        ///   The PrimeTime system clock (e.g. <see cref="PrimeSystemClock"/> or
        ///   <see cref="PrimeTestSystemClock"/>).
        /// </param>
        /// <returns>
        ///   A <see cref="TimeProvider"/> backed by <paramref name="clock"/>. When
        ///   <paramref name="clock"/> is a test clock, advancing virtual time with
        ///   <see cref="IPrimeTestSystemClock.Advance"/> or <see cref="IPrimeTestSystemClock.RunFor"/>
        ///   drives the returned provider's time and timers deterministically.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        ///   <paramref name="clock"/> is <c>null</c>.
        /// </exception>
        public static TimeProvider ToTimeProvider (this IPrimeSystemClock clock)
        {
            if (clock == null)
                throw new ArgumentNullException(nameof(clock));
            return new PrimeSystemClockTimeProviderAdapter(clock);
        }
    }
}
