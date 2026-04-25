// Copyright (c) Kevin Zehrer
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

namespace KZDev.SystemClock.PrimeTime.Examples.Infrastructure;

/// <summary>
/// Captures machine-specific time zone properties needed by DST scenarios.
/// </summary>
/// <param name="LocalTimeZoneId">
/// Local time zone identifier for the current runtime environment.
/// </param>
/// <param name="SupportsDaylightSavingTime">
/// Indicates whether the local time zone has daylight-saving transitions.
/// </param>
/// <param name="InvalidLocalTimeExample">
/// A representative invalid local time in the zone, if one can be identified.
/// </param>
/// <param name="AmbiguousLocalTimeExample">
/// A representative ambiguous local time in the zone, if one can be identified.
/// </param>
public sealed record TimeZoneScenarioContext(
    string LocalTimeZoneId,
    bool SupportsDaylightSavingTime,
    DateTime? InvalidLocalTimeExample,
    DateTime? AmbiguousLocalTimeExample);
