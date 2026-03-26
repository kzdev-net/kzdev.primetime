// Copyright (c) Kevin Zehrer
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

extern alias NodaImpl;

using AwesomeAssertions;
using KZDev.PrimeTime.Tests;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace KZDev.PrimeTime.UnitTests;

/// <summary>
///   Smoke tests that the NodaTime-backed clock remains usable when the test project references both
///   the core <c>KZDev.PrimeTime</c> project and the NodaTime implementation (mirrors the bundled NuGet layout).
/// </summary>
/// <remarks>
///   <see cref="NodaImpl"/> avoids CS0436 when both core and NodaTime assemblies expose the same shared contract types.
/// </remarks>
public class UsingBundledNodaTimeClock : UnitTestBase
{
    #region Constructors/Finalizers

    /// <summary>
    ///   Initializes a new instance of the <see cref="UsingBundledNodaTimeClock"/> class.
    /// </summary>
    /// <param name="xUnitTestOutputHelper">
    ///   The Xunit test output helper that can be used to output test messages.
    /// </param>
    public UsingBundledNodaTimeClock (ITestOutputHelper xUnitTestOutputHelper)
        : base(xUnitTestOutputHelper)
    {
    }

    #endregion Constructors/Finalizers

    /// <summary>
    ///   Verifies that registering the bundled NodaTime clock resolves <c>IPrimeClock</c> from a built service provider.
    /// </summary>
    [Fact]
    public void AddPrimeClock_BuildServiceProvider_ResolvesIPrimeClock ()
    {
        IServiceCollection services = new ServiceCollection();
        NodaImpl.KZDev.PrimeTime.PrimeClockServiceCollectionExtensions.AddPrimeClock(services);
        using ServiceProvider provider = services.BuildServiceProvider();
        NodaImpl.KZDev.PrimeTime.IPrimeClock clock = provider.GetRequiredService<NodaImpl.KZDev.PrimeTime.IPrimeClock>();
        clock.Should().NotBeNull();
    }
}
