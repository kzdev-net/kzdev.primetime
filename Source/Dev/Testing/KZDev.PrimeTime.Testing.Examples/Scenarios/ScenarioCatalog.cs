// Copyright (c) Kevin Zehrer
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

namespace KZDev.PrimeTime.Testing.Examples.Scenarios;

/// <summary>
/// Defines scenario labels for the PrimeTime testing examples.
/// </summary>
public static class ScenarioCatalog
{
    /// <summary>
    /// Gets baseline scenario names for Phase 1 scaffolding.
    /// </summary>
    /// <returns>
    /// Scenario names that define the planned testing example areas.
    /// </returns>
    public static IReadOnlyList<string> GetBaselineScenarioNames()
    {
        return
        [
            "Test clock control baseline",
            "Deterministic timer baseline",
            "DI replacement baseline",
            "DST baseline",
        ];
    }
}
