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
    /// A list of runnable production scenarios.
    /// </returns>
    public static IReadOnlyList<IExampleScenario> CreateScenarios (DemoRunMode runMode,
        TimeZoneScenarioContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        List<IExampleScenario> scenarios =
        [
            new DiRegistrationScenario(),
            new NowSurfaceScenario(),
            new PersistenceAndConversionScenario(),
            new SleepDelayCancellationScenario(runMode),
            new IntervalTimerScenario(runMode),
            new TimeOfDayTimerScenario(runMode),
            new TimeProviderBridgeScenario(),
            new DstScenario(),
            new EnvironmentAwareDstScenario(context)
        ];

        return scenarios;
    }
}
