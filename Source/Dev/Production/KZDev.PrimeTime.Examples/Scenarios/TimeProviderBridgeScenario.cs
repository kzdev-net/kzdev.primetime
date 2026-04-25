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

        using ServiceProvider serviceProvider = services.BuildServiceProvider();
        IPrimeClock primeClock = serviceProvider.GetRequiredService<IPrimeClock>();

        TimeProvider provider = primeClock.ToTimeProvider();
        Console.WriteLine($"TimeProvider UTC now: {provider.GetUtcNow():O}");
        Console.WriteLine($"TimeProvider local zone: {provider.LocalTimeZone.Id}");

        TaskCompletionSource timerCompleted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        using ITimer timer = provider.CreateTimer(
            callback: _ => timerCompleted.TrySetResult(),
            state: null,
            dueTime: TimeSpan.FromMilliseconds(250),
            period: Timeout.InfiniteTimeSpan);

        await timerCompleted.Task.WaitAsync(cancellationToken);
        Console.WriteLine("TimeProvider.CreateTimer callback completed.");
    }
}
