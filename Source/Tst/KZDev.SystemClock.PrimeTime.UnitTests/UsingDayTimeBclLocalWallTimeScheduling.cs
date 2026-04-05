// Copyright (c) Kevin Zehrer
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

#if NET

using AwesomeAssertions;
using KZDev.PrimeTime.Tests;

using Microsoft.Extensions.Time.Testing;

namespace KZDev.SystemClock.PrimeTime.UnitTests;

/// <summary>
///   Unit tests for <see cref="DayTimeBclLocalWallTimeScheduling"/> (local wall time + DST policy)
///   and integration with <see cref="IPrimeClock"/> day-time timers under a synthetic zone.
/// </summary>
public class UsingDayTimeBclLocalWallTimeScheduling : UnitTestBase
{
    /// <summary>
    ///   Initializes a new instance of the <see cref="UsingDayTimeBclLocalWallTimeScheduling"/> class.
    /// </summary>
    /// <param name="xUnitTestOutputHelper">The xUnit test output helper.</param>
    public UsingDayTimeBclLocalWallTimeScheduling (ITestOutputHelper xUnitTestOutputHelper)
        : base(xUnitTestOutputHelper)
    {
    }

    /// <summary>
    ///   Builds a synthetic US Eastern–style zone: DST from 2:00 on the second Sunday in March through
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
    ///   Verifies that an invalid spring-gap wall time with <see cref="SkippedTimeBehavior.Skip"/> advances
    ///   to the same clock time on the next local day when it is valid.
    /// </summary>
    [Fact]
    public void GetDelayUntilNextLocalDayTime_SpringGap_Skip_TargetsNextValidDay ()
    {
        TimeZoneInfo zone = CreateUsEasternStyleZone();
        DateTimeOffset now = new(2025, 3, 9, 6, 0, 0, TimeSpan.Zero);
        TimeOnly target = new(2, 30, 0);
        TimeSpan delay = DayTimeBclLocalWallTimeScheduling.GetDelayUntilNextLocalDayTime(now, zone, target,
            SkippedTimeBehavior.Skip, DuplicateTimeBehavior.RunLast);
        DateTimeOffset next = now + delay;
        DateTimeOffset expectedNext = new(2025, 3, 10, 6, 30, 0, TimeSpan.Zero);
        next.Should().Be(expectedNext);
    }

    /// <summary>
    ///   Verifies that <see cref="SkippedTimeBehavior.RunAfter"/> for a spring-gap target resolves to the first
    ///   valid instant after the gap (3:00 AM daylight on the transition day).
    /// </summary>
    [Fact]
    public void GetDelayUntilNextLocalDayTime_SpringGap_RunAfter_ResolvesToInstantAfterGap ()
    {
        TimeZoneInfo zone = CreateUsEasternStyleZone();
        DateTimeOffset now = new(2025, 3, 9, 6, 0, 0, TimeSpan.Zero);
        TimeOnly target = new(2, 30, 0);
        TimeSpan delay = DayTimeBclLocalWallTimeScheduling.GetDelayUntilNextLocalDayTime(now, zone, target,
            SkippedTimeBehavior.RunAfter, DuplicateTimeBehavior.RunLast);
        DateTimeOffset next = now + delay;
        DateTimeOffset expected = new(2025, 3, 9, 7, 0, 0, TimeSpan.Zero);
        next.Should().Be(expected);
        delay.Should().Be(TimeSpan.FromHours(1));
    }

    /// <summary>
    ///   Verifies that <see cref="SkippedTimeBehavior.RunBefore"/> resolves to the last valid instant before the
    ///   spring-forward gap when that instant is still in the future.
    /// </summary>
    [Fact]
    public void GetDelayUntilNextLocalDayTime_SpringGap_RunBefore_ResolvesToInstantBeforeGap ()
    {
        TimeZoneInfo zone = CreateUsEasternStyleZone();
        DateTimeOffset now = new(2025, 3, 9, 6, 0, 0, TimeSpan.Zero);
        TimeOnly target = new(2, 30, 0);
        TimeSpan delay = DayTimeBclLocalWallTimeScheduling.GetDelayUntilNextLocalDayTime(now, zone, target,
            SkippedTimeBehavior.RunBefore, DuplicateTimeBehavior.RunLast);
        DateTimeOffset next = now + delay;
        next.UtcDateTime.Should().BeBefore(new DateTime(2025, 3, 9, 7, 0, 0, DateTimeKind.Utc));
        TimeZoneInfo.ConvertTimeFromUtc(next.UtcDateTime, zone).Should().BeBefore(new DateTime(2025, 3, 9, 2, 0, 0));
    }

    /// <summary>
    ///   Verifies ambiguous fall-back wall times: <see cref="DuplicateTimeBehavior.RunFirst"/> uses the earlier
    ///   UTC instant and <see cref="DuplicateTimeBehavior.RunLast"/> uses the later UTC instant.
    /// </summary>
    [Fact]
    public void TryGetLocalDayTimeFireInstantForDate_FallBack_Ambiguous_RunFirstRunLast_OrderedByUtc ()
    {
        TimeZoneInfo zone = CreateUsEasternStyleZone();
        DateOnly date = new(2025, 11, 2);
        TimeOnly target = new(1, 30, 0);
        DayTimeBclLocalWallTimeScheduling.TryGetLocalDayTimeFireInstantForDate(date, target, zone,
            SkippedTimeBehavior.RunAfter, DuplicateTimeBehavior.RunFirst, out DateTimeOffset firstRun).Should().BeTrue();
        DayTimeBclLocalWallTimeScheduling.TryGetLocalDayTimeFireInstantForDate(date, target, zone,
            SkippedTimeBehavior.RunAfter, DuplicateTimeBehavior.RunLast, out DateTimeOffset lastRun).Should().BeTrue();
        firstRun.UtcDateTime.Should().BeBefore(lastRun.UtcDateTime);
        firstRun.UtcDateTime.Should().Be(new DateTime(2025, 11, 2, 5, 30, 0, DateTimeKind.Utc));
        lastRun.UtcDateTime.Should().Be(new DateTime(2025, 11, 2, 6, 30, 0, DateTimeKind.Utc));
    }

    /// <summary>
    ///   Verifies that a fixed-offset zone still schedules using the zone offset for future calendar days, not the
    ///   current <see cref="DateTimeOffset.Offset"/> drift from a different meaning of “local”.
    /// </summary>
    [Fact]
    public void GetDelayUntilNextLocalDayTime_FixedOffsetZone_UsesZoneRulesNotArbitraryOffset ()
    {
        TimeZoneInfo plusThree = TimeZoneInfo.CreateCustomTimeZone("KZDevPrimeTimeTestPlus3", TimeSpan.FromHours(3),
            null, null);
        DateTimeOffset now = new(2025, 6, 1, 12, 0, 0, TimeSpan.Zero);
        TimeOnly target = new(8, 0, 0);
        TimeSpan delay = DayTimeBclLocalWallTimeScheduling.GetDelayUntilNextLocalDayTime(now, plusThree, target,
            SkippedTimeBehavior.RunAfter, DuplicateTimeBehavior.RunLast);
        DateTimeOffset next = now + delay;
        next.UtcDateTime.Should().Be(new DateTime(2025, 6, 2, 5, 0, 0, DateTimeKind.Utc));
    }

    #region ClockDayTimeTimerRegistration — local policies vs scheduling helper

    /// <summary>
    ///   Creates <see cref="DayTimeTimerOptions"/> with <see cref="ConcurrentTriggerProcessing.RunConcurrently"/>
    ///   and the given skipped/duplicate policies (avoids sequential-callback retry noise on
    ///   <see cref="IClockDayTimeTimer.TimeUntilNextCallback"/>).
    /// </summary>
    /// <param name="skippedTimeBehavior">Skipped spring-gap policy.</param>
    /// <param name="duplicateTimeBehavior">Ambiguous local-time policy.</param>
    /// <returns>Options for <see cref="IPrimeClock.RegisterTimeOfDay"/>.</returns>
    private static DayTimeTimerOptions CreateLocalDayTimeTimerOptions (SkippedTimeBehavior skippedTimeBehavior,
        DuplicateTimeBehavior duplicateTimeBehavior) =>
        new DayTimeTimerOptions
        {
            ConcurrentTriggerProcessing = ConcurrentTriggerProcessing.RunConcurrently,
            SkippedTimeBehavior = skippedTimeBehavior,
            DuplicateTimeBehavior = duplicateTimeBehavior,
        };

    /// <summary>
    ///   Builds a <see cref="PrimeClock"/> over <see cref="FakeTimeProvider"/> at <paramref name="utcNow"/> with
    ///   <paramref name="zone"/> as the provider local (and thus <see cref="IPrimeClock.LocalScheduleTimeZone"/>) zone.
    /// </summary>
    /// <param name="utcNow">Virtual UTC instant.</param>
    /// <param name="zone">Local schedule zone.</param>
    /// <returns>A clock whose local and schedule zone match <paramref name="zone"/>.</returns>
    private static IPrimeClock CreatePrimeClockAtUtcWithLocalZone (DateTimeOffset utcNow, TimeZoneInfo zone)
    {
        FakeTimeProvider fake = new(utcNow);
        fake.SetLocalTimeZone(zone);
        return new PrimeClock(fake);
    }

    /// <summary>
    ///   Verifies <see cref="ClockDayTimeTimerRegistration"/> applies <see cref="SkippedTimeBehavior.RunAfter"/> for
    ///   local day-time scheduling by matching <see cref="IClockDayTimeTimer.TimeUntilNextCallback"/> to
    ///   <see cref="DayTimeBclLocalWallTimeScheduling.GetDelayUntilNextLocalDayTime"/>.
    /// </summary>
    [Fact]
    public void RegisterTimeOfDay_LocalSpringGap_SkippedRunAfter_TimeUntilNextMatchesSchedulingHelper ()
    {
        TimeZoneInfo zone = CreateUsEasternStyleZone();
        DateTimeOffset utcNow = new(2025, 3, 9, 6, 0, 0, TimeSpan.Zero);
        IPrimeClock clock = CreatePrimeClockAtUtcWithLocalZone(utcNow, zone);
        TimeOnly target = new(2, 30, 0);
        DayTimeTimerOptions options = CreateLocalDayTimeTimerOptions(SkippedTimeBehavior.RunAfter,
            DuplicateTimeBehavior.RunLast);
        using IClockDayTimeTimer timer = clock.RegisterTimeOfDay(new LocalTimeOfDay(target), () => { },
            TestContext.Current.CancellationToken, options);
        TimeSpan expected = DayTimeBclLocalWallTimeScheduling.GetDelayUntilNextLocalDayTime(clock.LocalNowOffset,
            clock.LocalScheduleTimeZone, target, options.SkippedTimeBehavior, options.DuplicateTimeBehavior);
        timer.TimeUntilNextCallback.Should().Be((long)expected.TotalMilliseconds);
    }

    /// <summary>
    ///   Verifies <see cref="ClockDayTimeTimerRegistration"/> applies <see cref="SkippedTimeBehavior.RunBefore"/> for
    ///   local day-time scheduling by matching <see cref="IClockDayTimeTimer.TimeUntilNextCallback"/> to the shared
    ///   delay helper.
    /// </summary>
    [Fact]
    public void RegisterTimeOfDay_LocalSpringGap_SkippedRunBefore_TimeUntilNextMatchesSchedulingHelper ()
    {
        TimeZoneInfo zone = CreateUsEasternStyleZone();
        DateTimeOffset utcNow = new(2025, 3, 9, 6, 0, 0, TimeSpan.Zero);
        IPrimeClock clock = CreatePrimeClockAtUtcWithLocalZone(utcNow, zone);
        TimeOnly target = new(2, 30, 0);
        DayTimeTimerOptions options = CreateLocalDayTimeTimerOptions(SkippedTimeBehavior.RunBefore,
            DuplicateTimeBehavior.RunLast);
        using IClockDayTimeTimer timer = clock.RegisterTimeOfDay(new LocalTimeOfDay(target), () => { },
            TestContext.Current.CancellationToken, options);
        TimeSpan expected = DayTimeBclLocalWallTimeScheduling.GetDelayUntilNextLocalDayTime(clock.LocalNowOffset,
            clock.LocalScheduleTimeZone, target, options.SkippedTimeBehavior, options.DuplicateTimeBehavior);
        timer.TimeUntilNextCallback.Should().Be((long)expected.TotalMilliseconds);
    }

    /// <summary>
    ///   Verifies <see cref="ClockDayTimeTimerRegistration"/> applies <see cref="SkippedTimeBehavior.Skip"/> for local
    ///   day-time scheduling by matching <see cref="IClockDayTimeTimer.TimeUntilNextCallback"/> to the shared delay
    ///   helper.
    /// </summary>
    [Fact]
    public void RegisterTimeOfDay_LocalSpringGap_SkippedSkip_TimeUntilNextMatchesSchedulingHelper ()
    {
        TimeZoneInfo zone = CreateUsEasternStyleZone();
        DateTimeOffset utcNow = new(2025, 3, 9, 6, 0, 0, TimeSpan.Zero);
        IPrimeClock clock = CreatePrimeClockAtUtcWithLocalZone(utcNow, zone);
        TimeOnly target = new(2, 30, 0);
        DayTimeTimerOptions options = CreateLocalDayTimeTimerOptions(SkippedTimeBehavior.Skip,
            DuplicateTimeBehavior.RunLast);
        using IClockDayTimeTimer timer = clock.RegisterTimeOfDay(new LocalTimeOfDay(target), () => { },
            TestContext.Current.CancellationToken, options);
        TimeSpan expected = DayTimeBclLocalWallTimeScheduling.GetDelayUntilNextLocalDayTime(clock.LocalNowOffset,
            clock.LocalScheduleTimeZone, target, options.SkippedTimeBehavior, options.DuplicateTimeBehavior);
        timer.TimeUntilNextCallback.Should().Be((long)expected.TotalMilliseconds);
    }

    /// <summary>
    ///   Verifies <see cref="ClockDayTimeTimerRegistration"/> applies <see cref="DuplicateTimeBehavior.RunFirst"/> for
    ///   an ambiguous fall-back wall time by matching <see cref="IClockDayTimeTimer.TimeUntilNextCallback"/> to the
    ///   shared delay helper.
    /// </summary>
    [Fact]
    public void RegisterTimeOfDay_LocalFallBack_DuplicateRunFirst_TimeUntilNextMatchesSchedulingHelper ()
    {
        TimeZoneInfo zone = CreateUsEasternStyleZone();
        DateTimeOffset utcNow = new(2025, 11, 2, 4, 0, 0, TimeSpan.Zero);
        IPrimeClock clock = CreatePrimeClockAtUtcWithLocalZone(utcNow, zone);
        TimeOnly target = new(1, 30, 0);
        DayTimeTimerOptions options = CreateLocalDayTimeTimerOptions(SkippedTimeBehavior.RunAfter,
            DuplicateTimeBehavior.RunFirst);
        using IClockDayTimeTimer timer = clock.RegisterTimeOfDay(new LocalTimeOfDay(target), () => { },
            TestContext.Current.CancellationToken, options);
        TimeSpan expected = DayTimeBclLocalWallTimeScheduling.GetDelayUntilNextLocalDayTime(clock.LocalNowOffset,
            clock.LocalScheduleTimeZone, target, options.SkippedTimeBehavior, options.DuplicateTimeBehavior);
        timer.TimeUntilNextCallback.Should().Be((long)expected.TotalMilliseconds);
    }

    /// <summary>
    ///   Verifies <see cref="ClockDayTimeTimerRegistration"/> applies <see cref="DuplicateTimeBehavior.RunLast"/> for
    ///   an ambiguous fall-back wall time by matching <see cref="IClockDayTimeTimer.TimeUntilNextCallback"/> to the
    ///   shared delay helper.
    /// </summary>
    [Fact]
    public void RegisterTimeOfDay_LocalFallBack_DuplicateRunLast_TimeUntilNextMatchesSchedulingHelper ()
    {
        TimeZoneInfo zone = CreateUsEasternStyleZone();
        DateTimeOffset utcNow = new(2025, 11, 2, 4, 0, 0, TimeSpan.Zero);
        IPrimeClock clock = CreatePrimeClockAtUtcWithLocalZone(utcNow, zone);
        TimeOnly target = new(1, 30, 0);
        DayTimeTimerOptions options = CreateLocalDayTimeTimerOptions(SkippedTimeBehavior.RunAfter,
            DuplicateTimeBehavior.RunLast);
        using IClockDayTimeTimer timer = clock.RegisterTimeOfDay(new LocalTimeOfDay(target), () => { },
            TestContext.Current.CancellationToken, options);
        TimeSpan expected = DayTimeBclLocalWallTimeScheduling.GetDelayUntilNextLocalDayTime(clock.LocalNowOffset,
            clock.LocalScheduleTimeZone, target, options.SkippedTimeBehavior, options.DuplicateTimeBehavior);
        timer.TimeUntilNextCallback.Should().Be((long)expected.TotalMilliseconds);
    }

    #endregion ClockDayTimeTimerRegistration — local policies vs scheduling helper
}

#endif
