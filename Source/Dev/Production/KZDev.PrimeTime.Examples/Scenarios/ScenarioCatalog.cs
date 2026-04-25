// Copyright (c) Kevin Zehrer
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using KZDev.PrimeTime.Examples.Helpers;
using KZDev.PrimeTime.Examples.Infrastructure;

namespace KZDev.PrimeTime.Examples.Scenarios;

/// <summary>
/// Defines scenario names for the PrimeTime example application.
/// </summary>
public static class ScenarioCatalog
{
    /// <summary>
    /// Creates the baseline list of scenario names for the selected run mode.
    /// </summary>
    /// <param name="runMode">
    /// The configured run mode.
    /// </param>
    /// <param name="context">
    /// The timezone and DST context.
    /// </param>
    /// <returns>
    /// A list of baseline scenario labels.
    /// </returns>
    public static IReadOnlyList<string> CreateBaselineScenarioNames(
        DemoRunMode runMode,
        TimeZoneScenarioContext context)
    {
        global::System.ArgumentNullException.ThrowIfNull(context);

        List<string> scenarios =
        [
            "DI registration baseline",
            "Clock now baseline",
            "Timer baseline",
        ];

        if (context.SupportsDaylightSavingTime)
        {
            scenarios.Add("DST transition baseline");
        }

        if (runMode == DemoRunMode.Long)
        {
            scenarios.Add("Extended runtime baseline");
        }

        return scenarios;
    }
}
