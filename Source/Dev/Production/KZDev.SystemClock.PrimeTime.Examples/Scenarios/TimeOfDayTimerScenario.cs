// Copyright (c) Kevin Zehrer
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using KZDev.SystemClock.PrimeTime.Examples.Helpers;

using Microsoft.Extensions.DependencyInjection;

namespace KZDev.SystemClock.PrimeTime.Examples.Scenarios;

/// <summary>
/// Demonstrates time-of-day timers with synchronous and asynchronous callbacks.
/// </summary>
public sealed class TimeOfDayTimerScenario : IExampleScenario
{
    private readonly DemoRunMode _runMode;

    /// <summary>
    /// Initializes a new instance of the <see cref="TimeOfDayTimerScenario"/> class.
    /// </summary>
    /// <param name="runMode">
    /// The configured demo runtime mode.
    /// </param>
    public TimeOfDayTimerScenario(DemoRunMode runMode)
    {
        _runMode = runMode;
    }

    /// <inheritdoc />
    public string Name => "Time-of-day timers (sync and async callbacks)";

    /// <inheritdoc />
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        #region Snippet
        ServiceCollection services = [];
        services.AddPrimeClock();

        await using ServiceProvider serviceProvider = services.BuildServiceProvider();
        IPrimeClock primeClock = serviceProvider.GetRequiredService<IPrimeClock>();

        TimeOnly now = primeClock.LocalNowTimeOnly;
        TimeOnly target = now.Add(_runMode == DemoRunMode.Long
                ? TimeSpan.FromSeconds(8)
                : TimeSpan.FromSeconds(4));

        LocalTimeOfDay timeOfDay = new(target);
        TaskCompletionSource syncCompleted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource asyncCompleted = new(TaskCreationOptions.RunContinuationsAsynchronously);

        ScenarioConsole.WriteLine($"Starting sync time-of-day timer for local time {timeOfDay.ToLongTimeString()}.");
        IClockDayTimeTimer syncTimer = primeClock.RegisterTimeOfDay(timeOfDay,
            (context, callbackCancellationToken) =>
            {
                callbackCancellationToken.ThrowIfCancellationRequested();
                ScenarioConsole.WriteLine($"sync time-of-day callback fired. RegistrationId={context.Registration.Id}");
                syncCompleted.TrySetResult();
            },
            cancellationToken, state: "sync time-of-day", timerOptions: null);

        ScenarioConsole.WriteLine($"Starting async time-of-day timer for local time {timeOfDay.ToLongTimeString()}.");
        IClockDayTimeTimer asyncTimer = primeClock.RegisterAsyncTimeOfDay(timeOfDay,
            async (context, callbackCancellationToken) =>
            {
                callbackCancellationToken.ThrowIfCancellationRequested();
                ScenarioConsole.WriteLine($"async time-of-day callback fired. RegistrationId={context.Registration.Id}");
                asyncCompleted.TrySetResult();
                await ValueTask.CompletedTask;
            },
            cancellationToken, state: "async time-of-day", timerOptions: null);

        ScenarioConsole.WriteLine("Waiting for both time-of-day timers to fire...");
        await Task.WhenAll(syncCompleted.Task, asyncCompleted.Task);
        ScenarioConsole.WriteLine("Both time-of-day timers have fired.");

        syncTimer.Dispose();
        asyncTimer.Dispose();
        #endregion Snippet
    }
}
