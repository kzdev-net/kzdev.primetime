// Copyright (c) Kevin Zehrer
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using System.Reflection;

using AwesomeAssertions;

using KZDev.PrimeTime.Tests;
using KZDev.SystemClock.PrimeTime.Testing;

namespace KZDev.SystemClock.PrimeTime.Testing.UnitTests;

//################################################################################
/// <summary>
///   Unit tests for shared testing timer contracts in the SystemClock testing package.
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
    ///   The xUnit test output helper that can be used to output test messages.
    /// </param>
    public UsingCommonTimerContracts (ITestOutputHelper xUnitTestOutputHelper)
        : base(xUnitTestOutputHelper)
    {
    }
    //----------------------------------------------------------------------------

    #endregion Constructors/Finalizers

    #region IPrimeTestClock contract

    /// <summary>
    ///   Verifies that <see cref="IPrimeTestClock"/> extends <see cref="IPrimeTestTime"/> and
    ///   <see cref="IPrimeClock"/>, and that <see cref="IPrimeTestClock.ClockEvents"/> uses
    ///   <see cref="PrimeTestClockEventHandler"/>.
    /// </summary>
    [Fact]
    public void IPrimeTestClock_ExtendsTestTimeAndClock_AndClockEventsUsesPrimeTestClockEventHandler ()
    {
        typeof(IPrimeTestClock).IsInterface.Should().BeTrue();
        typeof(IPrimeTestClock).GetInterfaces().Should().Contain(typeof(IPrimeTestTime));
        typeof(IPrimeTestClock).GetInterfaces().Should().Contain(typeof(IPrimeClock));
        EventInfo? clockEvents = typeof(IPrimeTestClock).GetEvent(nameof(IPrimeTestClock.ClockEvents));
        clockEvents.Should().NotBeNull();
        clockEvents!.EventHandlerType.Should().Be(typeof(PrimeTestClockEventHandler));
    }
    //----------------------------------------------------------------------------

    #endregion IPrimeTestClock contract
}
//################################################################################
