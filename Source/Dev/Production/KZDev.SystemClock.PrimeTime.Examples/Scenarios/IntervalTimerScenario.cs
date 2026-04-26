// Copyright (c) Kevin Zehrer
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using KZDev.SystemClock.PrimeTime.Examples.Helpers;

using Microsoft.Extensions.DependencyInjection;

namespace KZDev.SystemClock.PrimeTime.Examples.Scenarios;

/// <summary>
/// Demonstrates interval timers with synchronous and asynchronous callbacks.
/// </summary>
public sealed class IntervalTimerScenario : IExampleScenario
{
    private readonly DemoRunMode _runMode;

    /// <summary>
    /// Initializes a new instance of the <see cref="IntervalTimerScenario"/> class.
    /// </summary>
    /// <param name="runMode">
    /// The configured demo runtime mode.
    /// </param>
    public IntervalTimerScenario(DemoRunMode runMode)
    {
        _runMode = runMode;
    }

    /// <inheritdoc />
    public string Name => "Interval timers (sync and async callbacks)";

    /// <inheritdoc />
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        ServiceCollection services = [];
        services.AddPrimeClock();

        await using ServiceProvider serviceProvider = services.BuildServiceProvider();
        IPrimeClock primeClock = serviceProvider.GetRequiredService<IPrimeClock>();

        TimeSpan dueTime = _runMode == DemoRunMode.Long
            ? TimeSpan.FromSeconds(1)
            : TimeSpan.FromMilliseconds(300);

        TimeSpan repeat = _runMode == DemoRunMode.Long
            ? TimeSpan.FromSeconds(2)
            : TimeSpan.FromMilliseconds(700);

        TaskCompletionSource syncCompleted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource asyncCompleted = new(TaskCreationOptions.RunContinuationsAsynchronously);

        int syncCount = 0;
        int asyncCount = 0;

        ScenarioConsole.WriteLine($"Starting sync interval timer (due: {dueTime}, repeat: {repeat}).");
        IClockIntervalTimer syncTimer = primeClock.RegisterTimer(dueTime, repeat,
            (context, callbackCancellationToken) =>
            {
                callbackCancellationToken.ThrowIfCancellationRequested();
                int currentCount = Interlocked.Increment(ref syncCount);
                ScenarioConsole.WriteLine($"sync interval callback #{currentCount} on registration {context.Registration.Id}");
                if (currentCount >= 2)
                {
                    syncCompleted.TrySetResult();
                }
            },
            cancellationToken, state: "sync timer", timerOptions: null);

        ScenarioConsole.WriteLine($"Starting async interval timer (due: {dueTime}, repeat: {repeat}).");
        IClockIntervalTimer asyncTimer = primeClock.RegisterAsyncTimer(dueTime, repeat,
            async (context, callbackCancellationToken) =>
            {
                callbackCancellationToken.ThrowIfCancellationRequested();
                int currentCount = Interlocked.Increment(ref asyncCount);
                ScenarioConsole.WriteLine($"async interval callback #{currentCount} on registration {context.Registration.Id}");
                if (currentCount >= 2)
                {
                    asyncCompleted.TrySetResult();
                }

                await ValueTask.CompletedTask;
            },
            cancellationToken, state: "async timer", timerOptions: null);

        ScenarioConsole.WriteLine("Waiting for both interval timers to fire twice...");
        await Task.WhenAll(syncCompleted.Task, asyncCompleted.Task);
        ScenarioConsole.WriteLine("Both interval timers completed required callback count.");

        syncTimer.Dispose();
        asyncTimer.Dispose();
    }
}
