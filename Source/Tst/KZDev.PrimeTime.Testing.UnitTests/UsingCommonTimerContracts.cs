// Copyright (c) Kevin Zehrer
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using System.Reflection;

using AwesomeAssertions;

using KZDev.PrimeTime.Tests;

namespace KZDev.PrimeTime.Testing.UnitTests;

//################################################################################
/// <summary>
///   Unit tests for shared testing timer contracts.
/// </summary>
[ExcludeFromCodeCoverage]
public class UsingCommonTimerContracts : UnitTestBase
{
    #region Constructors/Finalizers

    //----------------------------------------------------------------------------
    /// <summary>
    ///   Initializes a new instance of the <see cref="UsingCommonTimerContracts"/> class.
    /// </summary>
    /// <param name="xUnitTestOutputHelper">
    ///   The Xunit test output helper that can be used to output test messages.
    /// </param>
    public UsingCommonTimerContracts (ITestOutputHelper xUnitTestOutputHelper)
        : base(xUnitTestOutputHelper)
    {
    }
    //----------------------------------------------------------------------------

    #endregion Constructors/Finalizers

    #region IPrimeTestTime contract

    /// <summary>
    ///   Verifies that <see cref="IPrimeTestTime"/> exists, extends <see cref="IPrimeTime"/>, and declares IsRunning.
    /// </summary>
    [Fact]
    public void IPrimeTestTime_ExistsExtendsIPrimeTimeAndDeclaresIsRunning ()
    {
        typeof(IPrimeTestTime).IsInterface.Should().BeTrue();
        typeof(IPrimeTestTime).GetInterfaces().Should().Contain(typeof(IPrimeTime));
        PropertyInfo? prop = typeof(IPrimeTestTime).GetProperty("IsRunning");
        prop.Should().NotBeNull();
        prop.PropertyType.Should().Be(typeof(bool));
    }
    //----------------------------------------------------------------------------

    #endregion IPrimeTestTime contract

    #region IPrimeTestClock contract

    /// <summary>
    ///   Verifies that <see cref="IPrimeTestClock"/> extends <see cref="IPrimeTestTime"/> and
    ///   <see cref="IPrimeClock"/>, and that <see cref="IPrimeTestClock.ClockEvents"/> uses
    ///   <see cref="EventHandler{T}"/> of <see cref="ClockTimeChangedEventArgs"/>.
    /// </summary>
    [Fact]
    public void IPrimeTestClock_ExtendsTestTimeAndClock_AndClockEventsUsesClockTimeChangedEventArgs ()
    {
        typeof(IPrimeTestClock).IsInterface.Should().BeTrue();
        typeof(IPrimeTestClock).GetInterfaces().Should().Contain(typeof(IPrimeTestTime));
        typeof(IPrimeTestClock).GetInterfaces().Should().Contain(typeof(IPrimeClock));
        EventInfo? clockEvents = typeof(IPrimeTestClock).GetEvent("ClockEvents");
        clockEvents.Should().NotBeNull();
        clockEvents!.EventHandlerType.Should().Be(typeof(EventHandler<ClockTimeChangedEventArgs>));
    }
    //----------------------------------------------------------------------------

    #endregion IPrimeTestClock contract
}
//################################################################################
