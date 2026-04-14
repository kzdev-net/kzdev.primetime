// Copyright (c) Kevin Zehrer
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;

using AwesomeAssertions;

using KZDev.PrimeTime.Tests;

using Microsoft.Extensions.DependencyInjection;

namespace KZDev.PrimeTime.UnitTests;

//################################################################################
/// <summary>
///   Unit tests for <see cref="PrimeTestClockServiceCollectionExtensions.AddPrimeTestClock(IServiceCollection)"/>
///   in the NodaTime testing package.
/// </summary>
[ExcludeFromCodeCoverage]
public class UsingPrimeTestClockServiceCollectionExtensions : UnitTestBase
{
    #region Constructors/Finalizers

    //----------------------------------------------------------------------------
    /// <summary>
    ///   Initializes a new instance of the <see cref="UsingPrimeTestClockServiceCollectionExtensions"/> class.
    /// </summary>
    /// <param name="xUnitTestOutputHelper">
    ///   The xUnit test output helper that can be used to output test messages.
    /// </param>
    public UsingPrimeTestClockServiceCollectionExtensions (ITestOutputHelper xUnitTestOutputHelper)
        : base(xUnitTestOutputHelper)
    {
    }
    //----------------------------------------------------------------------------

    #endregion Constructors/Finalizers

    /// <summary>
    ///   Verifies that <see cref="PrimeTestClockServiceCollectionExtensions.AddPrimeTestClock(IServiceCollection)"/>
    ///   throws <see cref="ArgumentNullException"/> when the service collection is null.
    /// </summary>
    [Fact]
    public void AddPrimeTestClock_WithNullServices_ThrowsArgumentNullException ()
    {
        IServiceCollection? services = null;
        Action act = () => services!.AddPrimeTestClock();
        act.Should().Throw<ArgumentNullException>().WithParameterName("services");
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies that after
    ///   <see cref="PrimeTestClockServiceCollectionExtensions.AddPrimeTestClock(IServiceCollection)"/>,
    ///   building the provider resolves <see cref="IPrimeTestClock"/>, <see cref="IPrimeClock"/>,
    ///   and <see cref="IPrimeTime"/> to the same <see cref="PrimeTestClock"/> singleton.
    /// </summary>
    [Fact]
    public void AddPrimeTestClock_BuildServiceProvider_ResolvesAllPrimeAbstractionsToPrimeTestClockSingleton ()
    {
        IServiceCollection services = new ServiceCollection();
        services.AddPrimeTestClock();

        using ServiceProvider provider = services.BuildServiceProvider();
        IPrimeTestClock testClock = provider.GetRequiredService<IPrimeTestClock>();
        IPrimeClock primeClock = provider.GetRequiredService<IPrimeClock>();
        IPrimeTime primeTime = provider.GetRequiredService<IPrimeTime>();
        IPrimeTestClock secondTestClock = provider.GetRequiredService<IPrimeTestClock>();

        testClock.Should().NotBeNull().And.BeOfType<PrimeTestClock>();
        primeClock.Should().BeSameAs(testClock);
        primeTime.Should().BeSameAs(testClock);
        secondTestClock.Should().BeSameAs(testClock);
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies that
    ///   <see cref="PrimeTestClockServiceCollectionExtensions.AddPrimeTestClock(IServiceCollection)"/>
    ///   returns the same service collection instance for fluent chaining and adds singleton
    ///   descriptors for <see cref="IPrimeTestClock"/>, <see cref="IPrimeClock"/>, and
    ///   <see cref="IPrimeTime"/> forwarding to the same test clock instance.
    /// </summary>
    [Fact]
    public void AddPrimeTestClock_ReturnsSameServiceCollectionAndRegistersSingletonDescriptors ()
    {
        IServiceCollection services = new ServiceCollection();

        IServiceCollection returned = services.AddPrimeTestClock();

        returned.Should().BeSameAs(services);
        services.Should().HaveCount(3);

        ServiceDescriptor testClockDescriptor = services.Single(d => d.ServiceType == typeof(IPrimeTestClock));
        testClockDescriptor.ImplementationType.Should().Be(typeof(PrimeTestClock));
        testClockDescriptor.Lifetime.Should().Be(ServiceLifetime.Singleton);

        ServiceDescriptor primeClockDescriptor = services.Single(d => d.ServiceType == typeof(IPrimeClock));
        primeClockDescriptor.Lifetime.Should().Be(ServiceLifetime.Singleton);
        primeClockDescriptor.ImplementationFactory.Should().NotBeNull();

        ServiceDescriptor primeTimeDescriptor = services.Single(d => d.ServiceType == typeof(IPrimeTime));
        primeTimeDescriptor.Lifetime.Should().Be(ServiceLifetime.Singleton);
        primeTimeDescriptor.ImplementationFactory.Should().NotBeNull();

        using ServiceProvider provider = services.BuildServiceProvider();
        IPrimeTestClock registeredTestClock = provider.GetRequiredService<IPrimeTestClock>();
        IPrimeClock factoryPrimeClock = (IPrimeClock)primeClockDescriptor.ImplementationFactory!.Invoke(provider);
        IPrimeTime factoryPrimeTime = (IPrimeTime)primeTimeDescriptor.ImplementationFactory!.Invoke(provider);

        factoryPrimeClock.Should().BeSameAs(registeredTestClock);
        factoryPrimeTime.Should().BeSameAs(registeredTestClock);
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies that
    ///   <see cref="PrimeTestClockServiceCollectionExtensions.AddPrimeTestClock(IServiceCollection)"/>
    ///   appends test-clock registrations so the resolved <see cref="IPrimeTestClock"/>,
    ///   <see cref="IPrimeClock"/>, and <see cref="IPrimeTime"/> are all the
    ///   <see cref="PrimeTestClock"/> registration added by this method.
    /// </summary>
    [Fact]
    public void AddPrimeTestClock_WithPreRegisteredIPrimeTestClockAndIPrimeClock_ResolvesPrimeTestClockForAllPrimeAbstractions ()
    {
        IServiceCollection services = new ServiceCollection();
        IPrimeTestClock existingTestClock = new PrimeTestClock();
        IPrimeClock existingPrimeClock = new PrimeTestClock();

        services.AddSingleton(existingTestClock);
        services.AddSingleton(existingPrimeClock);

        services.AddPrimeTestClock();

        using ServiceProvider provider = services.BuildServiceProvider();
        IPrimeTestClock resolvedTestClock = provider.GetRequiredService<IPrimeTestClock>();
        IPrimeClock resolvedPrimeClock = provider.GetRequiredService<IPrimeClock>();
        IPrimeTime resolvedPrimeTime = provider.GetRequiredService<IPrimeTime>();

        resolvedTestClock.Should().BeOfType<PrimeTestClock>();
        resolvedTestClock.Should().NotBeSameAs(existingTestClock);
        resolvedPrimeClock.Should().BeSameAs(resolvedTestClock);
        resolvedPrimeTime.Should().BeSameAs(resolvedTestClock);
        resolvedPrimeClock.Should().NotBeSameAs(existingPrimeClock);
    }
    //----------------------------------------------------------------------------
}
//################################################################################
