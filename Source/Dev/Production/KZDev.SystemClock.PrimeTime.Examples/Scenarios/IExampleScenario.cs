// Copyright (c) Kevin Zehrer
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

namespace KZDev.SystemClock.PrimeTime.Examples.Scenarios;

/// <summary>
/// Represents a runnable scenario in the production example application.
/// </summary>
public interface IExampleScenario
{
    /// <summary>
    /// Gets the display name used by the scenario runner.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Runs the scenario.
    /// </summary>
    /// <param name="cancellationToken">
    /// Token used to stop the scenario run.
    /// </param>
    /// <returns>
    /// A task that completes when the scenario has finished.
    /// </returns>
    Task RunAsync(CancellationToken cancellationToken);
}
