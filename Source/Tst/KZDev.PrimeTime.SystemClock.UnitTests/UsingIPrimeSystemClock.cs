// Copyright (c) Kevin Zehrer
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using AwesomeAssertions;
using KZDev.PrimeTime.Tests;

namespace KZDev.PrimeTime.SystemClock.UnitTests;

/// <summary>
///   Unit tests for <see cref="IPrimeSystemClock"/> and <see cref="PrimeSystemClock"/> (Phases 2 and 4).
///   Verifies that all "now" members (including time-only and date-only on .NET) return values
///   consistent with a known time source and with each other.
/// </summary>
public class UsingIPrimeSystemClock : UnitTestBase
{
    /// <summary>
    ///   Initializes a new instance of the <see cref="UsingIPrimeSystemClock"/> class.
    /// </summary>
    /// <param name="xUnitTestOutputHelper">
    ///   The xUnit test output helper that can be used to output test messages.

    /// </param>
    public UsingIPrimeSystemClock (ITestOutputHelper xUnitTestOutputHelper)
        : base(xUnitTestOutputHelper)
    {
    }

    /// <summary>
    ///   Verifies that <see cref="IPrimeSystemClock"/> extends <see cref="IPrimeTime"/>.
    /// </summary>
    [Fact]
    public void IPrimeSystemClock_ExtendsIPrimeTime ()
    {
        typeof(IPrimeSystemClock).GetInterfaces().Should().Contain(typeof(IPrimeTime));
    }

    /// <summary>
    ///   Verifies that <see cref="PrimeSystemClock"/> implements <see cref="IPrimeSystemClock"/>.
    /// </summary>
    [Fact]
    public void PrimeSystemClock_ImplementsIPrimeSystemClock ()
    {
        IPrimeSystemClock clock = new PrimeSystemClock();
        clock.Should().NotBeNull();
    }

    /// <summary>
    ///   Verifies that UTC "now" members are consistent: UtcNow.UtcDateTime matches UtcDateTimeNow,
    ///   and UtcNowTime/UtcNowDate match the time/date components of UtcDateTimeNow.
    /// </summary>
    [Fact]
    public void PrimeSystemClock_UtcNowMembersAreConsistent ()
    {
        IPrimeSystemClock clock = new PrimeSystemClock();
        DateTimeOffset utcNow = clock.UtcNow;
        DateTime utcDateTimeNow = clock.UtcDateTimeNow;
#if NET
        TimeOnly utcNowTime = clock.UtcNowTime;
        DateOnly utcNowDate = clock.UtcNowDate;
#endif

        utcNow.Offset.Should().Be(TimeSpan.Zero);
        utcNow.UtcDateTime.Should().BeCloseTo(utcDateTimeNow, TimeSpan.FromMilliseconds(50));
        utcDateTimeNow.Kind.Should().Be(DateTimeKind.Utc);
#if NET
        long timeTicks = Math.Abs(TimeOnly.FromDateTime(utcDateTimeNow).Ticks - utcNowTime.Ticks);
        timeTicks.Should().BeLessThan(TimeSpan.TicksPerSecond, "time-of-day from UtcDateTimeNow and UtcNowTime should be within 1 second");
        DateOnly.FromDateTime(utcDateTimeNow).Should().Be(utcNowDate);
#endif
    }

    /// <summary>
    ///   Verifies that local "now" members are consistent: LocalNow.DateTime matches LocalDateTimeNow,
    ///   and LocalNowTime/LocalNowDate match the time/date components of LocalDateTimeNow.
    /// </summary>
    [Fact]
    public void PrimeSystemClock_LocalNowMembersAreConsistent ()
    {
        IPrimeSystemClock clock = new PrimeSystemClock();
        DateTimeOffset localNow = clock.LocalNow;
        DateTime localDateTimeNow = clock.LocalDateTimeNow;
#if NET
        TimeOnly localNowTime = clock.LocalNowTime;
        DateOnly localNowDate = clock.LocalNowDate;
#endif

        localNow.Offset.Should().Be(TimeZoneInfo.Local.GetUtcOffset(localNow.DateTime));
        localNow.DateTime.Should().BeCloseTo(localDateTimeNow, TimeSpan.FromMilliseconds(50));
        localDateTimeNow.Kind.Should().Be(DateTimeKind.Local);
#if NET
        long localTimeTicks = Math.Abs(TimeOnly.FromDateTime(localDateTimeNow).Ticks - localNowTime.Ticks);
        localTimeTicks.Should().BeLessThan(TimeSpan.TicksPerSecond, "time-of-day from LocalDateTimeNow and LocalNowTime should be within 1 second");
        DateOnly.FromDateTime(localDateTimeNow).Should().Be(localNowDate);
#endif
    }

    /// <summary>
    ///   Verifies that all "now" values are recent (within the last 5 seconds).
    /// </summary>
    [Fact]
    public void PrimeSystemClock_NowMembersAreRecent ()
    {
        IPrimeSystemClock clock = new PrimeSystemClock();
        DateTimeOffset before = DateTimeOffset.UtcNow.AddSeconds(-1);
        DateTimeOffset after = DateTimeOffset.UtcNow.AddSeconds(1);

        clock.UtcNow.Should().BeAfter(before).And.BeBefore(after);
        clock.LocalNow.Should().BeAfter(before.ToLocalTime().AddSeconds(-2)).And.BeBefore(after.ToLocalTime().AddSeconds(2));
    }

    /// <summary>
    ///   Verifies that <see cref="IPrimeTime.Sleep(TimeSpan)"/> throws
    ///   <see cref="NotSupportedException"/> on <see cref="PrimeSystemClock"/> in this phase.
    /// </summary>
    [Fact]
    public void PrimeSystemClock_Sleep_ThrowsNotSupportedException ()
    {
        IPrimeSystemClock clock = new PrimeSystemClock();
        Action act = () => clock.Sleep(TimeSpan.Zero);
        act.Should().Throw<NotSupportedException>();
    }

    /// <summary>
    ///   Verifies that <see cref="IPrimeTime.DelayAsync(TimeSpan)"/> throws
    ///   <see cref="NotSupportedException"/> on <see cref="PrimeSystemClock"/> in this phase.
    /// </summary>
    [Fact]
    public void PrimeSystemClock_DelayAsync_ThrowsNotSupportedException ()
    {
        IPrimeSystemClock clock = new PrimeSystemClock();
        Func<Task> act = async () => await clock.DelayAsync(TimeSpan.Zero);
        act.Should().ThrowAsync<NotSupportedException>();
    }
}
