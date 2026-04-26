// Copyright (c) Kevin Zehrer
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using KZDev.SystemClock.PrimeTime.Examples.Infrastructure;

namespace KZDev.SystemClock.PrimeTime.Examples.Scenarios;

/// <summary>
/// Demonstrates environment-aware skipped and ambiguous local-time handling.
/// </summary>
public sealed class EnvironmentAwareDstScenario : IExampleScenario
{
    private readonly TimeZoneScenarioContext _context;

    /// <summary>
    /// Initializes a new instance of the <see cref="EnvironmentAwareDstScenario"/> class.
    /// </summary>
    /// <param name="context">
    /// Local runtime timezone and DST context.
    /// </param>
    public EnvironmentAwareDstScenario(TimeZoneScenarioContext context)
    {
        _context = context;
    }

    /// <inheritdoc />
    public string Name => "DST skipped/ambiguous local-time behavior";

    /// <inheritdoc />
    public Task RunAsync(CancellationToken cancellationToken)
    {
        TimeZoneInfo localZone = TimeZoneInfo.Local;
        if (!_context.SupportsDaylightSavingTime)
        {
            ScenarioConsole.WriteLine("Local timezone does not expose DST transitions on this machine.");
            return Task.CompletedTask;
        }

        if (_context.InvalidLocalTimeExample is DateTime invalidLocalTime)
        {
            bool isInvalid = localZone.IsInvalidTime(invalidLocalTime);
            ScenarioConsole.WriteLine($"Invalid local time sample: {invalidLocalTime:yyyy-MM-dd HH:mm:ss} (IsInvalidTime={isInvalid})");
        }
        else
        {
            ScenarioConsole.WriteLine("No invalid-time sample could be computed for the current environment.");
        }

        if (_context.AmbiguousLocalTimeExample is DateTime ambiguousLocalTime)
        {
            bool isAmbiguous = localZone.IsAmbiguousTime(ambiguousLocalTime);
            ScenarioConsole.WriteLine($"Ambiguous local time sample: {ambiguousLocalTime:yyyy-MM-dd HH:mm:ss} (IsAmbiguousTime={isAmbiguous})");
            if (isAmbiguous)
            {
                TimeSpan[] offsets = localZone.GetAmbiguousTimeOffsets(ambiguousLocalTime);
                ScenarioConsole.WriteLine($"Ambiguous offsets: {string.Join(", ", offsets)}");
            }
        }
        else
        {
            ScenarioConsole.WriteLine("No ambiguous-time sample could be computed for the current environment.");
        }

        return Task.CompletedTask;
    }
}
