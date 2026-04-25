// Copyright (c) Kevin Zehrer
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

namespace KZDev.PrimeTime.Examples.Infrastructure;

/// <summary>
/// Builds timezone context from the current machine environment.
/// </summary>
public sealed class LocalTimeZoneScenarioContextFactory : ITimeZoneScenarioContextFactory
{
    /// <summary>
    /// Creates timezone context based on the local machine timezone.
    /// </summary>
    /// <returns>
    /// Local timezone identity and daylight-saving support information.
    /// </returns>
    public TimeZoneScenarioContext Create()
    {
        TimeZoneInfo localTimeZone = TimeZoneInfo.Local;

        return new TimeZoneScenarioContext(
            localTimeZone.Id,
            localTimeZone.SupportsDaylightSavingTime);
    }
}
