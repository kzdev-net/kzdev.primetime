// Copyright (c) Kevin Zehrer
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using Microsoft.Extensions.DependencyInjection;

namespace KZDev.PrimeTime;

/// <summary>
///   Extension methods for adding PrimeTime system clock services to an
///   <see cref="IServiceCollection"/>.
/// </summary>
public static class PrimeSystemClockServiceCollectionExtensions
{
    /// <summary>
    ///   Adds the default <see cref="IPrimeSystemClock"/> implementation
    ///   (<see cref="PrimeSystemClock"/>) as a singleton in the service collection.
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
    public static IServiceCollection AddPrimeSystemClock (this IServiceCollection services) =>
        services is null ?
            throw new ArgumentNullException(nameof(services)) :
            services.AddSingleton<IPrimeSystemClock, PrimeSystemClock>();
}
