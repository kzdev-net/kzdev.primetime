// Copyright (c) Kevin Zehrer
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

namespace KZDev.PrimeTime.Testing.Examples.Infrastructure;

/// <summary>
/// Captures machine-specific details for deterministic test scenario setup.
/// </summary>
/// <param name="LocalTimeZoneId">
/// Local timezone identifier on the executing machine.
/// </param>
/// <param name="SupportsDaylightSavingTime">
/// Indicates whether the local timezone has daylight-saving transitions.
/// </param>
public sealed record TestEnvironmentDescriptor(
    string LocalTimeZoneId,
    bool SupportsDaylightSavingTime);
