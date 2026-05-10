// Copyright (c) Kevin Zehrer
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

namespace KZDev.SystemClock.PrimeTime.Testing.Examples.Scenarios;

/// <summary>
/// Defines scenario labels for the SystemClock PrimeTime testing examples.
/// </summary>
public static class ScenarioCatalog
{
    /// <summary>
    /// Gets the scenario categories implemented by the SystemClock testing example tests.
    /// </summary>
    /// <returns>
    /// Human-readable scenario names aligned with the example test classes.
    /// </returns>
    public static IReadOnlyList<string> GetImplementedScenarioNames ()
    {
        return
        [
            "Test clock control (SetTime, Advance, RunFor, Start/Stop)",
            "Deterministic interval timers (sync and async callbacks)",
            "DI replacement via AddPrimeTestClock",
            "DST (environment-aware local probe plus virtual advance smoke)",
            "Persistence and schedule-zone conversions (UTC offsets, DateOnly/TimeOnly)",
        ];
    }
}
