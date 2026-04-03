// Copyright (c) Kevin Zehrer
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using AwesomeAssertions;
using KZDev.PrimeTime;
using KZDev.PrimeTime.Tests;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace KZDev.PrimeTime.UnitTests;

/// <summary>
///   Smoke tests that the NodaTime-backed clock resolves from DI when the test project references
///   <c>KZDev.PrimeTime</c> (full <see cref="IPrimeClock"/> surface including NodaTime members).
/// </summary>
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
    ///   Verifies that registering the NodaTime-backed clock resolves <c>IPrimeClock</c> from a built service provider.
    /// </summary>
    [Fact]
    public void AddPrimeClock_BuildServiceProvider_ResolvesIPrimeClock ()
    {
        IServiceCollection services = new ServiceCollection();
        PrimeClockServiceCollectionExtensions.AddPrimeClock(services);
        using ServiceProvider provider = services.BuildServiceProvider();
        IPrimeClock clock = provider.GetRequiredService<IPrimeClock>();
        clock.Should().NotBeNull();
    }
}
