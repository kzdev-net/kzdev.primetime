// Copyright (c) Kevin Zehrer
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;

using AwesomeAssertions;

using KZDev.PrimeTime.Tests;

using Microsoft.Extensions.DependencyInjection;

using NodaTime;
using NodaTime.Testing;

namespace KZDev.PrimeTime.UnitTests;

//################################################################################
/// <summary>
///   Unit tests for <see cref="PrimeClockServiceCollectionExtensions.AddPrimeClock(IServiceCollection)"/>
///   with the NodaTime-backed <see cref="PrimeClock"/> implementation.
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
        IPrimeTime primeTime = provider.GetRequiredService<IPrimeTime>();
        primeTime.Should().BeSameAs(clock);
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies that <see cref="PrimeClockServiceCollectionExtensions.AddPrimeClock(IServiceCollection)"/>
    ///   returns the same service collection instance for fluent chaining and adds singleton
    ///   descriptors for <see cref="IClock"/> (<see cref="NodaTime.SystemClock"/>),
    ///   <see cref="IPrimeClock"/> implemented by <see cref="PrimeClock"/>, and
    ///   <see cref="IPrimeTime"/> forwarding to the same <see cref="IPrimeClock"/> instance.
    /// </summary>
    [Fact]
    public void AddPrimeClock_ReturnsSameServiceCollectionAndRegistersSingletonDescriptors ()
    {
        IServiceCollection services = new ServiceCollection();

        IServiceCollection returned = services.AddPrimeClock();

        returned.Should().BeSameAs(services);
        services.Should().HaveCount(3);

        ServiceDescriptor nodaClockDescriptor = services.Single(d => d.ServiceType == typeof(IClock));
        nodaClockDescriptor.Lifetime.Should().Be(ServiceLifetime.Singleton);
        object? nodaSingleton = nodaClockDescriptor.ImplementationInstance
            ?? nodaClockDescriptor.ImplementationFactory?.Invoke(null!);
        nodaSingleton.Should().BeSameAs(NodaTime.SystemClock.Instance);

        ServiceDescriptor primeClockDescriptor = services.Single(d => d.ServiceType == typeof(IPrimeClock));
        primeClockDescriptor.ImplementationType.Should().Be(typeof(PrimeClock));
        primeClockDescriptor.Lifetime.Should().Be(ServiceLifetime.Singleton);

        ServiceDescriptor primeTimeDescriptor = services.Single(d => d.ServiceType == typeof(IPrimeTime));
        primeTimeDescriptor.Lifetime.Should().Be(ServiceLifetime.Singleton);
        primeTimeDescriptor.ImplementationFactory.Should().NotBeNull();
        using ServiceProvider provider = services.BuildServiceProvider();
        IPrimeClock registeredPrimeClock = provider.GetRequiredService<IPrimeClock>();
        object? factoryResult = primeTimeDescriptor.ImplementationFactory!.Invoke(provider);
        IPrimeTime forwardedPrimeTime = (IPrimeTime)factoryResult!;
        forwardedPrimeTime.Should().BeSameAs(registeredPrimeClock);
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies that when <see cref="IClock"/> is pre-registered and <see cref="IPrimeClock"/> is not,
    ///   <see cref="PrimeClockServiceCollectionExtensions.AddPrimeClock(IServiceCollection)"/> creates
    ///   <see cref="PrimeClock"/> and that its current instant is sourced from the pre-registered clock.
    /// </summary>
    [Fact]
    public void AddPrimeClock_WithPreRegisteredIClockOnly_ResolvesPrimeClockUsingRegisteredClock ()
    {
        IServiceCollection services = new ServiceCollection();
        Instant fakeNow = Instant.FromUtc(2026, 2, 3, 4, 5, 6);
        FakeClock fakeNodaClock = new(fakeNow);
        services.AddSingleton<IClock>(fakeNodaClock);

        services.AddPrimeClock();

        using ServiceProvider provider = services.BuildServiceProvider();
        IPrimeClock clock = provider.GetRequiredService<IPrimeClock>();
        clock.Should().BeOfType<PrimeClock>();
        clock.NowInstant.Should().Be(fakeNow);
        provider.GetRequiredService<IPrimeTime>().Should().BeSameAs(clock);
    }
    //----------------------------------------------------------------------------
}
//################################################################################
