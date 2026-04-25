// Copyright (c) Kevin Zehrer
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

namespace KZDev.SystemClock.PrimeTime.Testing.Examples.Infrastructure;

/// <summary>
/// Captures machine-specific details for deterministic test scenario setup.
/// </summary>
/// <param name="LocalTimeZoneId">
/// Local timezone identifier on the executing machine.
/// </param>
/// <param name="SupportsDaylightSavingTime">
/// Indicates whether the local timezone has daylight-saving transitions.
/// </param>
/// <param name="InvalidLocalWallClockExample">
/// Optional local wall-clock value in the gap when clocks spring forward, if discoverable.
/// </param>
/// <param name="AmbiguousLocalWallClockExample">
/// Optional local wall-clock value that occurs twice when clocks fall back, if discoverable.
/// </param>
public sealed record TestEnvironmentDescriptor(
    string LocalTimeZoneId,
    bool SupportsDaylightSavingTime,
    DateTime? InvalidLocalWallClockExample,
    DateTime? AmbiguousLocalWallClockExample)
{
    /// <summary>
    /// Builds a snapshot of <see cref="TimeZoneInfo.Local"/> for environment-aware example tests.
    /// </summary>
    /// <returns>
    /// A descriptor with optional DST wall-clock examples when adjustment rules expose them.
    /// </returns>
    public static TestEnvironmentDescriptor FromLocalMachine () =>
        LocalDstTransitionFinder.BuildDescriptorForLocalMachine();
}
