// Copyright (c) Kevin Zehrer
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;

using AwesomeAssertions;
using KZDev.PrimeTime.Tests;

namespace KZDev.SystemClock.PrimeTime.UnitTests;

//################################################################################
/// <summary>
///   Unit tests for <see cref="IPrimeClock"/> and <see cref="PrimeClock"/>.
///   Verifies that all "now" members (including time-only and date-only on .NET) return values
///   consistent with a known time source and with each other.
///   On .NET, includes deterministic tests using FakeTimeProvider (Microsoft.Extensions.Time.Testing).
/// </summary>
[ExcludeFromCodeCoverage]
public partial class UsingIPrimeClock : UnitTestBase
{
    #region Constructors/Finalizers

    //----------------------------------------------------------------------------
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
    //----------------------------------------------------------------------------

    #endregion Constructors/Finalizers

    /// <summary>
    ///   Verifies that <see cref="IPrimeClock"/> extends <see cref="IPrimeTime"/>.
    /// </summary>
    [Fact]
    public void IPrimeClock_ExtendsIPrimeTime ()
    {
        typeof(IPrimeClock).GetInterfaces().Should().Contain(typeof(IPrimeTime));
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies that <see cref="PrimeClock"/> implements <see cref="IPrimeClock"/>.
    /// </summary>
    [Fact]
    public void PrimeClock_ImplementsIPrimeClock ()
    {
        IPrimeClock clock = new PrimeClock();
        clock.Should().NotBeNull();
    }
    //----------------------------------------------------------------------------

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
        clock.UtcNowDateTimeOffset.Should().BeAfter(before).And.BeBefore(after);
        clock.LocalNowDateTimeOffset.Should().BeAfter(TimeProvider.System.GetLocalNow().AddSeconds(-2)).
            And.BeBefore(TimeProvider.System.GetLocalNow().AddSeconds(2));
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies that <see cref="PrimeClock"/> throws when given a null time provider.
    /// </summary>
    [Fact]
    public void PrimeClock_WithNullTimeProvider_ThrowsArgumentNullException ()
    {
        Action act = () => _ = new PrimeClock(null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("timeProvider");
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies that UTC "now" members are consistent: UtcNowDateTimeOffset.UtcDateTime matches UtcNowDateTime,
    ///   and UtcNowTimeOnly/UtcNowDateOnly match the time/date components of UtcNowDateTime.
    /// </summary>
    [Fact]
    public void PrimeClock_UtcNowMembersAreConsistent ()
    {
        IPrimeClock clock = new PrimeClock();
        DateTimeOffset utcNowDateTimeOffset = clock.UtcNowDateTimeOffset;
        DateTime utcNowDateTime = clock.UtcNowDateTime;
#if NET
        TimeOnly utcNowTimeOnly = clock.UtcNowTimeOnly;
        DateOnly utcNowDateOnly = clock.UtcNowDateOnly;
#endif

        utcNowDateTimeOffset.Offset.Should().Be(TimeSpan.Zero);
        utcNowDateTimeOffset.UtcDateTime.Should().BeCloseTo(utcNowDateTime, TimeSpan.FromMilliseconds(50));
        utcNowDateTime.Kind.Should().Be(DateTimeKind.Utc);
#if NET
        long timeTicks = Math.Abs(TimeOnly.FromDateTime(utcNowDateTime).Ticks - utcNowTimeOnly.Ticks);
        timeTicks.Should().BeLessThan(TimeSpan.TicksPerSecond, "time-of-day from UtcNowDateTime and UtcNowTimeOnly should be within 1 second");
        DateOnly.FromDateTime(utcNowDateTime).Should().Be(utcNowDateOnly);
#endif
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies that local "now" members are consistent: LocalNowDateTimeOffset.DateTime matches LocalNowDateTime,
    ///   and LocalNowTimeOnly/LocalNowDateOnly match the time/date components of LocalNowDateTime.
    /// </summary>
    [Fact]
    public void PrimeClock_LocalNowMembersAreConsistent ()
    {
        IPrimeClock clock = new PrimeClock();
        DateTimeOffset localNowDateTimeOffset = clock.LocalNowDateTimeOffset;
        DateTime localNowDateTime = clock.LocalNowDateTime;
#if NET
        TimeOnly localNowTimeOnly = clock.LocalNowTimeOnly;
        DateOnly localNowDateOnly = clock.LocalNowDateOnly;
#endif

        localNowDateTimeOffset.Offset.Should().Be(TimeZoneInfo.Local.GetUtcOffset(localNowDateTimeOffset.DateTime));
        localNowDateTimeOffset.DateTime.Should().BeCloseTo(localNowDateTime, TimeSpan.FromMilliseconds(100));
        localNowDateTime.Kind.Should().Be(DateTimeKind.Local);
#if NET
        long localTimeTicks = Math.Abs(TimeOnly.FromDateTime(localNowDateTime).Ticks - localNowTimeOnly.Ticks);
        localTimeTicks.Should().BeLessThan(TimeSpan.TicksPerSecond, "time-of-day from LocalNowDateTime and LocalNowTimeOnly should be within 1 second");
        DateOnly.FromDateTime(localNowDateTime).Should().Be(localNowDateOnly);
#endif
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies that all "now" values are recent (within the last 5 seconds).
    /// </summary>
    [Fact]
    public void PrimeClock_NowMembersAreRecent ()
    {
        IPrimeClock clock = new PrimeClock();
        DateTimeOffset before = DateTimeOffset.UtcNow.AddSeconds(-1);
        DateTimeOffset after = DateTimeOffset.UtcNow.AddSeconds(1);

        clock.UtcNowDateTimeOffset.Should().BeAfter(before).And.BeBefore(after);
        clock.LocalNowDateTimeOffset.Should().BeAfter(before.ToLocalTime().AddSeconds(-2)).And.BeBefore(after.ToLocalTime().AddSeconds(2));
    }
    //----------------------------------------------------------------------------

}
//################################################################################


