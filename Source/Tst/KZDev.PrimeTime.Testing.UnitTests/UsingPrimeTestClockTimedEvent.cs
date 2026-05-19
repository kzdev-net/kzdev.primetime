// Copyright (c) Kevin Zehrer
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;

using AwesomeAssertions;

using KZDev.PrimeTime.Tests;

using NodaTime;

using NodaPrimeTestClockNewTimeEvent = KZDev.PrimeTime.Testing.PrimeTestClockNewTimeEvent;
using SystemClockPrimeTestClockNewTimeEvent =
    KZDev.SystemClock.PrimeTime.Testing.PrimeTestClockNewTimeEvent;

namespace KZDev.PrimeTime.Testing.UnitTests;

//################################################################################
/// <summary>
///   Unit tests for <see cref="PrimeTestClockTimedEvent"/> payloads and cross-package parity with the
///   SystemClock testing package.
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
    ///   Verifies that <see cref="PrimeTestClockTimedEvent.ClockTime"/> is UTC with zero offset when built from
    ///   <see cref="Instant"/> storage.
    /// </summary>
    [Fact]
    public void NewTimeEvent_FromInstant_ExposesUtcClockTimeWithZeroOffset ()
    {
        Instant instant = Instant.FromUtc(2024, 6, 1, 12, 0, 0);
        NodaPrimeTestClockNewTimeEvent clockEvent = new(instant, Duration.FromSeconds(1));

        clockEvent.ClockTime.Offset.Should().Be(TimeSpan.Zero);
        clockEvent.ClockInstant.Should().Be(instant);
    }
    //----------------------------------------------------------------------------

    //----------------------------------------------------------------------------
    /// <summary>
    ///   Verifies that SystemClock and Noda timed events expose the same <see cref="PrimeTestClockTimedEvent.ClockTime"/>
    ///   for an equivalent instant when the clock is stopped.
    /// </summary>
    [Fact]
    public void EquivalentInstant_SystemClockAndNodaNewTimeEvents_ExposeMatchingClockTimeWhenStopped ()
    {
        Instant instant = Instant.FromUtc(2025, 3, 15, 10, 30, 0);
        DateTimeOffset systemClockInput = new(instant.ToDateTimeUtc(), TimeSpan.Zero);
        SystemClockPrimeTestClockNewTimeEvent systemClockEvent = new(systemClockInput, null);
        NodaPrimeTestClockNewTimeEvent nodaEvent = new(instant, null);

        systemClockEvent.ClockTime.Should().Be(nodaEvent.ClockTime);
    }
    //----------------------------------------------------------------------------

    //----------------------------------------------------------------------------
    /// <summary>
    ///   Verifies that SystemClock event construction normalizes non-zero offsets to the same
    ///   <see cref="PrimeTestClockTimedEvent.ClockTime"/> as the Noda event for the same instant.
    /// </summary>
    [Fact]
    public void NonZeroOffset_SystemClockNewTimeEvent_MatchesNodaClockTimeForSameInstant ()
    {
        Instant instant = Instant.FromUtc(2025, 3, 15, 10, 30, 0);
        DateTimeOffset withOffset = new(2025, 3, 15, 14, 30, 0, TimeSpan.FromHours(4));
        SystemClockPrimeTestClockNewTimeEvent systemClockEvent = new(withOffset, null);
        NodaPrimeTestClockNewTimeEvent nodaEvent = new(instant, null);

        systemClockEvent.ClockTime.Should().Be(nodaEvent.ClockTime);
        systemClockEvent.ClockTime.Offset.Should().Be(TimeSpan.Zero);
    }
    //----------------------------------------------------------------------------

    #endregion ClockTime

    #region Run rate when stopped

    //----------------------------------------------------------------------------
    /// <summary>
    ///   Verifies that a stopped-clock payload exposes <see langword="null"/> runner rate properties on the Noda
    ///   event surface.
    /// </summary>
    [Fact]
    public void NewTimeEvent_WithNullRunRateDuration_ExposesNullRunRateProperties ()
    {
        Instant instant = Instant.FromUtc(2024, 1, 1, 0, 0, 0);
        NodaPrimeTestClockNewTimeEvent clockEvent = new(instant, null);

        clockEvent.RunRateDuration.Should().BeNull();
        clockEvent.RunRateTimeSpan.Should().BeNull();
    }
    //----------------------------------------------------------------------------

    //----------------------------------------------------------------------------
    /// <summary>
    ///   Verifies that SystemClock and Noda timed events both expose <see langword="null"/> runner rates when
    ///   representing a stopped clock.
    /// </summary>
    [Fact]
    public void EquivalentInstant_SystemClockAndNodaNewTimeEvents_ExposeNullRunRateWhenStopped ()
    {
        Instant instant = Instant.FromUtc(2025, 7, 4, 18, 0, 0);
        DateTimeOffset systemClockInput = new(instant.ToDateTimeUtc(), TimeSpan.Zero);
        SystemClockPrimeTestClockNewTimeEvent systemClockEvent = new(systemClockInput, null);
        NodaPrimeTestClockNewTimeEvent nodaEvent = new(instant, null);

        systemClockEvent.RunRateTimeSpan.Should().BeNull();
        nodaEvent.RunRateDuration.Should().BeNull();
        nodaEvent.RunRateTimeSpan.Should().BeNull();
    }
    //----------------------------------------------------------------------------

    #endregion Run rate when stopped
}
//################################################################################
