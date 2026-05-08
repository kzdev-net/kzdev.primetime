// Copyright (c) Kevin Zehrer
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

#if NET

using System;
using System.Diagnostics.CodeAnalysis;

using AwesomeAssertions;
using KZDev.PrimeTime.Tests;
using NodaTime;
using NodaTime.TimeZones;

namespace KZDev.PrimeTime.Testing.UnitTests;

/// <summary>
///   Verifies <see cref="PrimeTestClock"/> virtual local day-time scheduling honors
///   <see cref="DayTimeTimerOptions"/> across DST transitions (parity with
///   <see cref="DayTimeNodaLocalWallTimeScheduling"/>).
/// </summary>
[ExcludeFromCodeCoverage]
public class UsingPrimeTestClockVirtualDayTimeDstOptions : UnitTestBase
{
    /// <summary>
    ///   Initializes a new instance of the <see cref="UsingPrimeTestClockVirtualDayTimeDstOptions"/> class.
    /// </summary>
    /// <param name="xUnitTestOutputHelper">The xUnit test output helper.</param>
    public UsingPrimeTestClockVirtualDayTimeDstOptions (ITestOutputHelper xUnitTestOutputHelper)
        : base(xUnitTestOutputHelper)
    {
    }

    /// <summary>
    ///   Builds a synthetic US Eastern-style zone: DST from 2:00 on the second Sunday in March through
    ///   2:00 on the first Sunday in November (base offset -5, daylight delta +1).
    /// </summary>
    /// <returns>A custom <see cref="TimeZoneInfo"/> with predictable spring and fall transitions.</returns>
    private static TimeZoneInfo CreateUsEasternStyleZone ()
    {
        TimeSpan standardOffset = TimeSpan.FromHours(-5);
        TimeSpan daylightDelta = TimeSpan.FromHours(1);
        DateTime ruleTimeOfDay = new(1, 1, 1, 2, 0, 0, DateTimeKind.Unspecified);
        TimeZoneInfo.TransitionTime springStart = TimeZoneInfo.TransitionTime.CreateFloatingDateRule(ruleTimeOfDay, 3,
            2, DayOfWeek.Sunday);
        TimeZoneInfo.TransitionTime fallEnd = TimeZoneInfo.TransitionTime.CreateFloatingDateRule(ruleTimeOfDay, 11, 1,
            DayOfWeek.Sunday);
        TimeZoneInfo.AdjustmentRule rule = TimeZoneInfo.AdjustmentRule.CreateAdjustmentRule(new DateTime(2000, 1, 1),
            DateTime.MaxValue.Date,
            daylightDelta,
            springStart,
            fallEnd);
        return TimeZoneInfo.CreateCustomTimeZone("KZDevPrimeTimeTestEastern", standardOffset, "EST", "EST", "EDT",
            [rule]);
    }

    /// <summary>
    ///   Verifies <see cref="IClockTimer.TimeUntilNextCallback"/> for a spring-forward gap target with
    ///   <see cref="SkippedTimeBehavior.Skip"/> matches the Noda scheduling oracle.
    /// </summary>
    [Fact]
    public void RegisterTimeOfDay_Local_SpringGapSkipped_TimeUntilNextCallback_MatchesNodaOracle ()
    {
        TimeZoneInfo tz = CreateUsEasternStyleZone();
        DateTimeZone zone = BclDateTimeZone.FromTimeZoneInfo(tz);
        Instant now = Instant.FromUtc(2025, 3, 9, 6, 0, 0);
        LocalTime target = new(2, 30, 0);
        DayTimeTimerOptions options = new()
        {
            SkippedTimeBehavior = SkippedTimeBehavior.Skip,
            DuplicateTimeBehavior = DuplicateTimeBehavior.RunLast
        };
        IPrimeTestClock clock = new PrimeTestClock(now, zone);
        using IClockDayTimeTimer timer = clock.RegisterTimeOfDay(target, () => { },
            cancellationToken: TestContext.Current.CancellationToken,
            timerOptions: options);
        Duration expected = DayTimeNodaLocalWallTimeScheduling.GetDelayUntilNextLocalDayTime(now, zone, target,
            options.SkippedTimeBehavior, options.DuplicateTimeBehavior);
        long expectedMs = (long)expected.TotalMilliseconds;
        timer.TimeUntilNextCallback.Should()
            .BeInRange(expectedMs - VirtualClockTimerAssertionToleranceMilliseconds,
                expectedMs + VirtualClockTimerAssertionToleranceMilliseconds);
    }

    /// <summary>
    ///   Verifies <see cref="IClockTimer.TimeUntilNextCallback"/> for a spring-forward gap target with
    ///   <see cref="SkippedTimeBehavior.RunAfter"/> matches the Noda scheduling oracle.
    /// </summary>
    [Fact]
    public void RegisterTimeOfDay_Local_SpringGapRunAfter_TimeUntilNextCallback_MatchesNodaOracle ()
    {
        TimeZoneInfo tz = CreateUsEasternStyleZone();
        DateTimeZone zone = BclDateTimeZone.FromTimeZoneInfo(tz);
        Instant now = Instant.FromUtc(2025, 3, 9, 6, 0, 0);
        LocalTime target = new(2, 30, 0);
        DayTimeTimerOptions options = new()
        {
            SkippedTimeBehavior = SkippedTimeBehavior.RunAfter,
            DuplicateTimeBehavior = DuplicateTimeBehavior.RunLast
        };
        IPrimeTestClock clock = new PrimeTestClock(now, zone);
        using IClockDayTimeTimer timer = clock.RegisterTimeOfDay(target, () => { },
            cancellationToken: TestContext.Current.CancellationToken,
            timerOptions: options);
        Duration expected = DayTimeNodaLocalWallTimeScheduling.GetDelayUntilNextLocalDayTime(now, zone, target,
            options.SkippedTimeBehavior, options.DuplicateTimeBehavior);
        long expectedMs = (long)expected.TotalMilliseconds;
        timer.TimeUntilNextCallback.Should()
            .BeInRange(expectedMs - VirtualClockTimerAssertionToleranceMilliseconds,
                expectedMs + VirtualClockTimerAssertionToleranceMilliseconds);
    }
}

#endif

