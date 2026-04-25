// Copyright (c) Kevin Zehrer
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using KZDev.PrimeTime.Examples.Helpers;

using Microsoft.Extensions.DependencyInjection;

using NodaTime;

namespace KZDev.PrimeTime.Examples.Scenarios;

/// <summary>
/// Demonstrates sleep, delay, and cancellation APIs using <see cref="Duration"/>.
/// </summary>
public sealed class SleepDelayCancellationScenario : IExampleScenario
{
    private readonly DemoRunMode _runMode;

    /// <summary>
    /// Initializes a new instance of the <see cref="SleepDelayCancellationScenario"/> class.
    /// </summary>
    /// <param name="runMode">
    /// The configured demo runtime mode.
    /// </param>
    public SleepDelayCancellationScenario(DemoRunMode runMode)
    {
        _runMode = runMode;
    }

    /// <inheritdoc />
    public string Name => "Sleep / Delay / cancellation";

    /// <inheritdoc />
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        ServiceCollection services = [];
        services.AddPrimeClock();

        using ServiceProvider serviceProvider = services.BuildServiceProvider();
        IPrimeClock primeClock = serviceProvider.GetRequiredService<IPrimeClock>();

        Duration sleepDuration = _runMode == DemoRunMode.Long
            ? Duration.FromSeconds(1)
            : Duration.FromMilliseconds(250);

        Duration delayDuration = _runMode == DemoRunMode.Long
            ? Duration.FromSeconds(2)
            : Duration.FromSeconds(1);

        Duration cancellationAfter = _runMode == DemoRunMode.Long
            ? Duration.FromSeconds(2)
            : Duration.FromMilliseconds(800);

        Console.WriteLine($"Sleeping for {sleepDuration}...");
        primeClock.Sleep(sleepDuration);
        Console.WriteLine("Sleep completed.");

        Console.WriteLine($"Running DelayAsync for {delayDuration}...");
        await primeClock.DelayAsync(delayDuration, cancellationToken);
        Console.WriteLine("DelayAsync completed.");

        using TimeCancellationTokenSource timeout = primeClock.GetTimeCancellationToken(cancellationAfter);
        try
        {
            Console.WriteLine($"Starting cancellable delay; timeout in {cancellationAfter}.");
            await primeClock.DelayAsync(Duration.FromSeconds(10), timeout.Token);
            Console.WriteLine("Delay completed before timeout.");
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("Delay canceled by time-based cancellation token.");
        }
    }
}
