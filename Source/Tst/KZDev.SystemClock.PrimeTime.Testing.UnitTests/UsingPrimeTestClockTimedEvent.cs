// Copyright (c) Kevin Zehrer
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;

using AwesomeAssertions;

using KZDev.PrimeTime.Tests;

namespace KZDev.SystemClock.PrimeTime.Testing.UnitTests;

//################################################################################
/// <summary>
///   Unit tests for <see cref="PrimeTestClockTimedEvent"/> and timed <see cref="PrimeTestClockEvent"/>
///   payloads in the SystemClock testing package.
/// </summary>
[ExcludeFromCodeCoverage]
public class UsingPrimeTestClockTimedEvent : UnitTestBase
{
    #region Constructors/Finalizers

    //----------------------------------------------------------------------------
    /// <summary>
    ///   Initializes a new instance of the <see cref="UsingPrimeTestClockTimedEvent"/> class.
    /// </summary>
    /// <param name="xUnitTestOutputHelper">
    ///   The xUnit test output helper that can be used to output test messages.
    /// </param>
    public UsingPrimeTestClockTimedEvent (ITestOutputHelper xUnitTestOutputHelper)
        : base(xUnitTestOutputHelper)
    {
    }
    //----------------------------------------------------------------------------

    #endregion Constructors/Finalizers

    #region ClockTime

    //----------------------------------------------------------------------------
    /// <summary>
    ///   Verifies that <see cref="PrimeTestClockTimedEvent.ClockTime"/> is normalized to UTC with zero offset
    ///   when the internal constructor receives a non-zero offset.
    /// </summary>
    [Fact]
    public void NewTimeEvent_WithNonZeroOffsetClockTime_ExposesUtcClockTimeWithZeroOffset ()
    {
        DateTimeOffset input = new(2024, 6, 1, 12, 0, 0, TimeSpan.FromHours(5));
        PrimeTestClockNewTimeEvent clockEvent = new(input, TimeSpan.FromSeconds(1));

        clockEvent.ClockTime.Offset.Should().Be(TimeSpan.Zero);
        clockEvent.ClockTime.UtcDateTime.Should().Be(input.UtcDateTime);
    }
    //----------------------------------------------------------------------------

    #endregion ClockTime

    #region RunRateTimeSpan

    //----------------------------------------------------------------------------
    /// <summary>
    ///   Verifies that a stopped-clock payload exposes a <see langword="null"/> runner rate.
    /// </summary>
    [Fact]
    public void NewTimeEvent_WithNullRunRate_ExposesNullRunRateTimeSpan ()
    {
        DateTimeOffset utc = new(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
        PrimeTestClockNewTimeEvent clockEvent = new(utc, null);

        clockEvent.RunRateTimeSpan.Should().BeNull();
    }
    //----------------------------------------------------------------------------

    #endregion RunRateTimeSpan
}
//################################################################################
