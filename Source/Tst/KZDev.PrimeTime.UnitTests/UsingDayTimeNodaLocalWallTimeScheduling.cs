// Copyright (c) Kevin Zehrer
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

#if NET

using AwesomeAssertions;
using KZDev.PrimeTime.Tests;
using NodaTime;
using NodaTime.Testing;
using NodaTime.TimeZones;

namespace KZDev.PrimeTime.UnitTests;

/// <summary>
///   Unit tests for <see cref="DayTimeNodaLocalWallTimeScheduling"/> and Noda
///   <see cref="ClockDayTimeTimerRegistration"/> local day-time policy alignment with
///   <see cref="DayTimeBclLocalWallTimeScheduling"/> under a shared synthetic zone.
/// </summary>
public class UsingDayTimeNodaLocalWallTimeScheduling : UnitTestBase
{
    /// <summary>
    ///   Initializes a new instance of the <see cref="UsingDayTimeNodaLocalWallTimeScheduling"/> class.
    /// </summary>
    /// <param name="xUnitTestOutputHelper">The xUnit test output helper.</param>
    public UsingDayTimeNodaLocalWallTimeScheduling (ITestOutputHelper xUnitTestOutputHelper)
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
    ///   Converts a Noda <see cref="LocalTime"/> to a BCL <see cref="TimeOnly"/> for the shared BCL scheduling oracle.
    /// </summary>
    /// <param name="localTime">The local time of day.</param>
    /// <returns>The equivalent <see cref="TimeOnly"/>.</returns>
    private static TimeOnly LocalTimeToTimeOnly (LocalTime localTime) =>
        new(localTime.Hour, localTime.Minute, localTime.Second, localTime.Millisecond);

    /// <summary>
    ///   Verifies Noda delay computation matches the BCL helper for a spring-gap target with
    ///   <see cref="SkippedTimeBehavior.Skip"/>.
    /// </summary>
    [Fact]
    public void GetDelayUntilNextLocalDayTime_SpringGap_Skip_MatchesBclOracle ()
    {
        TimeZoneInfo tz = CreateUsEasternStyleZone();
        DateTimeZone zone = BclDateTimeZone.FromTimeZoneInfo(tz);
        Instant now = Instant.FromUtc(2025, 3, 9, 6, 0, 0);
        LocalTime target = new(2, 30, 0);
        TimeSpan bcl = DayTimeBclLocalWallTimeScheduling.GetDelayUntilNextLocalDayTime(
            new DateTimeOffset(now.ToDateTimeUtc(), TimeSpan.Zero), tz, LocalTimeToTimeOnly(target),
            SkippedTimeBehavior.Skip, DuplicateTimeBehavior.RunLast);
        Duration noda = DayTimeNodaLocalWallTimeScheduling.GetDelayUntilNextLocalDayTime(now, zone, target,
            SkippedTimeBehavior.Skip, DuplicateTimeBehavior.RunLast);
        noda.ToTimeSpan().Should().Be(bcl);
    }

    /// <summary>
    ///   Verifies Noda delay computation matches the BCL helper for a spring-gap target with
    ///   <see cref="SkippedTimeBehavior.RunAfter"/>.
    /// </summary>
    [Fact]
    public void GetDelayUntilNextLocalDayTime_SpringGap_RunAfter_MatchesBclOracle ()
    {
        TimeZoneInfo tz = CreateUsEasternStyleZone();
        DateTimeZone zone = BclDateTimeZone.FromTimeZoneInfo(tz);
        Instant now = Instant.FromUtc(2025, 3, 9, 6, 0, 0);
        LocalTime target = new(2, 30, 0);
        TimeSpan bcl = DayTimeBclLocalWallTimeScheduling.GetDelayUntilNextLocalDayTime(
            new DateTimeOffset(now.ToDateTimeUtc(), TimeSpan.Zero), tz, LocalTimeToTimeOnly(target),
            SkippedTimeBehavior.RunAfter, DuplicateTimeBehavior.RunLast);
        Duration noda = DayTimeNodaLocalWallTimeScheduling.GetDelayUntilNextLocalDayTime(now, zone, target,
            SkippedTimeBehavior.RunAfter, DuplicateTimeBehavior.RunLast);
        noda.ToTimeSpan().Should().Be(bcl);
    }

    /// <summary>
    ///   Verifies Noda delay computation matches the BCL helper for a spring-gap target with
    ///   <see cref="SkippedTimeBehavior.RunBefore"/>.
    /// </summary>
    [Fact]
    public void GetDelayUntilNextLocalDayTime_SpringGap_RunBefore_MatchesBclOracle ()
    {
        TimeZoneInfo tz = CreateUsEasternStyleZone();
        DateTimeZone zone = BclDateTimeZone.FromTimeZoneInfo(tz);
        Instant now = Instant.FromUtc(2025, 3, 9, 6, 0, 0);
        LocalTime target = new(2, 30, 0);
        TimeSpan bcl = DayTimeBclLocalWallTimeScheduling.GetDelayUntilNextLocalDayTime(
            new DateTimeOffset(now.ToDateTimeUtc(), TimeSpan.Zero), tz, LocalTimeToTimeOnly(target),
            SkippedTimeBehavior.RunBefore, DuplicateTimeBehavior.RunLast);
        Duration noda = DayTimeNodaLocalWallTimeScheduling.GetDelayUntilNextLocalDayTime(now, zone, target,
            SkippedTimeBehavior.RunBefore, DuplicateTimeBehavior.RunLast);
        noda.ToTimeSpan().Should().Be(bcl);
    }

    /// <summary>
    ///   Verifies ambiguous fall-back instants for <see cref="DuplicateTimeBehavior.RunFirst"/> and
    ///   <see cref="DuplicateTimeBehavior.RunLast"/> match the BCL helper.
    /// </summary>
    [Fact]
    public void TryGetLocalDayTimeFireInstantForDate_FallBack_Ambiguous_MatchesBclOracle ()
    {
        TimeZoneInfo tz = CreateUsEasternStyleZone();
        DateTimeZone zone = BclDateTimeZone.FromTimeZoneInfo(tz);
        LocalDate date = new(2025, 11, 2);
        LocalTime target = new(1, 30, 0);
        TimeOnly targetBcl = LocalTimeToTimeOnly(target);
        DateOnly calendarDate = new(date.Year, date.Month, date.Day);
        DayTimeBclLocalWallTimeScheduling.TryGetLocalDayTimeFireInstantForDate(calendarDate,
            targetBcl, tz, SkippedTimeBehavior.RunAfter, DuplicateTimeBehavior.RunFirst, out DateTimeOffset bclFirst)
            .Should().BeTrue();
        DayTimeBclLocalWallTimeScheduling.TryGetLocalDayTimeFireInstantForDate(calendarDate,
            targetBcl, tz, SkippedTimeBehavior.RunAfter, DuplicateTimeBehavior.RunLast, out DateTimeOffset bclLast)
            .Should().BeTrue();
        DayTimeNodaLocalWallTimeScheduling.TryGetLocalDayTimeFireInstantForDate(date, target, zone,
            SkippedTimeBehavior.RunAfter, DuplicateTimeBehavior.RunFirst, out Instant nodaFirst).Should().BeTrue();
        DayTimeNodaLocalWallTimeScheduling.TryGetLocalDayTimeFireInstantForDate(date, target, zone,
            SkippedTimeBehavior.RunAfter, DuplicateTimeBehavior.RunLast, out Instant nodaLast).Should().BeTrue();
        nodaFirst.Should().Be(Instant.FromDateTimeOffset(bclFirst));
        nodaLast.Should().Be(Instant.FromDateTimeOffset(bclLast));
    }

    /// <summary>
    ///   Creates <see cref="DayTimeTimerOptions"/> with <see cref="ConcurrentTriggerProcessing.RunConcurrently"/>
    ///   and the given skipped/duplicate policies.
    /// </summary>
    /// <param name="skippedTimeBehavior">Skipped spring-gap policy.</param>
    /// <param name="duplicateTimeBehavior">Ambiguous local-time policy.</param>
    /// <returns>Options for Noda <see cref="IPrimeClock"/> local <c>RegisterTimeOfDay</c> overloads.</returns>
    private static DayTimeTimerOptions CreateLocalDayTimeTimerOptions (SkippedTimeBehavior skippedTimeBehavior,
        DuplicateTimeBehavior duplicateTimeBehavior) =>
        new()
        {
            ConcurrentTriggerProcessing = ConcurrentTriggerProcessing.RunConcurrently,
            SkippedTimeBehavior = skippedTimeBehavior,
            DuplicateTimeBehavior = duplicateTimeBehavior,
        };

    /// <summary>
    ///   Verifies <see cref="ClockDayTimeTimerRegistration"/> applies <see cref="SkippedTimeBehavior.RunAfter"/> for
    ///   local day-time scheduling by matching <see cref="IClockDayTimeTimer.TimeUntilNextCallback"/> to the BCL
    ///   scheduling oracle (same zone via <see cref="BclDateTimeZone"/>).
    /// </summary>
    [Fact]
    public void RegisterTimeOfDay_LocalSpringGap_SkippedRunAfter_TimeUntilNextMatchesBclOracle ()
    {
        TimeZoneInfo tz = CreateUsEasternStyleZone();
        DateTimeZone zone = BclDateTimeZone.FromTimeZoneInfo(tz);
        Instant utcNow = Instant.FromUtc(2025, 3, 9, 6, 0, 0);
        IPrimeClock clock = new PrimeClock(new FakeClock(utcNow), zone);
        LocalTime target = new(2, 30, 0);
        DayTimeTimerOptions options = CreateLocalDayTimeTimerOptions(SkippedTimeBehavior.RunAfter,
            DuplicateTimeBehavior.RunLast);
        using IClockDayTimeTimer timer = clock.RegisterTimeOfDay(target, () => { },
            TestContext.Current.CancellationToken, options);
        TimeSpan expected = DayTimeBclLocalWallTimeScheduling.GetDelayUntilNextLocalDayTime(clock.LocalNowOffset, tz,
            LocalTimeToTimeOnly(target), options.SkippedTimeBehavior, options.DuplicateTimeBehavior);
        timer.TimeUntilNextCallback.Should().Be((long)expected.TotalMilliseconds);
    }

    /// <summary>
    ///   Verifies <see cref="ClockDayTimeTimerRegistration"/> applies <see cref="SkippedTimeBehavior.RunBefore"/> for
    ///   local day-time scheduling by matching <see cref="IClockDayTimeTimer.TimeUntilNextCallback"/> to the BCL
    ///   oracle.
    /// </summary>
    [Fact]
    public void RegisterTimeOfDay_LocalSpringGap_SkippedRunBefore_TimeUntilNextMatchesBclOracle ()
    {
        TimeZoneInfo tz = CreateUsEasternStyleZone();
        DateTimeZone zone = BclDateTimeZone.FromTimeZoneInfo(tz);
        Instant utcNow = Instant.FromUtc(2025, 3, 9, 6, 0, 0);
        IPrimeClock clock = new PrimeClock(new FakeClock(utcNow), zone);
        LocalTime target = new(2, 30, 0);
        DayTimeTimerOptions options = CreateLocalDayTimeTimerOptions(SkippedTimeBehavior.RunBefore,
            DuplicateTimeBehavior.RunLast);
        using IClockDayTimeTimer timer = clock.RegisterTimeOfDay(target, () => { },
            TestContext.Current.CancellationToken, options);
        TimeSpan expected = DayTimeBclLocalWallTimeScheduling.GetDelayUntilNextLocalDayTime(clock.LocalNowOffset, tz,
            LocalTimeToTimeOnly(target), options.SkippedTimeBehavior, options.DuplicateTimeBehavior);
        timer.TimeUntilNextCallback.Should().Be((long)expected.TotalMilliseconds);
    }

    /// <summary>
    ///   Verifies <see cref="ClockDayTimeTimerRegistration"/> applies <see cref="SkippedTimeBehavior.Skip"/> for local
    ///   day-time scheduling by matching <see cref="IClockDayTimeTimer.TimeUntilNextCallback"/> to the BCL oracle.
    /// </summary>
    [Fact]
    public void RegisterTimeOfDay_LocalSpringGap_SkippedSkip_TimeUntilNextMatchesBclOracle ()
    {
        TimeZoneInfo tz = CreateUsEasternStyleZone();
        DateTimeZone zone = BclDateTimeZone.FromTimeZoneInfo(tz);
        Instant utcNow = Instant.FromUtc(2025, 3, 9, 6, 0, 0);
        IPrimeClock clock = new PrimeClock(new FakeClock(utcNow), zone);
        LocalTime target = new(2, 30, 0);
        DayTimeTimerOptions options = CreateLocalDayTimeTimerOptions(SkippedTimeBehavior.Skip,
            DuplicateTimeBehavior.RunLast);
        using IClockDayTimeTimer timer = clock.RegisterTimeOfDay(target, () => { },
            TestContext.Current.CancellationToken, options);
        TimeSpan expected = DayTimeBclLocalWallTimeScheduling.GetDelayUntilNextLocalDayTime(clock.LocalNowOffset, tz,
            LocalTimeToTimeOnly(target), options.SkippedTimeBehavior, options.DuplicateTimeBehavior);
        timer.TimeUntilNextCallback.Should().Be((long)expected.TotalMilliseconds);
    }

    /// <summary>
    ///   Verifies <see cref="DuplicateTimeBehavior.RunFirst"/> for an ambiguous fall-back wall time by matching
    ///   <see cref="IClockDayTimeTimer.TimeUntilNextCallback"/> to the BCL oracle.
    /// </summary>
    [Fact]
    public void RegisterTimeOfDay_LocalFallBack_DuplicateRunFirst_TimeUntilNextMatchesBclOracle ()
    {
        TimeZoneInfo tz = CreateUsEasternStyleZone();
        DateTimeZone zone = BclDateTimeZone.FromTimeZoneInfo(tz);
        Instant utcNow = Instant.FromUtc(2025, 11, 2, 4, 0, 0);
        IPrimeClock clock = new PrimeClock(new FakeClock(utcNow), zone);
        LocalTime target = new(1, 30, 0);
        DayTimeTimerOptions options = CreateLocalDayTimeTimerOptions(SkippedTimeBehavior.RunAfter,
            DuplicateTimeBehavior.RunFirst);
        using IClockDayTimeTimer timer = clock.RegisterTimeOfDay(target, () => { },
            TestContext.Current.CancellationToken, options);
        TimeSpan expected = DayTimeBclLocalWallTimeScheduling.GetDelayUntilNextLocalDayTime(clock.LocalNowOffset, tz,
            LocalTimeToTimeOnly(target), options.SkippedTimeBehavior, options.DuplicateTimeBehavior);
        timer.TimeUntilNextCallback.Should().Be((long)expected.TotalMilliseconds);
    }

    /// <summary>
    ///   Verifies <see cref="DuplicateTimeBehavior.RunLast"/> for an ambiguous fall-back wall time by matching
    ///   <see cref="IClockDayTimeTimer.TimeUntilNextCallback"/> to the BCL oracle.
    /// </summary>
    [Fact]
    public void RegisterTimeOfDay_LocalFallBack_DuplicateRunLast_TimeUntilNextMatchesBclOracle ()
    {
        TimeZoneInfo tz = CreateUsEasternStyleZone();
        DateTimeZone zone = BclDateTimeZone.FromTimeZoneInfo(tz);
        Instant utcNow = Instant.FromUtc(2025, 11, 2, 4, 0, 0);
        IPrimeClock clock = new PrimeClock(new FakeClock(utcNow), zone);
        LocalTime target = new(1, 30, 0);
        DayTimeTimerOptions options = CreateLocalDayTimeTimerOptions(SkippedTimeBehavior.RunAfter,
            DuplicateTimeBehavior.RunLast);
        using IClockDayTimeTimer timer = clock.RegisterTimeOfDay(target, () => { },
            TestContext.Current.CancellationToken, options);
        TimeSpan expected = DayTimeBclLocalWallTimeScheduling.GetDelayUntilNextLocalDayTime(clock.LocalNowOffset, tz,
            LocalTimeToTimeOnly(target), options.SkippedTimeBehavior, options.DuplicateTimeBehavior);
        timer.TimeUntilNextCallback.Should().Be((long)expected.TotalMilliseconds);
    }
}

#endif
