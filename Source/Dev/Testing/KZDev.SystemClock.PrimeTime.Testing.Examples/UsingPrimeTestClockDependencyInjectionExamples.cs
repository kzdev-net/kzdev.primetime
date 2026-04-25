// Copyright (c) Kevin Zehrer
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

using KZDev.SystemClock.PrimeTime;

using Microsoft.Extensions.DependencyInjection;

namespace KZDev.SystemClock.PrimeTime.Testing.Examples;

/// <summary>
/// Demonstrates registering <see cref="PrimeTestClock"/> via
/// <see cref="PrimeTestClockServiceCollectionExtensions.AddPrimeTestClock(Microsoft.Extensions.DependencyInjection.IServiceCollection)"/>
/// so application code resolves a shared virtual clock.
/// </summary>
public sealed class UsingPrimeTestClockDependencyInjectionExamples
{
    /// <summary>
    /// Verifies DI consumers receive the same <see cref="PrimeTestClock"/> instance for
    /// <see cref="IPrimeTestClock"/>, <see cref="IPrimeClock"/>, and <see cref="IPrimeTime"/>.
    /// </summary>
    [Fact]
    public void DependencyInjection_AddPrimeTestClock_ResolvesSingletonAbstractions ()
    {
        ServiceCollection services = new();
        services.AddPrimeTestClock();
        services.AddSingleton<VirtualUtcTimestampService>();

        using ServiceProvider provider = services.BuildServiceProvider();
        VirtualUtcTimestampService consumer = provider.GetRequiredService<VirtualUtcTimestampService>();
        IPrimeTestClock testClock = provider.GetRequiredService<IPrimeTestClock>();
        IPrimeClock clock = provider.GetRequiredService<IPrimeClock>();

        DateTimeOffset marker = new DateTimeOffset(2025, 4, 1, 15, 0, 0, TimeSpan.Zero);
        testClock.SetTime(marker);
        consumer.ReadUtc().Should().Be(marker);
        clock.UtcNowDateTimeOffset.Should().Be(marker);
        consumer.ReadUtc().Should().Be(testClock.UtcNowDateTimeOffset);
    }

    /// <summary>
    /// Sample consumer that reads the virtual UTC time from an injected <see cref="IPrimeClock"/>.
    /// </summary>
    private sealed class VirtualUtcTimestampService
    {
        private readonly IPrimeClock _clock;

        /// <summary>
        /// Initializes a new instance of the <see cref="VirtualUtcTimestampService"/> class.
        /// </summary>
        /// <param name="clock">The prime clock abstraction.</param>
        public VirtualUtcTimestampService (IPrimeClock clock)
        {
            _clock = clock;
        }

        /// <summary>
        /// Reads the clock's current virtual UTC time.
        /// </summary>
        /// <returns>The virtual UTC <see cref="DateTimeOffset"/>.</returns>
        public DateTimeOffset ReadUtc () =>
            _clock.UtcNowDateTimeOffset;
    }
}
