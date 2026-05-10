// Copyright (c) Kevin Zehrer
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

using KZDev.PrimeTime;

using Microsoft.Extensions.DependencyInjection;

using NodaTime;

namespace KZDev.PrimeTime.Testing.Examples;

/// <summary>
/// Demonstrates registering <see cref="PrimeTestClock"/> via
/// <see cref="PrimeTestClockServiceCollectionExtensions.AddPrimeTestClock(Microsoft.Extensions.DependencyInjection.IServiceCollection)"/>
/// so application code resolves a shared virtual clock.
/// </summary>
public sealed class UsingPrimeTestClockDependencyInjectionExamples
{
    #region Snippet
    /// <summary>
    /// Verifies DI consumers receive the same <see cref="PrimeTestClock"/> instance for
    /// <see cref="IPrimeTestClock"/>, <see cref="IPrimeClock"/>, and <see cref="IPrimeTime"/>.
    /// </summary>
    [Fact]
    public void DependencyInjection_AddPrimeTestClock_ResolvesSingletonAbstractions ()
    {
        ServiceCollection services = new();
        services.AddPrimeTestClock();
        services.AddSingleton<VirtualTimestampService>();

        using ServiceProvider provider = services.BuildServiceProvider();
        VirtualTimestampService consumer = provider.GetRequiredService<VirtualTimestampService>();
        IPrimeTestClock testClock = provider.GetRequiredService<IPrimeTestClock>();
        IPrimeClock clock = provider.GetRequiredService<IPrimeClock>();

        Instant marker = Instant.FromUtc(2025, 4, 1, 15, 0, 0);
        testClock.SetInstant(marker);
        consumer.ReadInstant().Should().Be(marker);
        clock.NowInstant.Should().Be(marker);
        consumer.ReadInstant().Should().Be(testClock.NowInstant);
    }

    /// <summary>
    /// Sample consumer that reads the virtual instant from an injected <see cref="IPrimeClock"/>.
    /// </summary>
    private sealed class VirtualTimestampService
    {
        private readonly IPrimeClock _clock;

        /// <summary>
        /// Initializes a new instance of the <see cref="VirtualTimestampService"/> class.
        /// </summary>
        /// <param name="clock">The prime clock abstraction.</param>
        public VirtualTimestampService (IPrimeClock clock)
        {
            _clock = clock;
        }

        /// <summary>
        /// Reads the clock's current instant.
        /// </summary>
        /// <returns>The virtual UTC instant exposed by the clock.</returns>
        public Instant ReadInstant () =>
            _clock.NowInstant;
    }
    #endregion Snippet
}
