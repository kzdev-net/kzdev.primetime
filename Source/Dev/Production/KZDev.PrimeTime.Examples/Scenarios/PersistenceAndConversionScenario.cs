// Copyright (c) Kevin Zehrer
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using Microsoft.Extensions.DependencyInjection;

using NodaTime;

namespace KZDev.PrimeTime.Examples.Scenarios;

/// <summary>
/// Demonstrates projecting persisted absolute instants into the clock schedule zone and
/// converting time-of-day shapes used for storage.
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

        Instant persistedUtc = Instant.FromUtc(2025, 7, 4, 15, 30, 0);

        ScenarioConsole.WriteLine($"Schedule zoned: {time.ToScheduleZonedDateTime(persistedUtc)}");
        ScenarioConsole.WriteLine($"Schedule local date: {time.ToScheduleLocalDate(persistedUtc)}");
        ScenarioConsole.WriteLine($"Schedule DateTimeOffset: {time.ToScheduleDateTimeOffset(persistedUtc):O}");

        LocalTime wallTime = time.ToScheduleLocalTime(persistedUtc);
        LocalTimeOfDay storedTimeOfDay = PrimeTimeOfDayConversion.ToLocalTimeOfDay(wallTime);
        ScenarioConsole.WriteLine(
            $"LocalTimeOfDay round-trip equals wall LocalTime: "
            + $"{PrimeTimeOfDayConversion.ToLocalTime(storedTimeOfDay).Equals(wallTime)}");

        Duration beyondBclRange = Duration.FromTimeSpan(TimeSpan.MaxValue) + Duration.FromSeconds(1);
        ScenarioConsole.WriteLine(
            $"Noda delay clamped to BCL: {NodaDurationBclConversion.ToTimeSpanForDelay(beyondBclRange)}");

        return Task.CompletedTask;
        #endregion Snippet
    }
}
