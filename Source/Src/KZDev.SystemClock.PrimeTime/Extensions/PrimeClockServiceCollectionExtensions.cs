// Copyright (c) Kevin Zehrer
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using Microsoft.Extensions.DependencyInjection;
using KZDev.PrimeTime;

namespace KZDev.SystemClock.PrimeTime;

/// <summary>
///   Extension methods for adding PrimeTime BCL clock services to an
///   <see cref="IServiceCollection"/>.
/// </summary>
public static class PrimeClockServiceCollectionExtensions
{
    /// <summary>
    ///   Adds the default <see cref="IPrimeClock"/> implementation
    ///   (<see cref="PrimeClock"/>) as a singleton in the service collection.
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
    public static IServiceCollection AddPrimeClock (this IServiceCollection services) =>
        services is null ?
            throw new ArgumentNullException(nameof(services)) :
            services.AddSingleton<IPrimeClock, PrimeClock>();
}

