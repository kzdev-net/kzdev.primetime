// Copyright (c) Kevin Zehrer
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using AwesomeAssertions;
using KZDev.PrimeTime.Tests;

namespace KZDev.SystemClock.PrimeTime.UnitTests;

/// <summary>
///   Unit tests for <see cref="IPrimeClock"/> and <see cref="PrimeClock"/>.
///   Verifies that all "now" members (including time-only and date-only on .NET) return values
///   consistent with a known time source and with each other.
///   On .NET, includes deterministic tests using FakeTimeProvider (Microsoft.Extensions.Time.Testing).
/// </summary>
public partial class UsingIPrimeClock : UnitTestBase
{
    #region Constructors/Finalizers

    /// <summary>
    ///   Initializes a new instance of the <see cref="UsingIPrimeClock"/> class.
    /// </summary>
    /// <param name="xUnitTestOutputHelper">
    ///   The xUnit test output helper that can be used to output test messages.
    /// </param>
    public UsingIPrimeClock (ITestOutputHelper xUnitTestOutputHelper)
        : base(xUnitTestOutputHelper)
    {
    }

    #endregion Constructors/Finalizers

    /// <summary>
    ///   Verifies that <see cref="IPrimeClock"/> extends <see cref="IPrimeTime"/>.
    /// </summary>
    [Fact]
    public void IPrimeClock_ExtendsIPrimeTime ()
    {
        typeof(IPrimeClock).GetInterfaces().Should().Contain(typeof(IPrimeTime));
    }

    /// <summary>
    ///   Verifies that <see cref="PrimeClock"/> implements <see cref="IPrimeClock"/>.
    /// </summary>
    [Fact]
    public void PrimeClock_ImplementsIPrimeClock ()
    {
        IPrimeClock clock = new PrimeClock();
        clock.Should().NotBeNull();
    }

    /// <summary>
    ///   Verifies that <see cref="PrimeClock"/> constructed with an explicit
    ///   <see cref="TimeProvider"/> uses that provider for "now" values.
    /// </summary>
    [Fact]
    public void PrimeClock_WithTimeProvider_UsesProviderForNow ()
    {
        IPrimeClock clock = new PrimeClock(TimeProvider.System);
        DateTimeOffset before = TimeProvider.System.GetUtcNow().AddSeconds(-1);
        DateTimeOffset after = TimeProvider.System.GetUtcNow().AddSeconds(1);
        clock.UtcNow.Should().BeAfter(before).And.BeBefore(after);
        clock.LocalNow.Should().BeAfter(TimeProvider.System.GetLocalNow().AddSeconds(-2)).
            And.BeBefore(TimeProvider.System.GetLocalNow().AddSeconds(2));
    }

    /// <summary>
    ///   Verifies that <see cref="PrimeClock"/> throws when given a null time provider.
    /// </summary>
    [Fact]
    public void PrimeClock_WithNullTimeProvider_ThrowsArgumentNullException ()
    {
        Action act = () => _ = new PrimeClock(null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("timeProvider");
    }

    /// <summary>
    ///   Verifies that UTC "now" members are consistent: UtcNow.UtcDateTime matches UtcDateTimeNow,
    ///   and UtcNowTime/UtcNowDate match the time/date components of UtcDateTimeNow.
    /// </summary>
    [Fact]
    public void PrimeClock_UtcNowMembersAreConsistent ()
    {
        IPrimeClock clock = new PrimeClock();
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
    public void PrimeClock_LocalNowMembersAreConsistent ()
    {
        IPrimeClock clock = new PrimeClock();
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
    public void PrimeClock_NowMembersAreRecent ()
    {
        IPrimeClock clock = new PrimeClock();
        DateTimeOffset before = DateTimeOffset.UtcNow.AddSeconds(-1);
        DateTimeOffset after = DateTimeOffset.UtcNow.AddSeconds(1);

        clock.UtcNow.Should().BeAfter(before).And.BeBefore(after);
        clock.LocalNow.Should().BeAfter(before.ToLocalTime().AddSeconds(-2)).And.BeBefore(after.ToLocalTime().AddSeconds(2));
    }

}
