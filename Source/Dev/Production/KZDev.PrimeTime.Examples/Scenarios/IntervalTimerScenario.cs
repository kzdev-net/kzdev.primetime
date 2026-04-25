// Copyright (c) Kevin Zehrer
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using KZDev.PrimeTime.Examples.Helpers;

using Microsoft.Extensions.DependencyInjection;

using NodaTime;

namespace KZDev.PrimeTime.Examples.Scenarios;

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

        using ServiceProvider serviceProvider = services.BuildServiceProvider();
        IPrimeClock primeClock = serviceProvider.GetRequiredService<IPrimeClock>();

        Duration dueTime = _runMode == DemoRunMode.Long
            ? Duration.FromSeconds(1)
            : Duration.FromMilliseconds(300);

        Duration repeat = _runMode == DemoRunMode.Long
            ? Duration.FromSeconds(2)
            : Duration.FromMilliseconds(700);

        TaskCompletionSource syncCompleted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource asyncCompleted = new(TaskCreationOptions.RunContinuationsAsynchronously);

        int syncCount = 0;
        int asyncCount = 0;

        IClockIntervalTimer syncTimer = primeClock.RegisterTimer(dueTime,
            repeat,
            context =>
            {
                int currentCount = Interlocked.Increment(ref syncCount);
                Console.WriteLine($"sync interval callback #{currentCount} on registration {context.Registration.Id}");
                if (currentCount >= 2)
                {
                    syncCompleted.TrySetResult();
                }
            },
            cancellationToken,
            state: "sync timer",
            timerOptions: null);

        IClockIntervalTimer asyncTimer = primeClock.RegisterAsyncTimer(dueTime,
            repeat,
            async (context, callbackCancellationToken) =>
            {
                int currentCount = Interlocked.Increment(ref asyncCount);
                Console.WriteLine($"async interval callback #{currentCount} on registration {context.Registration.Id}");
                if (currentCount >= 2)
                {
                    asyncCompleted.TrySetResult();
                }

                await ValueTask.CompletedTask;
            },
            cancellationToken,
            state: "async timer",
            timerOptions: null);

        await Task.WhenAll(syncCompleted.Task, asyncCompleted.Task);

        syncTimer.Dispose();
        asyncTimer.Dispose();
    }
}
