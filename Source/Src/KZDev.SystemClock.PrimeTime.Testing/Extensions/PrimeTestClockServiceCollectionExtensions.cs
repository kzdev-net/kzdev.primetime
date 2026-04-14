// Copyright (c) Kevin Zehrer
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using Microsoft.Extensions.DependencyInjection;

namespace KZDev.SystemClock.PrimeTime;

/// <summary>
///   Extension methods for adding PrimeTime virtual test clock services to an
///   <see cref="IServiceCollection"/>.
/// </summary>
public static class PrimeTestClockServiceCollectionExtensions
{
    //--------------------------------------------------------------------------------
    /// <summary>
    ///   Adds the default virtual test clock implementation
    ///   (<see cref="PrimeTestClock"/>) as a singleton in the service collection, registers
    ///   <see cref="IPrimeClock"/> and <see cref="IPrimeTime"/> to resolve to the same
    ///   singleton test clock.
    /// </summary>
    /// <param name="services">
    ///   The <see cref="IServiceCollection"/> to add the service to.
    /// </param>
    /// <returns>
    ///   The <see cref="IServiceCollection"/> for chaining further registrations.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    ///   <paramref name="services"/> is <c>null</c>.
    /// </exception>
    public static IServiceCollection AddPrimeTestClock (this IServiceCollection services)
    {
        if (services is null)
            throw new ArgumentNullException(nameof(services));

        services.AddSingleton<IPrimeTestClock, PrimeTestClock>();
        services.AddSingleton<IPrimeClock>(sp => sp.GetRequiredService<IPrimeTestClock>());
        services.AddSingleton<IPrimeTime>(sp => sp.GetRequiredService<IPrimeTestClock>());
        return services;
    }
    //--------------------------------------------------------------------------------
}
