// Copyright (c) Kevin Zehrer
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using Microsoft.Extensions.DependencyInjection;

namespace KZDev.SystemClock.PrimeTime.Examples.Scenarios;

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
        #region Snippet
        ServiceCollection services = [];
        services.AddPrimeClock();

        using ServiceProvider serviceProvider = services.BuildServiceProvider();
        IPrimeClock primeClock = serviceProvider.GetRequiredService<IPrimeClock>();

        ScenarioConsole.WriteLine($"LocalNowDateTimeOffset: {primeClock.LocalNowDateTimeOffset:O}");
        ScenarioConsole.WriteLine($"UtcNowDateTimeOffset: {primeClock.UtcNowDateTimeOffset:O}");
        ScenarioConsole.WriteLine($"LocalNowDateTime: {primeClock.LocalNowDateTime:O}");
        ScenarioConsole.WriteLine($"UtcNowDateTime: {primeClock.UtcNowDateTime:O}");
        ScenarioConsole.WriteLine($"LocalNowTimeOnly: {primeClock.LocalNowTimeOnly}");
        ScenarioConsole.WriteLine($"LocalNowDateOnly: {primeClock.LocalNowDateOnly}");

        return Task.CompletedTask;
        #endregion Snippet
    }
}
