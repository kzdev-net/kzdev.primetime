// Copyright (c) Kevin Zehrer
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;

using AwesomeAssertions;

using KZDev.PrimeTime.Tests;

using Microsoft.Extensions.DependencyInjection;

namespace KZDev.SystemClock.PrimeTime.UnitTests;

//################################################################################
/// <summary>
///   Unit tests for <see cref="PrimeClockServiceCollectionExtensions.AddPrimeClock(IServiceCollection)"/>.
/// </summary>
[ExcludeFromCodeCoverage]
public class UsingPrimeClockServiceCollectionExtensions : UnitTestBase
{
    #region Constructors/Finalizers

    //----------------------------------------------------------------------------
    /// <summary>
    ///   Initializes a new instance of the <see cref="UsingPrimeClockServiceCollectionExtensions"/> class.
    /// </summary>
    /// <param name="xUnitTestOutputHelper">
    ///   The xUnit test output helper that can be used to output test messages.
    /// </param>
    public UsingPrimeClockServiceCollectionExtensions (ITestOutputHelper xUnitTestOutputHelper)
        : base(xUnitTestOutputHelper)
    {
    }
    //----------------------------------------------------------------------------

    #endregion Constructors/Finalizers

    /// <summary>
    ///   Verifies that <see cref="PrimeClockServiceCollectionExtensions.AddPrimeClock(IServiceCollection)"/>
    ///   throws <see cref="ArgumentNullException"/> when the service collection is null.
    /// </summary>
    [Fact]
    public void AddPrimeClock_WithNullServices_ThrowsArgumentNullException ()
    {
        IServiceCollection? services = null;
        Action act = () => services!.AddPrimeClock();
        act.Should().Throw<ArgumentNullException>().WithParameterName("services");
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies that after <see cref="PrimeClockServiceCollectionExtensions.AddPrimeClock(IServiceCollection)"/>,
    ///   building the provider resolves <see cref="IPrimeClock"/> as a <see cref="PrimeClock"/> singleton.
    /// </summary>
    [Fact]
    public void AddPrimeClock_BuildServiceProvider_ResolvesIPrimeClockAsPrimeClockSingleton ()
    {
        IServiceCollection services = new ServiceCollection();
        services.AddPrimeClock();
        using ServiceProvider provider = services.BuildServiceProvider();
        IPrimeClock clock = provider.GetRequiredService<IPrimeClock>();
        clock.Should().NotBeNull().And.BeOfType<PrimeClock>();
        IPrimeClock second = provider.GetRequiredService<IPrimeClock>();
        second.Should().BeSameAs(clock);
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies that <see cref="PrimeClockServiceCollectionExtensions.AddPrimeClock(IServiceCollection)"/>
    ///   returns the same service collection instance for fluent chaining and adds singleton
    ///   descriptors for <see cref="TimeProvider"/> (<see cref="TimeProvider.System"/>) and
    ///   <see cref="IPrimeClock"/> implemented by <see cref="PrimeClock"/>.
    /// </summary>
    [Fact]
    public void AddPrimeClock_ReturnsSameServiceCollectionAndRegistersSingletonDescriptors ()
    {
        IServiceCollection services = new ServiceCollection();

        IServiceCollection returned = services.AddPrimeClock();

        returned.Should().BeSameAs(services);
        services.Should().HaveCount(2);

        ServiceDescriptor timeProviderDescriptor = services.Single(d => d.ServiceType == typeof(TimeProvider));
        timeProviderDescriptor.Lifetime.Should().Be(ServiceLifetime.Singleton);
        object? timeProviderSingleton = timeProviderDescriptor.ImplementationInstance
            ?? timeProviderDescriptor.ImplementationFactory?.Invoke(null!);
        timeProviderSingleton.Should().BeSameAs(TimeProvider.System);

        ServiceDescriptor primeClockDescriptor = services.Single(d => d.ServiceType == typeof(IPrimeClock));
        primeClockDescriptor.ImplementationType.Should().Be(typeof(PrimeClock));
        primeClockDescriptor.Lifetime.Should().Be(ServiceLifetime.Singleton);
    }
    //----------------------------------------------------------------------------
}
//################################################################################
