// Copyright (c) Kevin Zehrer
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using KZDev.PrimeTime.Examples.Helpers;

using Microsoft.Extensions.DependencyInjection;

using NodaTime;

namespace KZDev.PrimeTime.Examples.Scenarios;

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
        ServiceCollection services = [];
        services.AddPrimeClock();

        using ServiceProvider serviceProvider = services.BuildServiceProvider();
        IPrimeClock primeClock = serviceProvider.GetRequiredService<IPrimeClock>();

        LocalTime targetTime = primeClock.LocalNowTime.Plus(
            _runMode == DemoRunMode.Long
                ? Period.FromSeconds(8)
                : Period.FromSeconds(4));

        TaskCompletionSource syncCompleted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource asyncCompleted = new(TaskCreationOptions.RunContinuationsAsynchronously);

        IClockDayTimeTimer syncTimer = primeClock.RegisterTimeOfDay(targetTime,
            context =>
            {
                Console.WriteLine($"sync time-of-day callback fired. RegistrationId={context.Registration.Id}");
                syncCompleted.TrySetResult();
            },
            cancellationToken,
            state: "sync time-of-day",
            timerOptions: null);

        IClockDayTimeTimer asyncTimer = primeClock.RegisterAsyncTimeOfDay(targetTime,
            async (context, callbackCancellationToken) =>
            {
                Console.WriteLine($"async time-of-day callback fired. RegistrationId={context.Registration.Id}");
                asyncCompleted.TrySetResult();
                await ValueTask.CompletedTask;
            },
            cancellationToken,
            state: "async time-of-day",
            timerOptions: null);

        await Task.WhenAll(syncCompleted.Task, asyncCompleted.Task);

        syncTimer.Dispose();
        asyncTimer.Dispose();
    }
}
