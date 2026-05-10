// Copyright (c) Kevin Zehrer
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using Microsoft.Extensions.DependencyInjection;

namespace KZDev.SystemClock.PrimeTime.Examples.Scenarios;

/// <summary>
/// Demonstrates projecting persisted UTC <see cref="DateTimeOffset"/> values into the clock
/// schedule zone using BCL date and time types.
/// </summary>
public sealed class PersistenceAndConversionScenario : IExampleScenario
{
    /// <inheritdoc />
    public string Name => "Persistence and schedule-zone conversions";

    /// <inheritdoc />
    public Task RunAsync(CancellationToken cancellationToken)
    {
        #region Snippet
        ServiceCollection services = [];
        services.AddPrimeClock();

        using ServiceProvider serviceProvider = services.BuildServiceProvider();
        IPrimeClock primeClock = serviceProvider.GetRequiredService<IPrimeClock>();
        IPrimeTime time = primeClock;

        DateTimeOffset persistedUtc = new DateTimeOffset(2025, 7, 4, 15, 30, 0, TimeSpan.Zero);

        DateTimeOffset inScheduleZone = time.ToScheduleDateTimeOffset(persistedUtc);
        ScenarioConsole.WriteLine($"Schedule DateTimeOffset: {inScheduleZone:O}");
        ScenarioConsole.WriteLine($"Schedule DateOnly: {time.ToScheduleDateOnly(persistedUtc)}");
        ScenarioConsole.WriteLine($"Schedule TimeOnly: {time.ToScheduleTimeOnly(persistedUtc)}");

        DateOnly scheduleDate = time.ToScheduleDateOnly(persistedUtc);
        TimeOnly scheduleTimeOfDay = time.ToScheduleTimeOnly(persistedUtc);
        DateTime wallUnspecified = scheduleDate.ToDateTime(scheduleTimeOfDay);
        ScenarioConsole.WriteLine($"DateOnly+TimeOnly wall (unspecified kind): {wallUnspecified:O}");

        return Task.CompletedTask;
        #endregion Snippet
    }
}
