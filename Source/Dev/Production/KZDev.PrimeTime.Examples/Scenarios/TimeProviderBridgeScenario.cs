// Copyright (c) Kevin Zehrer
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using Microsoft.Extensions.DependencyInjection;

namespace KZDev.PrimeTime.Examples.Scenarios;

/// <summary>
/// Demonstrates bridging a PrimeTime clock to <see cref="TimeProvider"/>.
/// </summary>
public sealed class TimeProviderBridgeScenario : IExampleScenario
{
    /// <inheritdoc />
    public string Name => "TimeProvider bridge";

    /// <inheritdoc />
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        ServiceCollection services = [];
        services.AddPrimeClock();

        await using ServiceProvider serviceProvider = services.BuildServiceProvider();
        IPrimeClock primeClock = serviceProvider.GetRequiredService<IPrimeClock>();

        TimeProvider provider = primeClock.ToTimeProvider();
        ScenarioConsole.WriteLine($"TimeProvider UTC now: {provider.GetUtcNow():O}");
        ScenarioConsole.WriteLine($"TimeProvider local zone: {provider.LocalTimeZone.Id}");

        TimeSpan dueTime = TimeSpan.FromMilliseconds(250);
        TaskCompletionSource timerCompleted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        ScenarioConsole.WriteLine($"Starting TimeProvider.CreateTimer with due time {dueTime}.");
        await using ITimer timer = provider.CreateTimer(callback: _ => timerCompleted.TrySetResult(),
            state: null, dueTime: dueTime, period: Timeout.InfiniteTimeSpan);

        ScenarioConsole.WriteLine("Waiting for TimeProvider.CreateTimer callback...");
        await timerCompleted.Task.WaitAsync(cancellationToken);
        ScenarioConsole.WriteLine("TimeProvider.CreateTimer callback completed.");
    }
}
