// Copyright (c) Kevin Zehrer
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using Microsoft.Extensions.DependencyInjection;

using NodaTime;

namespace KZDev.PrimeTime.Examples.Scenarios;

/// <summary>
/// Demonstrates dependency-injection registration and service resolution.
/// </summary>
public sealed class DiRegistrationScenario : IExampleScenario
{
    /// <inheritdoc />
    public string Name => "DI registration and service resolution";

    /// <inheritdoc />
    public Task RunAsync(CancellationToken cancellationToken)
    {
        ServiceCollection services = [];
        services.AddPrimeClock();

        using ServiceProvider serviceProvider = services.BuildServiceProvider();

        IPrimeClock primeClock = serviceProvider.GetRequiredService<IPrimeClock>();
        IPrimeTime primeTime = serviceProvider.GetRequiredService<IPrimeTime>();
        IClock nodaClock = serviceProvider.GetRequiredService<IClock>();

        ScenarioConsole.WriteLine($"IPrimeClock: {primeClock.GetType().Name}");
        ScenarioConsole.WriteLine($"IPrimeTime resolves same singleton: {ReferenceEquals(primeClock, primeTime)}");
        ScenarioConsole.WriteLine($"Noda IClock registered: {nodaClock.GetType().Name}");

        return Task.CompletedTask;
    }
}
