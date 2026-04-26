// Copyright (c) Kevin Zehrer
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using Microsoft.Extensions.DependencyInjection;

namespace KZDev.PrimeTime.Examples.Scenarios;

/// <summary>
/// Demonstrates current-time APIs exposed by the PrimeTime clock.
/// </summary>
public sealed class NowSurfaceScenario : IExampleScenario
{
    /// <inheritdoc />
    public string Name => "\"Now\" API surfaces";

    /// <inheritdoc />
    public Task RunAsync(CancellationToken cancellationToken)
    {
        ServiceCollection services = [];
        services.AddPrimeClock();

        using ServiceProvider serviceProvider = services.BuildServiceProvider();
        IPrimeClock primeClock = serviceProvider.GetRequiredService<IPrimeClock>();

        ScenarioConsole.WriteLine($"NowInstant: {primeClock.NowInstant}");
        ScenarioConsole.WriteLine($"LocalNowInstant: {primeClock.LocalNowInstant}");
        ScenarioConsole.WriteLine($"UtcNowInstant: {primeClock.UtcNowInstant}");
        ScenarioConsole.WriteLine($"LocalZonedNowInstant: {primeClock.LocalZonedNowInstant}");
        ScenarioConsole.WriteLine($"LocalNowTime: {primeClock.LocalNowTime}");
        ScenarioConsole.WriteLine($"LocalNowDate: {primeClock.LocalNowDate}");
        ScenarioConsole.WriteLine($"LocalNowDateTimeOffset: {primeClock.LocalNowDateTimeOffset:O}");

        return Task.CompletedTask;
    }
}
