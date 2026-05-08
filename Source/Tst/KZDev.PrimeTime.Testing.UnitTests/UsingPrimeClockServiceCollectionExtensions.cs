// Copyright (c) Kevin Zehrer
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;

using AwesomeAssertions;

using KZDev.PrimeTime.Tests;

using Microsoft.Extensions.DependencyInjection;

using NodaTime;
using NodaTime.Testing;

namespace KZDev.PrimeTime.Testing.UnitTests;

//################################################################################
/// <summary>
///   Testing-surface unit tests for <see cref="PrimeClockServiceCollectionExtensions"/>.
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
    ///   Verifies that <see cref="PrimeClockServiceCollectionExtensions.AddPrimeClock(IServiceCollection)"/> preserves
    ///   existing singleton registrations for <see cref="IClock"/> and <see cref="IPrimeClock"/> while still wiring
    ///   <see cref="IPrimeTime"/> to the registered <see cref="IPrimeClock"/>.
    /// </summary>
    [Fact]
    public void AddPrimeClock_WithPreRegisteredIClockAndIPrimeClock_PreservesExistingSingletonsAndForwardsIPrimeTime ()
    {
        IServiceCollection services = new ServiceCollection();
        FakeClock fakeNodaClock = new(Instant.FromUtc(2026, 1, 1, 0, 0, 0));
        IPrimeClock existingPrimeClock = new PrimeTestClock(Instant.FromUtc(2026, 1, 1, 0, 0, 0), DateTimeZone.Utc);
        services.AddSingleton<IClock>(fakeNodaClock);
        services.AddSingleton(existingPrimeClock);

        services.AddPrimeClock();

        using ServiceProvider provider = services.BuildServiceProvider();
        provider.GetRequiredService<IClock>().Should().BeSameAs(fakeNodaClock);
        provider.GetRequiredService<IPrimeClock>().Should().BeSameAs(existingPrimeClock);
        provider.GetRequiredService<IPrimeTime>().Should().BeSameAs(existingPrimeClock);
    }
    //----------------------------------------------------------------------------
}
//################################################################################
