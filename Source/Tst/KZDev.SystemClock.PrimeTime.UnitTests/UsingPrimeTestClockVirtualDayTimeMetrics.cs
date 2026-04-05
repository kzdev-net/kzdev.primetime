// Copyright (c) Kevin Zehrer
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
//
// Virtual day-time timer metrics on PrimeTestClock (ElapsedTime, TimeUntilNextCallback).

#if NET

using System;
using System.Diagnostics.CodeAnalysis;

using AwesomeAssertions;
using KZDev.PrimeTime.Tests;

namespace KZDev.SystemClock.PrimeTime.UnitTests;

/// <summary>
///   Verifies <see cref="IClockTimer.ElapsedTime"/> and <see cref="IClockTimer.TimeUntilNextCallback"/> for
///   <see cref="PrimeTestClock"/> day-time registrations across machine local time zones.
/// </summary>
[ExcludeFromCodeCoverage]
public class UsingPrimeTestClockVirtualDayTimeMetrics : UnitTestBase
{
    #region Constructors/Finalizers

    /// <summary>
    ///   Initializes a new instance of the <see cref="UsingPrimeTestClockVirtualDayTimeMetrics"/> class.
    /// </summary>
    /// <param name="xUnitTestOutputHelper">The xUnit test output helper.</param>
    public UsingPrimeTestClockVirtualDayTimeMetrics (ITestOutputHelper xUnitTestOutputHelper)
        : base(xUnitTestOutputHelper)
    {
    }

    #endregion Constructors/Finalizers

    /// <summary>
    ///   Computes the expected value in milliseconds for <see cref="IClockTimer.TimeUntilNextCallback"/> before the
    ///   first fire of a <c>local</c> day-time registration on <see cref="PrimeTestClock"/>: the span from
    ///   the virtual clock's current instant to the UTC instant of the next wall-clock occurrence of
    ///   <paramref name="targetTimeOfDay"/> in the machine's local time zone.
    /// </summary>
    /// <remarks>
    ///   <para>
    ///     Uses <see cref="DayTimeBclLocalWallTimeScheduling.GetDelayUntilNextLocalDayTime"/> with
    ///     <see cref="IPrimeClock.LocalScheduleTimeZone"/> and default <see cref="DayTimeTimerOptions"/> (same as
    ///     production <see cref="ClockDayTimeTimerRegistration"/> local scheduling).
    ///   </para>
    /// </remarks>
    /// <param name="clock">The virtual test clock (local "now" is <see cref="IPrimeTestClock.LocalNowOffset"/>).</param>
    /// <param name="targetTimeOfDay">The registered local time of day since local midnight.</param>
    /// <returns>
    ///   Whole milliseconds from <see cref="DateTimeOffset.ToUniversalTime"/> of <see cref="IPrimeTestClock.LocalNowOffset"/>
    ///   to the next due UTC instant; suitable to compare to <see cref="IClockTimer.TimeUntilNextCallback"/>.
    /// </returns>
    private static long ExpectedMillisecondsUntilNextLocalFire (IPrimeTestClock clock, TimeOnly targetTimeOfDay)
    {
        DayTimeTimerOptions defaults = new();
        TimeSpan delay = DayTimeBclLocalWallTimeScheduling.GetDelayUntilNextLocalDayTime(clock.LocalNowOffset,
            clock.LocalScheduleTimeZone, targetTimeOfDay, defaults.SkippedTimeBehavior, defaults.DuplicateTimeBehavior);
        DateTimeOffset nextDueUtc = new DateTimeOffset((clock.LocalNowOffset + delay).UtcDateTime, TimeSpan.Zero);
        return (long)(nextDueUtc - clock.UtcNowOffset).TotalMilliseconds;
    }

    /// <summary>
    ///   Verifies that before the first fire, <see cref="IClockTimer.ElapsedTime"/> is <c>-1</c> and
    ///   <see cref="IClockTimer.TimeUntilNextCallback"/> matches the next local occurrence.
    /// </summary>
    [Fact]
    public void RegisterTimeOfDay_Local_BeforeDue_ElapsedTimeNegativeOne_TimeUntilNextMatchesVirtualSchedule ()
    {
        DateTimeOffset utcStart = new DateTimeOffset(2025, 1, 1, 1, 0, 0, TimeSpan.Zero);
        IPrimeTestClock clock = new PrimeTestClock(utcStart);
        TimeOnly threeAm = new TimeOnly(3, 0);
        LocalTimeOfDay target = new LocalTimeOfDay(threeAm);
        using IClockDayTimeTimer timer = clock.RegisterTimeOfDay(target, () => { },
            cancellationToken: TestContext.Current.CancellationToken);
        timer.ElapsedTime.Should().Be(-1);
        long expectedMs = ExpectedMillisecondsUntilNextLocalFire(clock, threeAm);
        // Virtual time and the BCL scheduling oracle are deterministic; no real-clock slack required.
        timer.TimeUntilNextCallback.Should().Be(expectedMs);
    }

    /// <summary>
    ///   Verifies that after firing, advancing virtual time increases <see cref="IClockTimer.ElapsedTime"/> by the
    ///   same amount (uses <see cref="IClockTimer.TimeUntilNextCallback"/> to reach the first fire so the test is
    ///   time-zone agnostic).
    /// </summary>
    [Fact]
    public void RegisterTimeOfDay_Local_AfterFirstFire_Advance_ElapsedTimeTracksVirtualLocalClock ()
    {
        DateTimeOffset utcStart = new DateTimeOffset(2025, 3, 10, 8, 30, 0, TimeSpan.Zero);
        IPrimeTestClock clock = new PrimeTestClock(utcStart);
        TimeOnly targetTime = new TimeOnly(4, 15);
        LocalTimeOfDay target = new LocalTimeOfDay(targetTime);
        int fireCount = 0;
        using IClockDayTimeTimer timer = clock.RegisterTimeOfDay(target, () => fireCount++,
            cancellationToken: TestContext.Current.CancellationToken);
        long advanceToFireMs = timer.TimeUntilNextCallback;
        advanceToFireMs.Should().BeGreaterThan(0);
        clock.Advance(TimeSpan.FromMilliseconds(advanceToFireMs));
        fireCount.Should().Be(1);
        clock.Advance(TimeSpan.FromHours(1));
        long expectedElapsedMs = (long)TimeSpan.FromHours(1).TotalMilliseconds;
        timer.ElapsedTime.Should()
            .BeInRange(expectedElapsedMs - VirtualClockTimerAssertionToleranceMilliseconds,
                expectedElapsedMs + VirtualClockTimerAssertionToleranceMilliseconds);
    }

    /// <summary>
    ///   Verifies UTC day-time registrations use the virtual UTC clock for
    ///   <see cref="IClockTimer.TimeUntilNextCallback"/>.
    /// </summary>
    [Fact]
    public void RegisterTimeOfDay_Utc_BeforeDue_TimeUntilNextCallbackUsesUtcDayBoundary ()
    {
        DateTimeOffset utcStart = new DateTimeOffset(2025, 6, 15, 10, 0, 0, TimeSpan.Zero);
        IPrimeTestClock clock = new PrimeTestClock(utcStart);
        UtcTimeOfDay twoPmUtc = new UtcTimeOfDay(new TimeOnly(14, 0));
        using IClockDayTimeTimer timer = clock.RegisterTimeOfDay(twoPmUtc, () => { },
            cancellationToken: TestContext.Current.CancellationToken);
        timer.IsLocalTimeRepresentation.Should().BeFalse();
        long expectedMs = (long)TimeSpan.FromHours(4).TotalMilliseconds;
        timer.TimeUntilNextCallback.Should()
            .BeInRange(expectedMs - VirtualClockTimerAssertionToleranceMilliseconds,
                expectedMs + VirtualClockTimerAssertionToleranceMilliseconds);
    }
}

#endif
