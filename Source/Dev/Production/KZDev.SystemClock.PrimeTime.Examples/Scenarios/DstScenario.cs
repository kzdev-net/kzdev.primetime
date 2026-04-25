// Copyright (c) Kevin Zehrer
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using Microsoft.Extensions.DependencyInjection;

namespace KZDev.SystemClock.PrimeTime.Examples.Scenarios;

/// <summary>
/// Demonstrates that the local scheduling zone exposed by the clock is available for DST logic.
/// </summary>
public sealed class DstScenario : IExampleScenario
{
    /// <inheritdoc />
    public string Name => "Clock local schedule zone";

    /// <inheritdoc />
    public Task RunAsync(CancellationToken cancellationToken)
    {
        ServiceCollection services = [];
        services.AddPrimeClock();

        using ServiceProvider serviceProvider = services.BuildServiceProvider();
        IPrimeClock primeClock = serviceProvider.GetRequiredService<IPrimeClock>();

        TimeZoneInfo localScheduleZone = primeClock.LocalScheduleTimeZone;
        Console.WriteLine($"Clock LocalScheduleTimeZone: {localScheduleZone.Id}");
        Console.WriteLine($"Clock zone supports DST: {localScheduleZone.SupportsDaylightSavingTime}");

        return Task.CompletedTask;
    }
}
