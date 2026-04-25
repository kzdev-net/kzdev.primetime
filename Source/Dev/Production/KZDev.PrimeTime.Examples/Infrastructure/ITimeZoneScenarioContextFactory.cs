// Copyright (c) Kevin Zehrer
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

namespace KZDev.PrimeTime.Examples.Infrastructure;

/// <summary>
/// Creates runtime-specific timezone and DST context for scenarios.
/// </summary>
public interface ITimeZoneScenarioContextFactory
{
    /// <summary>
    /// Creates context for timezone and DST-aware scenarios on the current machine.
    /// </summary>
    /// <returns>
    /// A context object describing local timezone capabilities.
    /// </returns>
    TimeZoneScenarioContext Create();
}
