// Copyright (c) Kevin Zehrer. All rights reserved.
// This file is part of the PrimeTime project.

using Microsoft.Extensions.DependencyInjection;

#if SYSTEMCLOCK
namespace KZDev.SystemClock.PrimeTime;
#else
namespace KZDev.PrimeTime;
#endif

//################################################################################
/// <summary>
///   Extension methods for adding PrimeTime clock services to an
///   <see cref="IServiceCollection"/>.
/// </summary>
public static partial class PrimeClockServiceCollectionExtensions
{
    //----------------------------------------------------------------------------
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
    //----------------------------------------------------------------------------
}
//################################################################################
