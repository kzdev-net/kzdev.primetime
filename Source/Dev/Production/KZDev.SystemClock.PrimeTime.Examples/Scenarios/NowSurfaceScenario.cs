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
        ServiceCollection services = [];
        services.AddPrimeClock();

        using ServiceProvider serviceProvider = services.BuildServiceProvider();
        IPrimeClock primeClock = serviceProvider.GetRequiredService<IPrimeClock>();

        Console.WriteLine($"LocalNowDateTimeOffset: {primeClock.LocalNowDateTimeOffset:O}");
        Console.WriteLine($"UtcNowDateTimeOffset: {primeClock.UtcNowDateTimeOffset:O}");
        Console.WriteLine($"LocalNowDateTime: {primeClock.LocalNowDateTime:O}");
        Console.WriteLine($"UtcNowDateTime: {primeClock.UtcNowDateTime:O}");
        Console.WriteLine($"LocalNowTimeOnly: {primeClock.LocalNowTimeOnly}");
        Console.WriteLine($"LocalNowDateOnly: {primeClock.LocalNowDateOnly}");

        return Task.CompletedTask;
    }
}
