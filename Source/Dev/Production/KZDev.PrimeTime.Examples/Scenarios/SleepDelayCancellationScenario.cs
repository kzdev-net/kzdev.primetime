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
        #region Snippet
        ServiceCollection services = [];
        services.AddPrimeClock();

        await using ServiceProvider serviceProvider = services.BuildServiceProvider();
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

        ScenarioConsole.WriteLine($"Sleeping for {sleepDuration}...");
        primeClock.Sleep(sleepDuration);
        ScenarioConsole.WriteLine("Sleep completed.");

        ScenarioConsole.WriteLine($"Running DelayAsync for {delayDuration}...");
        await primeClock.DelayAsync(delayDuration, cancellationToken);
        ScenarioConsole.WriteLine("DelayAsync completed.");

        using TimeCancellationTokenSource timeout = primeClock.GetTimeCancellationToken(cancellationAfter);
        try
        {
            ScenarioConsole.WriteLine($"Starting cancellable delay; timeout in {cancellationAfter}.");
            await primeClock.DelayAsync(Duration.FromSeconds(10), timeout.Token);
            ScenarioConsole.WriteLine("Delay completed before timeout.");
        }
        catch (OperationCanceledException)
        {
            ScenarioConsole.WriteLine("Delay canceled by time-based cancellation token.");
        }
        #endregion Snippet
    }
}
