// Copyright (c) Kevin Zehrer
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;

using AwesomeAssertions;

using KZDev.PrimeTime.Tests;

using NodaTime;
// ReSharper disable AccessToDisposedClosure

namespace KZDev.PrimeTime.Testing.UnitTests;

//################################################################################
/// <summary>
///   Unit tests for <see cref="IPrimeTestClock"/> and <see cref="PrimeTestClock"/>.
/// </summary>
/// <remarks>
///   Covers set-instant/time/local-time APIs, advance and run-for, start/stop, clock events, and virtual-time-driven
///   sleep, delay, time cancellation, and timers.
/// </remarks>
[ExcludeFromCodeCoverage]
public class UsingIPrimeTestClock : UnitTestBase
{
    //----------------------------------------------------------------------------
    /// <summary>
    ///   Wall-clock guard for loops that wait for a sleep completion flag while advancing virtual time on another path.
    /// </summary>
    private static readonly Duration SleepTestRealTimeTimeout = Duration.FromSeconds(5);

    /// <summary>
    ///   Minimum virtual time per real second allowed by <see cref="PrimeTestClock.Start(NodaTime.Duration?)"/>.
    /// </summary>
    private static readonly Duration MinimumAllowedStartRunRate = Duration.FromMilliseconds(100);

    /// <summary>
    ///   Run rate one millisecond below <see cref="MinimumAllowedStartRunRate"/> for out-of-range start tests.
    /// </summary>
    private static readonly Duration StartRunRateBelowMinimum = MinimumAllowedStartRunRate - Duration.FromMilliseconds(1);

    /// <summary>
    ///   Brief real wall delay while the automatic runner is active: long enough for measurable virtual
    ///   advancement at 10 virtual seconds per real second without slowing the suite.
    /// </summary>
    private static readonly TimeSpan ClockRunningProjectionTestWallDelay = TimeSpan.FromMilliseconds(250);

    /// <summary>
    ///   Virtual-time assertion margin for runner projection tests: the run anchor starts inside
    ///   <see cref="PrimeTestClock.Start(NodaTime.Duration?)"/> before the wall <see cref="Stopwatch"/> begins,
    ///   separate stopwatch instances, imprecise <see cref="Task.Delay(TimeSpan, CancellationToken)"/>, and
    ///   persist-on-read work while the runner is active under parallel scheduling.
    /// </summary>
    private static readonly Duration ClockRunningProjectionVirtualTolerance = Duration.FromMilliseconds(150);

    /// <summary>
    ///   Real wall delay before persist-on-read in delay-due tests: slightly exceeds the ~500 ms real time
    ///   needed for a 5 s virtual delay at 10 virtual seconds per real second, plus runner scheduling slack.
    /// </summary>
    private static readonly TimeSpan ClockRunningDelayDueTestWallDelay = TimeSpan.FromMilliseconds(600);

    /// <summary>
    ///   Short real wall delay used when the runner is stopped to confirm virtual time does not drift with wall time.
    /// </summary>
    private static readonly TimeSpan ClockStoppedNoAdvanceTestWallDelay = TimeSpan.FromMilliseconds(50);

    /// <summary>
    ///   Real wall delay for virtual-minute heartbeat at 10 virtual seconds per real second (one virtual minute is about
    ///   six seconds of real time, plus scheduling slack).
    /// </summary>
    private static readonly TimeSpan ClockRunningHeartbeatTestWallDelay = TimeSpan.FromSeconds(8);

    /// <summary>
    ///   Virtual-time tolerance when asserting heartbeat <see cref="IPrimeTestClock.ClockEvents"/> instants.
    /// </summary>
    private static readonly Duration ClockRunningHeartbeatVirtualTolerance = Duration.FromSeconds(2);

    /// <summary>
    ///   Maximum real wall time for the 15 ms burst-path test: three sub-15 ms virtual delays at 10 virtual seconds
    ///   per real second should finish well under this budget on CI.
    /// </summary>
    private static readonly TimeSpan ClockRunningBurstPathMaxWallDelay = TimeSpan.FromMilliseconds(500);

    /// <summary>
    ///   Real wall ceiling for slow-rate delay completion: below the old fixed one-second poll interval.
    /// </summary>
    private static readonly TimeSpan ClockRunningSlowRateDelayMaxWallDelay = TimeSpan.FromMilliseconds(850);

    /// <summary>
    ///   Minimum real wall time expected for a 50 ms virtual delay at the minimum run rate (100 ms virtual per real
    ///   second).
    /// </summary>
    private static readonly TimeSpan ClockRunningSlowRateDelayMinWallDelay = TimeSpan.FromMilliseconds(200);

    /// <summary>
    ///   Real wall ceiling for multiple-delay catch-up completion at 10 virtual seconds per real second.
    /// </summary>
    private static readonly TimeSpan ClockRunningCatchUpTestWallDelay = TimeSpan.FromSeconds(1);

    /// <summary>
    ///   Minimum real wall time before catch-up completes: first due delay is 300 ms virtual (30 ms real at 10:1),
    ///   which exceeds the 15 ms minimum sleep threshold.
    /// </summary>
    private static readonly TimeSpan ClockRunningCatchUpMinWallDelay = TimeSpan.FromMilliseconds(20);

    /// <summary>
    ///   Real wall delay for heartbeat cadence reset test at 10 virtual seconds per real second.
    /// </summary>
    private static readonly TimeSpan ClockRunningHeartbeatResetTestWallDelay = TimeSpan.FromSeconds(12);

    /// <summary>
    ///   Tolerance when asserting a reset heartbeat <see cref="IPrimeTestClock.ClockEvents"/> instant after a
    ///   substantive timer at 45 virtual seconds.
    /// </summary>
    private static readonly Duration ClockRunningResetHeartbeatVirtualTolerance = Duration.FromSeconds(5);
    //----------------------------------------------------------------------------

    #region Constructors/Finalizers

    //----------------------------------------------------------------------------
    /// <summary>
    ///   Initializes a new instance of the <see cref="UsingIPrimeTestClock"/> class.
    /// </summary>
    /// <param name="xUnitTestOutputHelper">
    ///   The xUnit test output helper that can be used to output test messages.
    /// </param>
    public UsingIPrimeTestClock (ITestOutputHelper xUnitTestOutputHelper)
        : base(xUnitTestOutputHelper)
    {
    }
    //----------------------------------------------------------------------------

    #endregion Constructors/Finalizers

    #region Contract and construction

    /// <summary>
    ///   Verifies that <see cref="IPrimeTestClock"/> extends <see cref="IPrimeTestTime"/> and
    ///   <see cref="IPrimeClock"/>.
    /// </summary>
    [Fact]
    public void IPrimeTestClock_ExtendsIPrimeTestTimeAndIPrimeClock ()
    {
        typeof(IPrimeTestClock).GetInterfaces().Should().Contain(typeof(IPrimeTestTime));
        typeof(IPrimeTestClock).GetInterfaces().Should().Contain(typeof(IPrimeClock));
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies that <see cref="PrimeTestClock"/> implements <see cref="IPrimeTestClock"/>.
    /// </summary>
    [Fact]
    public void PrimeTestClock_ImplementsIPrimeTestClock ()
    {
        IPrimeTestClock clock = new PrimeTestClock();
        clock.Should().NotBeNull();
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies that <see cref="PrimeTestClock"/> with initial instant returns that instant from Instant.
    /// </summary>
    [Fact]
    public void PrimeTestClock_WithInitialInstant_ReturnsThatInstantFromInstant ()
    {
        Instant initial = Instant.FromUtc(2020, 6, 15, 12, 0, 0);
        IPrimeTestClock clock = new PrimeTestClock(initial);
        clock.NowInstant.Should().Be(initial);
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies that <see cref="PrimeTestClock"/> with initial instant and zone returns correct UtcNowInstant and LocalZonedNowInstant.
    /// </summary>
    [Fact]
    public void PrimeTestClock_WithInitialInstantAndZone_ReturnsCorrectZonedNow ()
    {
        Instant initial = Instant.FromUtc(2020, 6, 15, 12, 0, 0);
        DateTimeZone utc = DateTimeZone.Utc;
        IPrimeTestClock clock = new PrimeTestClock(initial, utc);
        clock.NowInstant.Should().Be(initial);
        clock.UtcNowInstant.ToInstant().Should().Be(initial);
        clock.LocalZonedNowInstant.Zone.Should().Be(utc);
    }
    //----------------------------------------------------------------------------

    #endregion Contract and construction

    #region SetInstant, SetTime, SetLocalTime and Advance

    /// <summary>
    ///   Verifies that <see cref="IPrimeTestClock.SetInstant"/> updates Instant and related "now" members.
    /// </summary>
    [Fact]
    public void SetInstant_WithInstant_UpdatesInstantAndRelatedMembers ()
    {
        Instant setInstant = Instant.FromUtc(2025, 1, 10, 14, 30, 0);
        IPrimeTestClock clock = new PrimeTestClock();
        clock.SetInstant(setInstant);
        clock.NowInstant.Should().Be(setInstant);
        clock.UtcNowInstant.ToInstant().Should().Be(setInstant);
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies that <see cref="IPrimeTestClock.SetTime"/> (DateTimeOffset) updates Instant.
    /// </summary>
    [Fact]
    public void SetTime_WithDateTimeOffset_UpdatesInstant ()
    {
        DateTimeOffset utcTime = new(2025, 2, 20, 10, 0, 0, TimeSpan.Zero);
        IPrimeTestClock clock = new PrimeTestClock();
        clock.SetTime(utcTime);
        clock.NowInstant.Should().Be(Instant.FromDateTimeUtc(utcTime.UtcDateTime));
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies that <see cref="IPrimeTestClock.SetLocalTime"/> sets the instant from local date/time in the clock's zone.
    /// </summary>
    [Fact]
    public void SetLocalTime_WithLocalDateTime_UpdatesInstantInZone ()
    {
        IPrimeTestClock clock = new PrimeTestClock(Instant.FromUtc(2020, 1, 1, 0, 0, 0), DateTimeZone.Utc);
        LocalDateTime local = new LocalDate(2025, 3, 15).At(new LocalTime(9, 30));
        clock.SetLocalTime(local);
        clock.LocalNowInstant.Should().Be(local);
        clock.UtcNowInstant.LocalDateTime.Should().Be(local);
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies that <see cref="IPrimeTestClock.SetLocalTime"/> uses lenient zone resolution for ambiguous local
    ///   wall-clock values.
    /// </summary>
    [Fact]
    public void SetLocalTime_WithAmbiguousLocalDateTime_UsesLenientZoneResolution ()
    {
        DateTimeZone zone = DateTimeZoneProviders.Tzdb["America/New_York"];
        IPrimeTestClock clock = new PrimeTestClock(Instant.FromUtc(2025, 1, 1, 0, 0, 0), zone);
        LocalDateTime ambiguous = new(2025, 11, 2, 1, 30, 0);

        clock.SetLocalTime(ambiguous);

        clock.NowInstant.Should().Be(ambiguous.InZoneLeniently(zone).ToInstant());
        clock.LocalNowInstant.Should().Be(ambiguous);
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies that <see cref="IPrimeTestClock.Advance(Duration)"/> adds duration to virtual time.
    /// </summary>
    [Fact]
    public void Advance_WithPositiveDuration_AddsToVirtualTime ()
    {
        Instant initial = Instant.FromUtc(2025, 1, 1, 0, 0, 0);
        IPrimeTestClock clock = new PrimeTestClock(initial);
        clock.Advance(Duration.FromHours(2));
        clock.NowInstant.Should().Be(initial + Duration.FromHours(2));
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies that <see cref="IPrimeTestClock.Advance(Duration)"/> with zero does not change time.
    /// </summary>
    [Fact]
    public void Advance_WithZero_LeavesTimeUnchanged ()
    {
        Instant initial = Instant.FromUtc(2025, 1, 1, 0, 0, 0);
        IPrimeTestClock clock = new PrimeTestClock(initial);
        clock.Advance(Duration.Zero);
        clock.NowInstant.Should().Be(initial);
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies that <see cref="IPrimeTestClock.Advance(Duration)"/> with negative duration does not move
    ///   time backward (implementation treats it as zero).
    /// </summary>
    [Fact]
    public void Advance_WithNegativeDuration_LeavesTimeUnchanged ()
    {
        Instant initial = Instant.FromUtc(2025, 1, 1, 0, 0, 0);
        IPrimeTestClock clock = new PrimeTestClock(initial);
        clock.Advance(Duration.FromHours(-1));
        clock.NowInstant.Should().Be(initial);
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies that <see cref="IPrimeTestClock.Advance(Duration)"/> with <see cref="Duration.MaxValue"/> throws
    ///   when the resulting virtual UTC target is not representable as <see cref="DateTimeOffset"/>.
    /// </summary>
    [Fact]
    public void Advance_WithDurationMaxValue_ThrowsWhenVirtualTargetNotRepresentable ()
    {
        Instant initial = Instant.FromUtc(2025, 1, 1, 0, 0, 0);
        IPrimeTestClock clock = new PrimeTestClock(initial);

        Action act = () => clock.Advance(Duration.MaxValue);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }
    //----------------------------------------------------------------------------

    #region Advance — forward virtual march

    /// <summary>
    ///   Verifies that a single <see cref="IPrimeTestClock.Advance(Duration)"/> that spans multiple interval ticks
    ///   invokes each callback with <see cref="IPrimeTestClock.NowInstant"/> at that tick&apos;s firing instant.
    /// </summary>
    [Fact]
    public void Advance_OverMultipleIntervalTicks_CallbackSeesNowInstantAtEachFiringInstant ()
    {
        Instant initial = Instant.FromUtc(2025, 1, 1, 0, 0, 0);
        IPrimeTestClock clock = new PrimeTestClock(initial);
        List<Instant> observedNow = [];
        using (clock.RegisterTimer(Duration.FromSeconds(10),
                   _ => observedNow.Add(clock.NowInstant),
                   TestContext.Current.CancellationToken,
                   repeat: true))
        {
            clock.Advance(Duration.FromSeconds(30));
        }

        observedNow.Should().HaveCount(3);
        observedNow[0].Should().Be(initial + Duration.FromSeconds(10));
        observedNow[1].Should().Be(initial + Duration.FromSeconds(20));
        observedNow[2].Should().Be(initial + Duration.FromSeconds(30));
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies that <see cref="IPrimeTestClock.ClockEvents"/> is raised once per distinct virtual instant crossed
    ///   during one <see cref="IPrimeTestClock.Advance(Duration)"/> when multiple interval ticks occur.
    /// </summary>
    [Fact]
    public void Advance_SpanningMultipleDueInstants_RaisesClockEventsOncePerDistinctInstant ()
    {
        Instant initial = Instant.FromUtc(2025, 1, 1, 0, 0, 0);
        IPrimeTestClock clock = new PrimeTestClock(initial);
        int eventCount = 0;
        clock.ClockEvents += (_, _) => eventCount++;
        using (clock.RegisterTimer(Duration.FromSeconds(10),
                   _ => { },
                   TestContext.Current.CancellationToken,
                   repeat: true))
        {
            clock.Advance(Duration.FromSeconds(30));
        }

        eventCount.Should().Be(3);
    }
    //----------------------------------------------------------------------------

    #endregion Advance — forward virtual march

    #region SetTime, SetInstant, SetLocalTime — forward virtual march

    /// <summary>
    ///   Verifies that a single forward <see cref="IPrimeTestClock.SetTime"/> that lands after multiple interval
    ///   ticks invokes each callback with <see cref="IPrimeTestClock.NowInstant"/> at that tick&apos;s firing instant.
    /// </summary>
    [Fact]
    public void SetTime_Forward_OverMultipleIntervalTicks_CallbackSeesNowInstantAtEachFiringInstant ()
    {
        Instant initial = Instant.FromUtc(2025, 1, 1, 0, 0, 0);
        Instant targetInstant = initial + Duration.FromSeconds(30);
        DateTimeOffset targetUtc = new DateTimeOffset(targetInstant.ToDateTimeUtc(), TimeSpan.Zero);
        IPrimeTestClock clock = new PrimeTestClock(initial);
        List<Instant> observedNow = [];
        using (clock.RegisterTimer(Duration.FromSeconds(10),
                   _ => observedNow.Add(clock.NowInstant),
                   TestContext.Current.CancellationToken,
                   repeat: true))
        {
            clock.SetTime(targetUtc);
        }

        observedNow.Should().HaveCount(3);
        observedNow[0].Should().Be(initial + Duration.FromSeconds(10));
        observedNow[1].Should().Be(initial + Duration.FromSeconds(20));
        observedNow[2].Should().Be(initial + Duration.FromSeconds(30));
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies that <see cref="IPrimeTestClock.ClockEvents"/> is raised once per distinct virtual instant crossed
    ///   during one forward <see cref="IPrimeTestClock.SetTime"/> when multiple interval ticks occur.
    /// </summary>
    [Fact]
    public void SetTime_Forward_SpanningMultipleDueInstants_RaisesClockEventsOncePerDistinctInstant ()
    {
        Instant initial = Instant.FromUtc(2025, 1, 1, 0, 0, 0);
        Instant targetInstant = initial + Duration.FromSeconds(30);
        DateTimeOffset targetUtc = new DateTimeOffset(targetInstant.ToDateTimeUtc(), TimeSpan.Zero);
        IPrimeTestClock clock = new PrimeTestClock(initial);
        int eventCount = 0;
        clock.ClockEvents += (_, _) => eventCount++;
        using (clock.RegisterTimer(Duration.FromSeconds(10),
                   _ => { },
                   TestContext.Current.CancellationToken,
                   repeat: true))
        {
            clock.SetTime(targetUtc);
        }

        eventCount.Should().Be(3);
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies that a single forward <see cref="IPrimeTestClock.SetInstant"/> that lands after multiple interval
    ///   ticks invokes each callback with <see cref="IPrimeTestClock.NowInstant"/> at that tick&apos;s firing instant.
    /// </summary>
    [Fact]
    public void SetInstant_Forward_OverMultipleIntervalTicks_CallbackSeesNowInstantAtEachFiringInstant ()
    {
        Instant initial = Instant.FromUtc(2025, 1, 1, 0, 0, 0);
        Instant targetInstant = initial + Duration.FromSeconds(30);
        IPrimeTestClock clock = new PrimeTestClock(initial);
        List<Instant> observedNow = [];
        using (clock.RegisterTimer(Duration.FromSeconds(10),
                   _ => observedNow.Add(clock.NowInstant),
                   TestContext.Current.CancellationToken,
                   repeat: true))
        {
            clock.SetInstant(targetInstant);
        }

        observedNow.Should().HaveCount(3);
        observedNow[0].Should().Be(initial + Duration.FromSeconds(10));
        observedNow[1].Should().Be(initial + Duration.FromSeconds(20));
        observedNow[2].Should().Be(initial + Duration.FromSeconds(30));
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies that <see cref="IPrimeTestClock.ClockEvents"/> is raised once per distinct virtual instant crossed
    ///   during one forward <see cref="IPrimeTestClock.SetInstant"/> when multiple interval ticks occur.
    /// </summary>
    [Fact]
    public void SetInstant_Forward_SpanningMultipleDueInstants_RaisesClockEventsOncePerDistinctInstant ()
    {
        Instant initial = Instant.FromUtc(2025, 1, 1, 0, 0, 0);
        Instant targetInstant = initial + Duration.FromSeconds(30);
        IPrimeTestClock clock = new PrimeTestClock(initial);
        int eventCount = 0;
        clock.ClockEvents += (_, _) => eventCount++;
        using (clock.RegisterTimer(Duration.FromSeconds(10),
                   _ => { },
                   TestContext.Current.CancellationToken,
                   repeat: true))
        {
            clock.SetInstant(targetInstant);
        }

        eventCount.Should().Be(3);
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies that a single forward <see cref="IPrimeTestClock.SetLocalTime"/> that lands after multiple interval
    ///   ticks invokes each callback with <see cref="IPrimeTestClock.NowInstant"/> at that tick&apos;s firing instant.
    /// </summary>
    [Fact]
    public void SetLocalTime_Forward_OverMultipleIntervalTicks_CallbackSeesNowInstantAtEachFiringInstant ()
    {
        Instant initial = Instant.FromUtc(2025, 1, 1, 0, 0, 0);
        DateTimeZone zone = DateTimeZone.Utc;
        IPrimeTestClock clock = new PrimeTestClock(initial, zone);
        LocalDateTime targetLocal = initial.InUtc().Plus(Duration.FromSeconds(30)).LocalDateTime;
        List<Instant> observedNow = [];
        using (clock.RegisterTimer(Duration.FromSeconds(10),
                   _ => observedNow.Add(clock.NowInstant),
                   TestContext.Current.CancellationToken,
                   repeat: true))
        {
            clock.SetLocalTime(targetLocal);
        }

        observedNow.Should().HaveCount(3);
        observedNow[0].Should().Be(initial + Duration.FromSeconds(10));
        observedNow[1].Should().Be(initial + Duration.FromSeconds(20));
        observedNow[2].Should().Be(initial + Duration.FromSeconds(30));
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies that <see cref="IPrimeTestClock.ClockEvents"/> is raised once per distinct virtual instant crossed
    ///   during one forward <see cref="IPrimeTestClock.SetLocalTime"/> when multiple interval ticks occur.
    /// </summary>
    [Fact]
    public void SetLocalTime_Forward_SpanningMultipleDueInstants_RaisesClockEventsOncePerDistinctInstant ()
    {
        Instant initial = Instant.FromUtc(2025, 1, 1, 0, 0, 0);
        DateTimeZone zone = DateTimeZone.Utc;
        IPrimeTestClock clock = new PrimeTestClock(initial, zone);
        LocalDateTime targetLocal = initial.InUtc().Plus(Duration.FromSeconds(30)).LocalDateTime;
        int eventCount = 0;
        clock.ClockEvents += (_, _) => eventCount++;
        using (clock.RegisterTimer(Duration.FromSeconds(10),
                   _ => { },
                   TestContext.Current.CancellationToken,
                   repeat: true))
        {
            clock.SetLocalTime(targetLocal);
        }

        eventCount.Should().Be(3);
    }
    //----------------------------------------------------------------------------

    #endregion SetTime, SetInstant, SetLocalTime — forward virtual march

    #region SetTime, SetInstant, SetLocalTime — backward virtual time rules

    /// <summary>
    ///   Verifies that <see cref="IPrimeTestClock.SetTime"/> to a strictly earlier virtual instant throws while the
    ///   clock is running.
    /// </summary>
    [Fact]
    public void ClockRunning_SetTimeToPastValue_ThrowsInvalidOperationException ()
    {
        Instant initial = Instant.FromUtc(2025, 1, 1, 12, 0, 0);
        DateTimeOffset earlier = new DateTimeOffset((initial - Duration.FromHours(1)).ToDateTimeUtc(), TimeSpan.Zero);
        IPrimeTestClock clock = new PrimeTestClock(initial);
        clock.Start((Duration?)null);
        Action act = () => clock.SetTime(earlier);
        act.Should().Throw<InvalidOperationException>().WithMessage("*running*");
        clock.Stop().Should().BeTrue();
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies that a strictly backward <see cref="IPrimeTestClock.SetInstant"/> throws while the clock is running.
    /// </summary>
    [Fact]
    public void ClockRunning_SetInstantBackward_ThrowsInvalidOperationException ()
    {
        Instant initial = Instant.FromUtc(2025, 1, 1, 12, 0, 0);
        Instant earlier = initial - Duration.FromHours(1);
        IPrimeTestClock clock = new PrimeTestClock(initial);
        clock.Start((Duration?)null);
        Action act = () => clock.SetInstant(earlier);
        act.Should().Throw<InvalidOperationException>().WithMessage("*running*");
        clock.Stop().Should().BeTrue();
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies that a strictly backward <see cref="IPrimeTestClock.SetLocalTime"/> throws while the clock is running.
    /// </summary>
    [Fact]
    public void ClockRunning_SetLocalTimeBackward_ThrowsInvalidOperationException ()
    {
        Instant initial = Instant.FromUtc(2025, 1, 2, 12, 0, 0);
        LocalDateTime earlierLocal = new LocalDateTime(2025, 1, 1, 0, 0, 0);
        IPrimeTestClock clock = new PrimeTestClock(initial, DateTimeZone.Utc);
        clock.Start((Duration?)null);
        Action act = () => clock.SetLocalTime(earlierLocal);
        act.Should().Throw<InvalidOperationException>().WithMessage("*running*");
        clock.Stop().Should().BeTrue();
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies that a strictly backward <see cref="IPrimeTestClock.SetTime"/> throws when any interval timer
    ///   registration is still active, even if the clock is not running.
    /// </summary>
    [Fact]
    public void ClockStopped_ActiveIntervalTimer_SetTimeBackward_ThrowsInvalidOperationException ()
    {
        Instant initial = Instant.FromUtc(2025, 1, 1, 12, 0, 0);
        DateTimeOffset earlier = new DateTimeOffset((initial - Duration.FromHours(1)).ToDateTimeUtc(), TimeSpan.Zero);
        IPrimeTestClock clock = new PrimeTestClock(initial);
        using (clock.RegisterTimer(Duration.FromMinutes(1), Duration.FromMinutes(1), _ => { },
                   TestContext.Current.CancellationToken))
        {
            Action act = () => clock.SetTime(earlier);
            act.Should().Throw<InvalidOperationException>().WithMessage("*interval*");
        }
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies that a strictly backward <see cref="IPrimeTestClock.SetTime"/> succeeds when the clock is stopped
    ///   and the only interval registration has been disposed (no longer active).
    /// </summary>
    [Fact]
    public void ClockStopped_DisposedIntervalTimer_SetTimeBackward_UpdatesNowInstant ()
    {
        Instant initial = Instant.FromUtc(2025, 1, 1, 12, 0, 0);
        DateTimeOffset earlier = new DateTimeOffset((initial - Duration.FromHours(1)).ToDateTimeUtc(), TimeSpan.Zero);
        IPrimeTestClock clock = new PrimeTestClock(initial);
        IClockIntervalTimer timer = clock.RegisterTimer(Duration.FromMinutes(1), Duration.FromMinutes(1), _ => { },
            TestContext.Current.CancellationToken);
        timer.Dispose();
        clock.SetTime(earlier);
        clock.NowInstant.Should().Be(initial - Duration.FromHours(1));
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies that a permitted strictly backward <see cref="IPrimeTestClock.SetTime"/> raises
    ///   <see cref="IPrimeTestClock.ClockEvents"/> exactly once for the new instant.
    /// </summary>
    [Fact]
    public void ClockStopped_PermittedBackwardSetTime_RaisesClockEventsOnce ()
    {
        Instant initial = Instant.FromUtc(2025, 1, 1, 12, 0, 0);
        DateTimeOffset earlier = new DateTimeOffset((initial - Duration.FromHours(1)).ToDateTimeUtc(), TimeSpan.Zero);
        IPrimeTestClock clock = new PrimeTestClock(initial);
        int eventCount = 0;
        clock.ClockEvents += (_, _) => eventCount++;
        clock.SetTime(earlier);
        eventCount.Should().Be(1);
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies that after a forward march fires a local day-time timer (UTC zone) and a permitted backward jump,
    ///   <see cref="IClockTimer.TimeUntilNextCallback"/> reflects the next same-calendar occurrence.
    /// </summary>
    [Fact]
    public void ClockStopped_LocalDayTimeUtcZone_AfterForwardFireAndBackwardJump_TimeUntilNextCallback_MatchesNextSameDayOccurrence ()
    {
        Instant morning = Instant.FromUtc(2025, 1, 1, 10, 0, 0);
        Instant evening = Instant.FromUtc(2025, 1, 1, 18, 0, 0);
        DateTimeOffset eveningOffset = new DateTimeOffset(evening.ToDateTimeUtc(), TimeSpan.Zero);
        DateTimeOffset morningOffset = new DateTimeOffset(morning.ToDateTimeUtc(), TimeSpan.Zero);
        IPrimeTestClock clock = new PrimeTestClock(morning, DateTimeZone.Utc);
        using IClockDayTimeTimer timer = clock.RegisterTimeOfDay(new LocalTime(12, 0),
            _ => { },
            TestContext.Current.CancellationToken);
        clock.SetTime(eveningOffset);
        long msAfterForward = timer.TimeUntilNextCallback;
        msAfterForward.Should().Be((long)TimeSpan.FromHours(18).TotalMilliseconds);
        clock.SetTime(morningOffset);
        timer.TimeUntilNextCallback.Should().Be((long)TimeSpan.FromHours(2).TotalMilliseconds);
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies that a local day-time timer (UTC zone) fires once per discrete occurrence when virtual time is
    ///   moved backward (permitted) between two forward marches across the same nominal fire instant.
    /// </summary>
    [Fact]
    public void ClockStopped_LocalDayTimeUtcZone_PermittedBackwardThenForwardAgain_FiresOncePerCrossing ()
    {
        Instant morning = Instant.FromUtc(2025, 1, 1, 10, 0, 0);
        Instant evening = Instant.FromUtc(2025, 1, 1, 18, 0, 0);
        DateTimeOffset eveningOffset = new DateTimeOffset(evening.ToDateTimeUtc(), TimeSpan.Zero);
        DateTimeOffset morningOffset = new DateTimeOffset(morning.ToDateTimeUtc(), TimeSpan.Zero);
        IPrimeTestClock clock = new PrimeTestClock(morning, DateTimeZone.Utc);
        int fireCount = 0;
        using (clock.RegisterTimeOfDay(new LocalTime(12, 0),
                   _ => fireCount++,
                   TestContext.Current.CancellationToken))
        {
            clock.SetTime(eveningOffset);
            fireCount.Should().Be(1);
            clock.SetTime(morningOffset);
            fireCount.Should().Be(1);
            clock.SetTime(eveningOffset);
            fireCount.Should().Be(2);
        }
    }
    //----------------------------------------------------------------------------

    #endregion SetTime, SetInstant, SetLocalTime — backward virtual time rules

    #endregion SetInstant, SetTime, SetLocalTime and Advance

    #region RunFor

    /// <summary>
    ///   Verifies that <see cref="IPrimeTestClock.RunFor(Duration)"/> advances virtual time by the given duration
    ///   when the bounded run completes.
    /// </summary>
    [Fact]
    public void RunFor_WithDuration_AdvancesVirtualTimeByDuration ()
    {
        Instant initial = Instant.FromUtc(2025, 1, 1, 0, 0, 0);
        Duration runDuration = Duration.FromMinutes(30);
        IPrimeTestClock clock = new PrimeTestClock(initial);
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        clock.RunFor(runDuration, Duration.FromTimeSpan(RunForTestFastPerSecondRate)).Should().BeTrue();
        WaitUntilClockStopped(() => clock.IsRunning, RunForTestWaitTimeout, cancellationToken);
        clock.NowInstant.Should().Be(initial + runDuration);
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies that <see cref="IPrimeTestClock.RunFor(NodaTime.Duration, NodaTime.Duration)"/> returns
    ///   <c>false</c> when the clock is already running.
    /// </summary>
    [Fact]
    public void RunFor_WhenAlreadyRunning_ReturnsFalse ()
    {
        IPrimeTestClock clock = new PrimeTestClock();
        clock.Start(Duration.FromTimeSpan(RunForTestFastPerSecondRate));
        clock.RunFor(Duration.FromMinutes(1), Duration.FromTimeSpan(RunForTestFastPerSecondRate)).Should().BeFalse();
        clock.Stop().Should().BeTrue();
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies that zero-duration <see cref="IPrimeTestClock.RunFor"/> raises started and stopped lifecycle
    ///   events and leaves the clock not running.
    /// </summary>
    [Fact]
    public void RunFor_WithZeroDuration_RaisesStartedAndStoppedAndNotRunning ()
    {
        IPrimeTestClock clock = new PrimeTestClock();
        using ClockEventTypeCollector eventTypes = new(clock);
        clock.RunFor(Duration.Zero).Should().BeTrue();
        clock.IsRunning.Should().BeFalse();
        eventTypes.Snapshot().Should().Equal(
            PrimeTestClockEventType.ClockStarted,
            PrimeTestClockEventType.ClockStopped);
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies that <see cref="IPrimeTestClock.RunFor(NodaTime.Duration, NodaTime.Duration)"/> rejects an
    ///   out-of-range per-second rate.
    /// </summary>
    [Fact]
    public void RunFor_WithInvalidPerSecondRate_ThrowsArgumentOutOfRangeException ()
    {
        IPrimeTestClock clock = new PrimeTestClock();
        Action act = () => clock.RunFor(Duration.FromMinutes(1), StartRunRateBelowMinimum);
        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("perSecondRate");
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies that negative <see cref="IPrimeTestClock.RunFor"/> duration is treated like zero duration.
    /// </summary>
    [Fact]
    public void RunFor_WithNegativeDuration_TreatedAsZero ()
    {
        Instant initial = Instant.FromUtc(2025, 1, 1, 0, 0, 0);
        IPrimeTestClock clock = new PrimeTestClock(initial);
        using ClockEventTypeCollector eventTypes = new(clock);
        clock.RunFor(Duration.FromMinutes(-5)).Should().BeTrue();
        clock.IsRunning.Should().BeFalse();
        clock.NowInstant.Should().Be(initial);
        eventTypes.Snapshot().Should().Equal(
            PrimeTestClockEventType.ClockStarted,
            PrimeTestClockEventType.ClockStopped);
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies that a non-zero <see cref="IPrimeTestClock.RunFor"/> raises
    ///   <see cref="PrimeTestClockEventType.ClockStarted"/> before
    ///   <see cref="PrimeTestClockEventType.ClockStopped"/> when the run completes.
    /// </summary>
    [Fact]
    public void RunFor_WhenStarted_RaisesClockStartedThenClockStopped ()
    {
        IPrimeTestClock clock = new PrimeTestClock();
        using ClockEventTypeCollector eventTypes = new(clock);
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        clock.RunFor(Duration.FromSeconds(10), Duration.FromTimeSpan(RunForTestFastPerSecondRate)).Should().BeTrue();
        WaitUntilClockStopped(() => clock.IsRunning, RunForTestWaitTimeout, cancellationToken);
        WaitUntilCondition(
            () => eventTypes.ContainsEventType(PrimeTestClockEventType.ClockStopped),
            RunForTestWaitTimeout,
            cancellationToken,
            "Timed out waiting for ClockStopped after "
                + RunForTestWaitTimeout.TotalSeconds.ToString("g0", CultureInfo.InvariantCulture)
                + " seconds.");
        List<PrimeTestClockEventType> snapshot = eventTypes.Snapshot();
        int startedIndex = snapshot.IndexOf(PrimeTestClockEventType.ClockStarted);
        int stoppedIndex = snapshot.LastIndexOf(PrimeTestClockEventType.ClockStopped);
        startedIndex.Should().BeGreaterThanOrEqualTo(0);
        stoppedIndex.Should().BeGreaterThan(startedIndex);
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies that when a bounded <see cref="IPrimeTestClock.RunFor"/> completes naturally on the automatic
    ///   runner thread (without external <see cref="IPrimeTestClock.Advance"/> or set-time mutations), the clock stops
    ///   cleanly and raises exactly one <see cref="PrimeTestClockEventType.ClockStopped"/> at the stop horizon.
    /// </summary>
    [Fact]
    public void RunFor_WhenRunnerLoopReachesStopHorizon_CompletesWithSingleClockStoppedAtHorizon ()
    {
        Instant initial = Instant.FromUtc(2025, 1, 1, 0, 0, 0);
        Duration runForDuration = Duration.FromSeconds(10);
        Instant runForStopInstant = initial + runForDuration;
        IPrimeTestClock clock = new PrimeTestClock(initial);
        using ClockEventCollector collector = new(clock);
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        clock.RunFor(runForDuration, Duration.FromTimeSpan(RunForTestFastPerSecondRate)).Should().BeTrue();
        PrimeTestClockBoundedRunAssertionHelpers.AssertBoundedRunCompletedWithSingleClockStoppedAtHorizon(
            clock,
            collector,
            runForStopInstant,
            runForStopInstant,
            RunForTestWaitTimeout,
            cancellationToken,
            "Timed out waiting for ClockStopped after runner-loop bounded completion.");
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies that completing an active bounded <see cref="IPrimeTestClock.RunFor"/> via the external march path
    ///   while executing on the automatic runner thread (for example <see cref="IPrimeTestClock.Advance"/> from a
    ///   <see cref="IPrimeTestClock.ClockEvents"/> handler) does not self-join deadlock and raises exactly one
    ///   <see cref="PrimeTestClockEventType.ClockStopped"/> at the stop horizon.
    /// </summary>
    [Fact]
    public void RunFor_WhenAdvanceFromClockEventsOnRunnerThread_CompletesWithSingleClockStoppedAtHorizon ()
    {
        Instant initial = Instant.FromUtc(2025, 1, 1, 0, 0, 0);
        Duration runForDuration = Duration.FromMinutes(10);
        Duration advanceDuration = Duration.FromMinutes(30);
        Instant runForStopInstant = initial + runForDuration;
        IPrimeTestClock clock = new PrimeTestClock(initial);
        using ClockEventCollector collector = new(clock);
        object advanceFromRunnerCallbackSync = new();
        bool advancedFromRunnerCallback = false;
        Instant advanceTargetInstant = initial;
        clock.ClockEvents += (_, e) =>
        {
            if (e.EventType != PrimeTestClockEventType.NewTime)
            {
                return;
            }

            lock (advanceFromRunnerCallbackSync)
            {
                if (advancedFromRunnerCallback)
                {
                    return;
                }

                advancedFromRunnerCallback = true;
                advanceTargetInstant = clock.NowInstant + advanceDuration;
            }

            clock.Advance(advanceDuration);
        };
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        clock.RunFor(runForDuration, Duration.FromTimeSpan(RunForTestFastPerSecondRate)).Should().BeTrue();
        WaitUntilCondition(
            () =>
            {
                lock (advanceFromRunnerCallbackSync)
                {
                    return advancedFromRunnerCallback;
                }
            },
            RunForTestWaitTimeout,
            cancellationToken,
            "Timed out waiting for Advance from a ClockEvents handler on the runner thread.");
        Instant expectedAdvanceTargetInstant;
        lock (advanceFromRunnerCallbackSync)
        {
            expectedAdvanceTargetInstant = advanceTargetInstant;
        }

        PrimeTestClockBoundedRunAssertionHelpers.AssertBoundedRunCompletedWithSingleClockStoppedAtHorizon(
            clock,
            collector,
            expectedAdvanceTargetInstant,
            runForStopInstant,
            RunForTestWaitTimeout,
            cancellationToken,
            "Timed out waiting for ClockStopped after bounded completion from a runner-thread ClockEvents callback.");
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies that a second <see cref="IPrimeTestClock.RunFor"/> while the first bounded run is still active
    ///   returns <c>false</c> and does not complete the first run at the wrong horizon when the first run is
    ///   finished via <see cref="IPrimeTestClock.Advance"/>.
    /// </summary>
    [Fact]
    public void RunFor_WhenSecondRunForWhileFirstActive_ReturnsFalseAndCompletesFirstRunAtHorizon ()
    {
        Instant initial = Instant.FromUtc(2025, 1, 1, 0, 0, 0);
        Duration firstRunForDuration = Duration.FromMinutes(10);
        Duration secondRunForDuration = Duration.FromMinutes(5);
        Duration advancePastHorizon = Duration.FromMinutes(30);
        Instant firstRunForStopInstant = initial + firstRunForDuration;
        IPrimeTestClock clock = new PrimeTestClock(initial);
        using ClockEventCollector collector = new(clock);
        object overlappingRunForSync = new();
        bool attemptedOverlappingRunForFromHandler = false;
        clock.ClockEvents += (_, e) =>
        {
            if (e.EventType != PrimeTestClockEventType.NewTime)
            {
                return;
            }

            lock (overlappingRunForSync)
            {
                if (attemptedOverlappingRunForFromHandler)
                {
                    return;
                }

                attemptedOverlappingRunForFromHandler = true;
            }

            clock.RunFor(
                    secondRunForDuration,
                    Duration.FromTimeSpan(RunForTestFastPerSecondRate))
                .Should()
                .BeFalse();
        };
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        clock.RunFor(firstRunForDuration, Duration.FromTimeSpan(RunForTestFastPerSecondRate)).Should().BeTrue();
        clock.RunFor(secondRunForDuration, Duration.FromTimeSpan(RunForTestFastPerSecondRate)).Should().BeFalse();
        WaitUntilCondition(
            () =>
            {
                lock (overlappingRunForSync)
                {
                    return attemptedOverlappingRunForFromHandler;
                }
            },
            RunForTestWaitTimeout,
            cancellationToken,
            "Timed out waiting for overlapping RunFor attempt from a ClockEvents handler.");
        clock.Advance(advancePastHorizon);
        Instant advanceTargetInstant = clock.NowInstant;
        PrimeTestClockBoundedRunAssertionHelpers.AssertBoundedRunCompletedWithSingleClockStoppedAtHorizon(
            clock,
            collector,
            advanceTargetInstant,
            firstRunForStopInstant,
            RunForTestWaitTimeout,
            cancellationToken);
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies that two sequential bounded <see cref="IPrimeTestClock.RunFor"/> runs each raise exactly one
    ///   <see cref="PrimeTestClockEventType.ClockStopped"/> at that run's stop horizon and do not complete the other
    ///   run's generation.
    /// </summary>
    [Fact]
    public void RunFor_WhenTwoSequentialBoundedRuns_EachRaisesClockStoppedAtOwnHorizon ()
    {
        Instant initial = Instant.FromUtc(2025, 1, 1, 0, 0, 0);
        Duration firstRunForDuration = Duration.FromMinutes(10);
        Duration secondRunForDuration = Duration.FromMinutes(5);
        Duration advancePastHorizon = Duration.FromMinutes(30);
        Instant firstRunForStopInstant = initial + firstRunForDuration;
        IPrimeTestClock clock = new PrimeTestClock(initial);
        using ClockEventCollector collector = new(clock);
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        clock.RunFor(firstRunForDuration, Duration.FromTimeSpan(RunForTestFastPerSecondRate)).Should().BeTrue();
        clock.Advance(advancePastHorizon);
        Instant afterFirstRunCommittedInstant = clock.NowInstant;
        PrimeTestClockBoundedRunAssertionHelpers.AssertBoundedRunCompletedWithSingleClockStoppedAtHorizon(
            clock,
            collector,
            afterFirstRunCommittedInstant,
            firstRunForStopInstant,
            RunForTestWaitTimeout,
            cancellationToken);
        Instant secondRunStartInstant = clock.NowInstant;
        Instant secondRunForStopInstant = secondRunStartInstant + secondRunForDuration;
        clock.RunFor(secondRunForDuration, Duration.FromTimeSpan(RunForTestFastPerSecondRate)).Should().BeTrue();
        clock.Advance(advancePastHorizon);
        Instant afterSecondRunCommittedInstant = clock.NowInstant;
        WaitUntilCondition(
            () => collector.Snapshot().Count(e => e.EventType == PrimeTestClockEventType.ClockStopped) == 2,
            RunForTestWaitTimeout,
            cancellationToken,
            "Timed out waiting for ClockStopped from the second bounded RunFor.");
        clock.IsRunning.Should().BeFalse();
        clock.NowInstant.Should().Be(afterSecondRunCommittedInstant);
        List<PrimeTestClockEvent> snapshot = collector.Snapshot();
        snapshot.Count(e => e.EventType == PrimeTestClockEventType.ClockStopped).Should().Be(2);
        PrimeTestClockStoppedEvent[] stoppedEvents = [.. snapshot.OfType<PrimeTestClockStoppedEvent>()];
        stoppedEvents[0].ClockInstant.Should().Be(firstRunForStopInstant);
        stoppedEvents[1].ClockInstant.Should().Be(secondRunForStopInstant);
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies that when two concurrent <see cref="IPrimeTestClock.Advance"/> calls both cross an active
    ///   <see cref="IPrimeTestClock.RunFor"/> stop horizon, bounded completion is committed exactly once, one competing
    ///   completion path safely returns without duplicating <see cref="PrimeTestClockEventType.ClockStopped"/>, and both
    ///   callers make forward progress.
    /// </summary>
    [Fact]
    public void RunFor_WhenConcurrentAdvanceCallsRaceAcrossStopHorizon_RaisesSingleClockStoppedAndMakesProgress ()
    {
        Instant initial = Instant.FromUtc(2025, 1, 1, 0, 0, 0);
        Duration runForDuration = Duration.FromHours(6);
        Duration advanceDuration = Duration.FromHours(12);
        Instant runForStopInstant = initial + runForDuration;
        IPrimeTestClock clock = new PrimeTestClock(initial);
        using ClockEventCollector collector = new(clock);
        using ManualResetEventSlim releaseConcurrentAdvances = new(false);
        int readyAdvanceCallers = 0;
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        clock.RunFor(runForDuration, Duration.FromTimeSpan(RunForTestFastPerSecondRate)).Should().BeTrue();
        Task advanceCallerOne = Task.Run(() =>
        {
            Interlocked.Increment(ref readyAdvanceCallers);
            releaseConcurrentAdvances.Wait(cancellationToken);
            clock.Advance(advanceDuration);
        }, cancellationToken);
        Task advanceCallerTwo = Task.Run(() =>
        {
            Interlocked.Increment(ref readyAdvanceCallers);
            releaseConcurrentAdvances.Wait(cancellationToken);
            clock.Advance(advanceDuration);
        }, cancellationToken);
        WaitUntilCondition(
            () => Volatile.Read(ref readyAdvanceCallers) == 2,
            RunForTestWaitTimeout,
            cancellationToken,
            "Timed out waiting for concurrent Advance callers to be ready.");
        releaseConcurrentAdvances.Set();
        Task.WaitAll([advanceCallerOne, advanceCallerTwo], cancellationToken);
        WaitUntilCondition(
            () => collector.Snapshot().Count(e => e.EventType == PrimeTestClockEventType.ClockStopped) == 1,
            RunForTestWaitTimeout,
            cancellationToken,
            "Timed out waiting for bounded completion after concurrent Advance calls.");
        clock.IsRunning.Should().BeFalse();
        clock.NowInstant.Should().BeGreaterThan(initial);
        List<PrimeTestClockEvent> snapshot = collector.Snapshot();
        snapshot.Count(e => e.EventType == PrimeTestClockEventType.ClockStopped).Should().Be(1);
        PrimeTestClockStoppedEvent stoppedEvent = snapshot.OfType<PrimeTestClockStoppedEvent>().Should().ContainSingle().Subject;
        stoppedEvent.ClockInstant.Should().Be(runForStopInstant);
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies that <see cref="IPrimeTestClock.Advance"/> past an active <see cref="IPrimeTestClock.RunFor"/>
    ///   stop horizon raises <see cref="PrimeTestClockEventType.ClockStopped"/> at the horizon, leaves the clock
    ///   stopped, and commits the <see cref="IPrimeTestClock.Advance"/> target instant.
    /// </summary>
    [Fact]
    public void RunFor_WhenAdvancePassesStopHorizon_StopsAtHorizonAndReachesAdvanceTarget ()
    {
        Instant initial = Instant.FromUtc(2025, 1, 1, 0, 0, 0);
        Duration runForDuration = Duration.FromMinutes(10);
        Duration advanceDuration = Duration.FromMinutes(30);
        Instant runForStopInstant = initial + runForDuration;
        Instant advanceTargetInstant = initial + advanceDuration;
        IPrimeTestClock clock = new PrimeTestClock(initial);
        using ClockEventCollector collector = new(clock);
        clock.RunFor(runForDuration, Duration.FromTimeSpan(RunForTestFastPerSecondRate)).Should().BeTrue();
        clock.Advance(advanceDuration);
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        PrimeTestClockBoundedRunAssertionHelpers.AssertBoundedRunCompletedWithSingleClockStoppedAtHorizon(
            clock,
            collector,
            advanceTargetInstant,
            runForStopInstant,
            RunForTestWaitTimeout,
            cancellationToken);
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies that <see cref="IPrimeTestClock.Advance"/> to exactly the active
    ///   <see cref="IPrimeTestClock.RunFor"/> stop horizon completes the bounded run at that instant.
    /// </summary>
    [Fact]
    public void RunFor_WhenAdvanceReachesStopHorizonExactly_StopsAtHorizonAndNotRunning ()
    {
        Instant initial = Instant.FromUtc(2025, 1, 1, 0, 0, 0);
        Duration runForDuration = Duration.FromMinutes(10);
        Instant runForStopInstant = initial + runForDuration;
        IPrimeTestClock clock = new PrimeTestClock(initial);
        using ClockEventCollector collector = new(clock);
        clock.RunFor(runForDuration, Duration.FromTimeSpan(RunForTestFastPerSecondRate)).Should().BeTrue();
        clock.Advance(runForDuration);
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        PrimeTestClockBoundedRunAssertionHelpers.AssertBoundedRunCompletedWithSingleClockStoppedAtHorizon(
            clock,
            collector,
            runForStopInstant,
            runForStopInstant,
            RunForTestWaitTimeout,
            cancellationToken);
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies that forward <see cref="IPrimeTestClock.SetInstant"/> past an active
    ///   <see cref="IPrimeTestClock.RunFor"/> stop horizon raises
    ///   <see cref="PrimeTestClockEventType.ClockStopped"/> at the horizon, leaves the clock stopped, and commits the
    ///   requested instant.
    /// </summary>
    [Fact]
    public void RunFor_WhenSetInstantPassesStopHorizon_StopsAtHorizonAndReachesSetInstantTarget ()
    {
        Instant initial = Instant.FromUtc(2025, 1, 1, 0, 0, 0);
        Duration runForDuration = Duration.FromMinutes(10);
        Instant runForStopInstant = initial + runForDuration;
        Instant setInstantTarget = initial + Duration.FromMinutes(45);
        IPrimeTestClock clock = new PrimeTestClock(initial);
        using ClockEventCollector collector = new(clock);
        clock.RunFor(runForDuration, Duration.FromTimeSpan(RunForTestFastPerSecondRate)).Should().BeTrue();
        clock.SetInstant(setInstantTarget);
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        PrimeTestClockBoundedRunAssertionHelpers.AssertBoundedRunCompletedWithSingleClockStoppedAtHorizon(
            clock,
            collector,
            setInstantTarget,
            runForStopInstant,
            RunForTestWaitTimeout,
            cancellationToken);
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies that forward <see cref="IPrimeTestClock.SetInstant"/> to exactly the active
    ///   <see cref="IPrimeTestClock.RunFor"/> stop horizon completes the bounded run at that instant.
    /// </summary>
    [Fact]
    public void RunFor_WhenSetInstantReachesStopHorizonExactly_StopsAtHorizonAndNotRunning ()
    {
        Instant initial = Instant.FromUtc(2025, 1, 1, 0, 0, 0);
        Duration runForDuration = Duration.FromMinutes(10);
        Instant runForStopInstant = initial + runForDuration;
        IPrimeTestClock clock = new PrimeTestClock(initial);
        using ClockEventCollector collector = new(clock);
        clock.RunFor(runForDuration, Duration.FromTimeSpan(RunForTestFastPerSecondRate)).Should().BeTrue();
        clock.SetInstant(runForStopInstant);
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        PrimeTestClockBoundedRunAssertionHelpers.AssertBoundedRunCompletedWithSingleClockStoppedAtHorizon(
            clock,
            collector,
            runForStopInstant,
            runForStopInstant,
            RunForTestWaitTimeout,
            cancellationToken);
    }
    //----------------------------------------------------------------------------

    #endregion RunFor

    #region Start and Stop

    /// <summary>
    ///   Verifies that <see cref="IPrimeTestClock.IsRunning"/> is false when not started.
    /// </summary>
    [Fact]
    public void IsRunning_WhenNotStarted_ReturnsFalse ()
    {
        IPrimeTestClock clock = new PrimeTestClock();
        clock.IsRunning.Should().BeFalse();
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies that <see cref="IPrimeTestClock.Stop"/> returns false when not running.
    /// </summary>
    [Fact]
    public void Stop_WhenNotRunning_ReturnsFalse ()
    {
        IPrimeTestClock clock = new PrimeTestClock();
        clock.Stop().Should().BeFalse();
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies that <see cref="IPrimeTestClock.Start(NodaTime.Duration?)"/> and <see cref="IPrimeTestClock.Stop"/> set
    ///   IsRunning and that Stop returns true when was running.
    /// </summary>
    [Fact]
    public void Start_ThenStop_SetsIsRunningAndStopReturnsTrue ()
    {
        IPrimeTestClock clock = new PrimeTestClock(Instant.FromUtc(2025, 1, 1, 0, 0, 0));
        clock.Start(Duration.FromSeconds(1));
        clock.IsRunning.Should().BeTrue();
        bool stopped = clock.Stop();
        stopped.Should().BeTrue();
        clock.IsRunning.Should().BeFalse();
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies that <see cref="IPrimeTestClock.Start(NodaTime.Duration?)"/> with <c>null</c> starts at the default
    ///   1:1 virtual-to-real rate.
    /// </summary>
    [Fact]
    public void Start_WithNullRate_StartsSuccessfully ()
    {
        IPrimeTestClock clock = new PrimeTestClock(Instant.FromUtc(2025, 1, 1, 0, 0, 0));
        clock.Start((Duration?)null);
        clock.IsRunning.Should().BeTrue();
        clock.Stop().Should().BeTrue();
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies that the minimum allowed explicit run rate (100 ms virtual per real second) starts successfully.
    /// </summary>
    [Fact]
    public void Start_WithMinimumAllowedRate_StartsSuccessfully ()
    {
        IPrimeTestClock clock = new PrimeTestClock(Instant.FromUtc(2025, 1, 1, 0, 0, 0));
        clock.Start(MinimumAllowedStartRunRate);
        clock.IsRunning.Should().BeTrue();
        clock.Stop().Should().BeTrue();
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies that the maximum allowed explicit run rate (1 hour virtual per real second) starts successfully.
    /// </summary>
    [Fact]
    public void Start_WithMaximumAllowedRate_StartsSuccessfully ()
    {
        IPrimeTestClock clock = new PrimeTestClock(Instant.FromUtc(2025, 1, 1, 0, 0, 0));
        clock.Start(Duration.FromHours(1));
        clock.IsRunning.Should().BeTrue();
        clock.Stop().Should().BeTrue();
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies that a run rate below the minimum throws and does not start the clock.
    /// </summary>
    [Fact]
    public void Start_WithRateBelowMinimum_ThrowsArgumentOutOfRangeException ()
    {
        IPrimeTestClock clock = new PrimeTestClock();
        Action act = () => clock.Start(StartRunRateBelowMinimum);
        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("perSecondRate");
        clock.IsRunning.Should().BeFalse();
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies that a run rate one tick below the minimum throws and does not start the clock.
    /// </summary>
    [Fact]
    public void Start_WithRateOneTickBelowMinimum_ThrowsArgumentOutOfRangeException ()
    {
        IPrimeTestClock clock = new PrimeTestClock();
        Duration belowMinimum = MinimumAllowedStartRunRate - Duration.FromTicks(1);
        Action act = () => clock.Start(belowMinimum);
        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("perSecondRate");
        clock.IsRunning.Should().BeFalse();
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies that a run rate above the maximum throws and does not start the clock.
    /// </summary>
    [Fact]
    public void Start_WithRateAboveMaximum_ThrowsArgumentOutOfRangeException ()
    {
        IPrimeTestClock clock = new PrimeTestClock();
        Action act = () => clock.Start(Duration.FromHours(1) + Duration.FromMinutes(1));
        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("perSecondRate");
        clock.IsRunning.Should().BeFalse();
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies that a run rate one tick above the maximum throws and does not start the clock.
    /// </summary>
    [Fact]
    public void Start_WithRateOneTickAboveMaximum_ThrowsArgumentOutOfRangeException ()
    {
        IPrimeTestClock clock = new PrimeTestClock();
        Duration aboveMaximum = Duration.FromHours(1) + Duration.FromTicks(1);
        Action act = () => clock.Start(aboveMaximum);
        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("perSecondRate");
        clock.IsRunning.Should().BeFalse();
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies that a zero run rate throws and does not start the clock.
    /// </summary>
    [Fact]
    public void Start_WithZeroRate_ThrowsArgumentOutOfRangeException ()
    {
        IPrimeTestClock clock = new PrimeTestClock();
        Action act = () => clock.Start(Duration.Zero);
        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("perSecondRate");
        clock.IsRunning.Should().BeFalse();
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies that while the automatic runner is active, <see cref="IPrimeTestClock.NowInstant"/> reflects linear
    ///   projection of virtual time from the committed instant and monotonic anchor between explicit commits.
    /// </summary>
    /// <remarks>
    ///   <para>
    ///     Expected virtual time is computed from a wall <see cref="Stopwatch"/> around the same real delay, using the
    ///     same virtual-per-real-second scaling as the clock (not a fixed nominal delay), so slow or parallel test
    ///     scheduling does not skew the assertion. A small fixed virtual tolerance remains for residual skew: the
    ///     clock&apos;s anchor stopwatch is a different instance than the test stopwatch and starts before the wall
    ///     timer begins, <see cref="Task.Delay(TimeSpan, CancellationToken)"/> does not guarantee exact wall duration,
    ///     and persist-on-read on <see cref="IPrimeTestClock.NowInstant"/> is measured before the wall timer stops so
    ///     post-read scheduling does not inflate the observed instant.
    ///   </para>
    /// </remarks>
    [Fact]
    public async Task ClockRunning_NowInstantRead_AfterRealElapsed_ApproximatesProjectedInstant ()
    {
        Instant initial = Instant.FromUtc(2020, 6, 1, 0, 0, 0);
        Duration runRate = Duration.FromSeconds(10);
        IPrimeTestClock clock = new PrimeTestClock(initial, DateTimeZone.Utc);
        clock.Start(runRate);
        Stopwatch wall = Stopwatch.StartNew();
        await Task.Delay(ClockRunningProjectionTestWallDelay, TestContext.Current.CancellationToken);
        Instant observed = clock.NowInstant;
        wall.Stop();
        clock.Stop().Should().BeTrue();
        TimeSpan virtualElapsedFromWall =
            PrimeTestClock.ScaleRealElapsedToVirtualTime(wall.Elapsed, runRate.ToTimeSpan());
        Instant expectedFromWall = initial + Duration.FromTimeSpan(virtualElapsedFromWall);
        observed.Should().BeGreaterThanOrEqualTo(expectedFromWall - ClockRunningProjectionVirtualTolerance)
            .And.BeLessThanOrEqualTo(expectedFromWall + ClockRunningProjectionVirtualTolerance);
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies that a persist-on-read on <see cref="IPrimeTestClock.NowInstant"/> while the runner is active marches
    ///   through due virtual-time work so a pending delay can complete without waiting for the coarse runner sleep.
    /// </summary>
    [Fact]
    public async Task ClockRunning_DelayDue_NowInstantRead_CompletesDelayTask ()
    {
        Instant initial = Instant.FromUtc(2020, 6, 1, 12, 0, 0);
        IPrimeTestClock clock = new PrimeTestClock(initial, DateTimeZone.Utc);
        Task delayTask = clock.DelayAsync(Duration.FromSeconds(5), TestContext.Current.CancellationToken);
        clock.Start(Duration.FromSeconds(10));
        await Task.Delay(ClockRunningDelayDueTestWallDelay, TestContext.Current.CancellationToken);
        _ = clock.NowInstant;
        clock.Stop().Should().BeTrue();
        delayTask.Status.Should().Be(TaskStatus.RanToCompletion);
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies that when the automatic runner is not active, <see cref="IPrimeTestClock.NowInstant"/> does not
    ///   advance with real time between explicit virtual-time operations.
    /// </summary>
    [Fact]
    public async Task ClockStopped_NowInstantRead_AfterRealElapsed_ReturnsPersistedInstant ()
    {
        Instant initial = Instant.FromUtc(2020, 6, 1, 0, 0, 0);
        IPrimeTestClock clock = new PrimeTestClock(initial, DateTimeZone.Utc);
        await Task.Delay(ClockStoppedNoAdvanceTestWallDelay, TestContext.Current.CancellationToken);
        clock.NowInstant.Should().Be(initial);
    }
    //----------------------------------------------------------------------------

    #endregion Start and Stop

    #region Runner loop

    /// <summary>
    ///   Verifies that the deadline-driven runner completes a delay registered after automatic virtual-time advancement
    ///   is started on <see cref="IPrimeTestClock"/>, without requiring a persist-on-read on
    ///   <see cref="IPrimeTestClock.NowInstant"/>.
    /// </summary>
    /// <remarks>
    ///   <para>
    ///     Uses a wall-clock ceiling (<see cref="SleepTestRealTimeTimeout"/>) so the delay task races a maximum wait,
    ///     allowing slow CI to pass when the runner still completes well under that budget.
    ///   </para>
    /// </remarks>
    [Fact]
    public async Task ClockRunning_DelayRegisteredWhileRunning_RunnerCompletesDelayWithoutPersistOnRead ()
    {
        Instant initial = Instant.FromUtc(2020, 6, 1, 12, 0, 0);
        IPrimeTestClock clock = new PrimeTestClock(initial, DateTimeZone.Utc);
        clock.Start(Duration.FromSeconds(10));
        await Task.Delay(TimeSpan.FromMilliseconds(50), TestContext.Current.CancellationToken);
        Task delayTask = clock.DelayAsync(Duration.FromSeconds(5), TestContext.Current.CancellationToken);
        Task delayOrTimeout = await Task.WhenAny(delayTask,
            Task.Delay(SleepTestRealTimeTimeout.ToTimeSpan(), TestContext.Current.CancellationToken));
        try
        {
            delayOrTimeout.Should().BeSameAs(delayTask);
            delayTask.Status.Should().Be(TaskStatus.RanToCompletion);
        }
        finally
        {
            clock.Stop().Should().BeTrue();
        }
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies that cancelling a delay while the runner is active completes the delay task promptly without waiting
    ///   for the full virtual due horizon.
    /// </summary>
    [Fact]
    public async Task ClockRunning_CancelDelayWhileRunning_CancelsPromptly ()
    {
        Instant initial = Instant.FromUtc(2020, 6, 1, 12, 0, 0);
        using CancellationTokenSource cancelSource = new();
        IPrimeTestClock clock = new PrimeTestClock(initial, DateTimeZone.Utc);
        Task delayTask = clock.DelayAsync(Duration.FromHours(1), cancelSource.Token);
        clock.Start(Duration.FromSeconds(1));
        await Task.Delay(TimeSpan.FromMilliseconds(100), TestContext.Current.CancellationToken);
        cancelSource.Cancel();
        Func<Task> awaitCanceled = async () => await delayTask;
        await awaitCanceled.Should().ThrowAsync<OperationCanceledException>();
        clock.Stop().Should().BeTrue();
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies that after one quiet virtual minute with no substantive work, the runner raises
    ///   <see cref="IPrimeTestClock.ClockEvents"/> for the heartbeat instant.
    /// </summary>
    [Fact]
    public async Task ClockRunning_QuietVirtualMinute_RaisesClockEventsHeartbeat ()
    {
        Instant initial = Instant.FromUtc(2020, 6, 1, 0, 0, 0);
        IPrimeTestClock clock = new PrimeTestClock(initial, DateTimeZone.Utc);
        int eventCount = 0;
        Instant? lastHeartbeatInstant = null;
        clock.ClockEvents += (_, e) =>
        {
            if (e is PrimeTestClockNewTimeEvent newTime)
            {
                eventCount++;
                lastHeartbeatInstant = newTime.ClockInstant;
            }
        };
        clock.Start(Duration.FromSeconds(10));
        await Task.Delay(ClockRunningHeartbeatTestWallDelay, TestContext.Current.CancellationToken);
        clock.Stop().Should().BeTrue();
        eventCount.Should().BeGreaterThanOrEqualTo(1);
        lastHeartbeatInstant.Should().NotBeNull();
        Instant expectedHeartbeat = initial + Duration.FromMinutes(1);
        lastHeartbeatInstant!.Value.Should().BeGreaterThanOrEqualTo(expectedHeartbeat - ClockRunningHeartbeatVirtualTolerance)
            .And.BeLessThanOrEqualTo(expectedHeartbeat + ClockRunningHeartbeatVirtualTolerance);
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies that <see cref="IPrimeTestClock.Stop"/> returns promptly while the runner would otherwise wait for a
    ///   virtual-minute heartbeat at a slow run rate.
    /// </summary>
    [Fact]
    public async Task ClockRunning_StopWhileWaitingForHeartbeat_ReturnsPromptly ()
    {
        IPrimeTestClock clock = new PrimeTestClock(Instant.FromUtc(2020, 6, 1, 0, 0, 0), DateTimeZone.Utc);
        clock.Start(Duration.FromSeconds(10));
        await Task.Delay(TimeSpan.FromMilliseconds(50), TestContext.Current.CancellationToken);
        Stopwatch stopElapsed = Stopwatch.StartNew();
        clock.Stop().Should().BeTrue();
        stopElapsed.Stop();
        stopElapsed.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(2));
        clock.IsRunning.Should().BeFalse();
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies that at the minimum run rate a short virtual delay completes before the old fixed one-second real poll
    ///   would have advanced virtual time.
    /// </summary>
    [Fact]
    public async Task ClockRunning_SlowRate_ShortVirtualDelay_CompletesBeforeOneSecondRealPoll ()
    {
        Instant initial = Instant.FromUtc(2020, 6, 1, 0, 0, 0);
        IPrimeTestClock clock = new PrimeTestClock(initial, DateTimeZone.Utc);
        Task delayTask = clock.DelayAsync(Duration.FromMilliseconds(50), TestContext.Current.CancellationToken);
        Stopwatch wall = Stopwatch.StartNew();
        clock.Start(MinimumAllowedStartRunRate);
        Task delayOrTimeout = await Task.WhenAny(delayTask,
            Task.Delay(ClockRunningSlowRateDelayMaxWallDelay, TestContext.Current.CancellationToken));
        wall.Stop();
        try
        {
            delayOrTimeout.Should().BeSameAs(delayTask);
            delayTask.Status.Should().Be(TaskStatus.RanToCompletion);
            wall.Elapsed.Should().BeLessThan(ClockRunningSlowRateDelayMaxWallDelay);
            wall.Elapsed.Should().BeGreaterThan(ClockRunningSlowRateDelayMinWallDelay);
        }
        finally
        {
            clock.Stop().Should().BeTrue();
        }
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies that when intended real waits are below 15 ms, the runner bursts virtual steps and completes multiple
    ///   short delays without a large real wall delay.
    /// </summary>
    [Fact]
    public async Task ClockRunning_SubMinimumRealWaits_BurstCompletesMultipleDelaysQuickly ()
    {
        Instant initial = Instant.FromUtc(2020, 6, 1, 0, 0, 0);
        IPrimeTestClock clock = new PrimeTestClock(initial, DateTimeZone.Utc);
        Task delay10Ms = clock.DelayAsync(Duration.FromMilliseconds(10), TestContext.Current.CancellationToken);
        Task delay20Ms = clock.DelayAsync(Duration.FromMilliseconds(20), TestContext.Current.CancellationToken);
        Task delay30Ms = clock.DelayAsync(Duration.FromMilliseconds(30), TestContext.Current.CancellationToken);
        Stopwatch wall = Stopwatch.StartNew();
        clock.Start(Duration.FromSeconds(10));
        await Task.WhenAll(delay10Ms, delay20Ms, delay30Ms);
        wall.Stop();
        try
        {
            wall.Elapsed.Should().BeLessThan(ClockRunningBurstPathMaxWallDelay);
            clock.NowInstant.Should().BeGreaterThanOrEqualTo(initial + Duration.FromMilliseconds(30));
        }
        finally
        {
            clock.Stop().Should().BeTrue();
        }
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies that after a real elapsed slice the runner catch-up budget completes every due delay up to the
    ///   measured virtual horizon, not only work due within the first intended real wait.
    /// </summary>
    [Fact]
    public async Task ClockRunning_MultipleDelays_AfterRealElapsed_CompletesAllFromCatchUpBudget ()
    {
        IPrimeTestClock clock = new PrimeTestClock(Instant.FromUtc(2020, 6, 1, 0, 0, 0), DateTimeZone.Utc);
        Task delay300Ms = clock.DelayAsync(Duration.FromMilliseconds(300), TestContext.Current.CancellationToken);
        Task delay400Ms = clock.DelayAsync(Duration.FromMilliseconds(400), TestContext.Current.CancellationToken);
        Task delay500Ms = clock.DelayAsync(Duration.FromMilliseconds(500), TestContext.Current.CancellationToken);
        Stopwatch wall = Stopwatch.StartNew();
        clock.Start(Duration.FromSeconds(10));
        Task allDelays = Task.WhenAll(delay300Ms, delay400Ms, delay500Ms);
        Task completed = await Task.WhenAny(allDelays,
            Task.Delay(ClockRunningCatchUpTestWallDelay, TestContext.Current.CancellationToken));
        wall.Stop();
        try
        {
            completed.Should().BeSameAs(allDelays);
            wall.Elapsed.Should().BeGreaterThan(ClockRunningCatchUpMinWallDelay);
            wall.Elapsed.Should().BeLessThan(ClockRunningCatchUpTestWallDelay);
        }
        finally
        {
            clock.Stop().Should().BeTrue();
        }
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies that a substantive interval timer firing resets the virtual-minute heartbeat window so the first
    ///   heartbeat <see cref="IPrimeTestClock.ClockEvents"/> is after one quiet virtual minute from that fire, not from
    ///   clock start.
    /// </summary>
    [Fact]
    public async Task ClockRunning_SubstantiveTimerMidWindow_ResetsHeartbeatCadence ()
    {
        Instant initial = Instant.FromUtc(2020, 6, 1, 0, 0, 0);
        IPrimeTestClock clock = new PrimeTestClock(initial, DateTimeZone.Utc);
        List<Instant> eventInstants = [];
        object sync = new();
        clock.ClockEvents += (_, e) =>
        {
            if (e is PrimeTestClockNewTimeEvent newTime)
            {
                lock (sync)
                {
                    eventInstants.Add(newTime.ClockInstant);
                }
            }
        };
        int timerFireCount = 0;
        using IClockIntervalTimer registration = clock.RegisterTimer(Duration.FromSeconds(45),
            () => Interlocked.Increment(ref timerFireCount),
            TestContext.Current.CancellationToken);
        clock.Start(Duration.FromSeconds(10));
        await Task.Delay(ClockRunningHeartbeatResetTestWallDelay, TestContext.Current.CancellationToken);
        clock.Stop().Should().BeTrue();
        timerFireCount.Should().BeGreaterThanOrEqualTo(1);
        List<Instant> copy;
        lock (sync)
        {
            copy = [.. eventInstants];
        }

        Duration uncanceledHeartbeatWindowStart = Duration.FromSeconds(58);
        Duration uncanceledHeartbeatWindowEnd = Duration.FromSeconds(62);
        foreach (Instant instant in copy)
        {
            Duration delta = instant - initial;
            bool inUncanceledHeartbeatWindow = delta >= uncanceledHeartbeatWindowStart
                && delta <= uncanceledHeartbeatWindowEnd;
            inUncanceledHeartbeatWindow.Should().BeFalse(
                "a 45 s substantive timer should reset the heartbeat window so no heartbeat-only raise occurs near T+60 s (instant {0})",
                instant);
        }

        Instant resetHeartbeatLower = initial + Duration.FromSeconds(45) + Duration.FromMinutes(1)
            - ClockRunningResetHeartbeatVirtualTolerance;
        Instant resetHeartbeatUpper = initial + Duration.FromSeconds(45) + Duration.FromMinutes(1)
            + ClockRunningResetHeartbeatVirtualTolerance;
        copy.Should().Contain(instant => instant >= resetHeartbeatLower && instant <= resetHeartbeatUpper);
    }
    //----------------------------------------------------------------------------

    #endregion Runner loop

    #region ClockEvents

    /// <summary>
    ///   Subscribes to <see cref="IPrimeTestClock.ClockEvents"/> and retains the most recent event of
    ///   <typeparamref name="TEvent"/>.
    /// </summary>
    /// <typeparam name="TEvent">Derived <see cref="PrimeTestClockEvent"/> type to capture.</typeparam>
    private sealed class ClockEventCapture<TEvent> where TEvent : PrimeTestClockEvent
    {
        private readonly object _sync = new();
        private TEvent? _lastReceived;

        public ClockEventCapture (IPrimeTestClock clock)
        {
            clock.ClockEvents += OnClockEvent;
        }

        public TEvent? LastReceived
        {
            get
            {
                lock (_sync)
                {
                    return _lastReceived;
                }
            }
        }

        private void OnClockEvent (object? sender, PrimeTestClockEvent e)
        {
            if (e is not TEvent typed)
            {
                return;
            }

            lock (_sync)
            {
                _lastReceived = typed;
            }
        }
    }

    /// <summary>
    ///   Verifies that <see cref="IPrimeTestClock.ClockEvents"/> raises
    ///   <see cref="PrimeTestClockEventType.NewTime"/> with the expected <see cref="PrimeTestClockTimedEvent.ClockInstant"/>
    ///   when <see cref="IPrimeTestClock.SetInstant"/> is called.
    /// </summary>
    [Fact]
    public void SetInstant_WhenClockEventsSubscribed_RaisesNewTimeWithExpectedInstant ()
    {
        Instant setInstant = Instant.FromUtc(2025, 2, 20, 10, 0, 0);
        IPrimeTestClock clock = new PrimeTestClock();
        ClockEventCapture<PrimeTestClockNewTimeEvent> capture = new(clock);
        clock.SetInstant(setInstant);
        PrimeTestClockNewTimeEvent? received = capture.LastReceived;
        received.Should().NotBeNull();
        received!.EventType.Should().Be(PrimeTestClockEventType.NewTime);
        received.ClockInstant.Should().Be(setInstant);
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies that <see cref="IPrimeTestClock.ClockEvents"/> raises
    ///   <see cref="PrimeTestClockEventType.NewTime"/> with the march target instant when
    ///   <see cref="IPrimeTestClock.Advance"/> is called.
    /// </summary>
    [Fact]
    public void Advance_WhenClockEventsSubscribed_RaisesNewTimeWithExpectedInstant ()
    {
        Instant initial = Instant.FromUtc(2025, 1, 1, 0, 0, 0);
        Instant expected = initial + Duration.FromHours(1);
        IPrimeTestClock clock = new PrimeTestClock(initial);
        ClockEventCapture<PrimeTestClockNewTimeEvent> capture = new(clock);
        clock.Advance(Duration.FromHours(1));
        PrimeTestClockNewTimeEvent? received = capture.LastReceived;
        received.Should().NotBeNull();
        received!.EventType.Should().Be(PrimeTestClockEventType.NewTime);
        received.ClockInstant.Should().Be(expected);
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies that <see cref="IPrimeTestClock.ClockEvents"/> raises
    ///   <see cref="PrimeTestClockEventType.NewTime"/> at the bounded run stop instant when
    ///   <see cref="IPrimeTestClock.RunFor"/> completes.
    /// </summary>
    [Fact]
    public void RunFor_WhenClockEventsSubscribed_RaisesNewTimeWithExpectedInstant ()
    {
        Instant initial = Instant.FromUtc(2025, 1, 1, 0, 0, 0);
        Instant expected = initial + Duration.FromMinutes(15);
        IPrimeTestClock clock = new PrimeTestClock(initial);
        using ClockEventListCapture<PrimeTestClockNewTimeEvent> capture = new(clock);
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        clock.RunFor(Duration.FromMinutes(15), Duration.FromTimeSpan(RunForTestFastPerSecondRate)).Should().BeTrue();
        WaitUntilClockStopped(() => clock.IsRunning, RunForTestWaitTimeout, cancellationToken);
        WaitUntilCondition(
            () => capture.Any(e => e.ClockInstant == expected),
            RunForTestWaitTimeout,
            cancellationToken,
            "Timed out waiting for NewTime at the RunFor stop instant after "
                + RunForTestWaitTimeout.TotalSeconds.ToString("g0", CultureInfo.InvariantCulture)
                + " seconds.");
        clock.NowInstant.Should().Be(expected);
        capture.Snapshot().Should().Contain(e => e.ClockInstant == expected);
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies that <see cref="PrimeTestClockEventType.NewTime"/> raised while the clock is stopped exposes
    ///   <see langword="null"/> <see cref="PrimeTestClockTimedEvent.RunRateDuration"/> and
    ///   <see cref="PrimeTestClockTimedEvent.RunRateTimeSpan"/>.
    /// </summary>
    [Fact]
    public void SetInstant_WhileStopped_NewTimeHasNullRunRate ()
    {
        Instant setInstant = Instant.FromUtc(2025, 2, 20, 10, 0, 0);
        IPrimeTestClock clock = new PrimeTestClock(Instant.FromUtc(2025, 1, 1, 0, 0, 0));
        ClockEventCapture<PrimeTestClockNewTimeEvent> capture = new(clock);
        clock.SetInstant(setInstant);
        PrimeTestClockNewTimeEvent? received = capture.LastReceived;
        received.Should().NotBeNull();
        received!.RunRateDuration.Should().BeNull();
        received.RunRateTimeSpan.Should().BeNull();
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies that each <see cref="PrimeTestClockEventType.NewTime"/> raised during
    ///   <see cref="IPrimeTestClock.Advance"/> while the automatic runner is active includes the current run rate and
    ///   that Noda <see cref="PrimeTestClockTimedEvent.ClockInstant"/> matches
    ///   <see cref="PrimeTestClockTimedEvent.ClockTime"/>.
    /// </summary>
    [Fact]
    public void Advance_WhileRunning_NewTimeIncludesRunRate ()
    {
        Instant initial = Instant.FromUtc(2025, 1, 1, 0, 0, 0);
        Duration runRate = Duration.FromSeconds(5);
        IPrimeTestClock clock = new PrimeTestClock(initial);
        using ClockEventListCapture<PrimeTestClockNewTimeEvent> capture = new(clock);
        clock.Start(runRate);
        List<PrimeTestClockNewTimeEvent> snapshot = [];
        try
        {
            clock.Advance(Duration.FromMinutes(1));
            snapshot = capture.Snapshot();
        }
        finally
        {
            clock.Stop().Should().BeTrue();
        }

        snapshot.Should().NotBeEmpty();
        snapshot.Should().OnlyContain(e => e.EventType == PrimeTestClockEventType.NewTime);
        foreach (PrimeTestClockNewTimeEvent newTime in snapshot)
        {
            newTime.RunRateDuration.Should().Be(runRate);
            newTime.RunRateTimeSpan.Should().Be(runRate.ToTimeSpan());
            newTime.ClockInstant.Should().Be(Instant.FromDateTimeOffset(newTime.ClockTime));
        }
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies that <see cref="IPrimeTestClock.Start"/> raises
    ///   <see cref="PrimeTestClockEventType.ClockStarted"/> on a stopped-to-running transition.
    /// </summary>
    [Fact]
    public void Start_FromStopped_RaisesClockStartedWithEventType ()
    {
        Instant initial = Instant.FromUtc(2025, 1, 1, 0, 0, 0);
        Duration runRate = Duration.FromSeconds(5);
        IPrimeTestClock clock = new PrimeTestClock(initial);
        using ClockEventCollector collector = new(clock);
        clock.Start(runRate);
        try
        {
            List<PrimeTestClockEvent> snapshot = collector.Snapshot();

            PrimeTestClockStartedEvent started = snapshot
                .OfType<PrimeTestClockStartedEvent>()
                .Should()
                .ContainSingle()
                .Subject;
            started.EventType.Should().Be(PrimeTestClockEventType.ClockStarted);
            started.ClockInstant.Should().Be(initial);
            started.RunRateDuration.Should().Be(runRate);
        }
        finally
        {
            clock.Stop().Should().BeTrue();
        }
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies that a second <see cref="IPrimeTestClock.Start"/> while already running does not raise
    ///   <see cref="PrimeTestClockEventType.ClockStarted"/> again.
    /// </summary>
    [Fact]
    public void Start_WhenAlreadyRunning_DoesNotRaiseClockStarted ()
    {
        Instant initial = Instant.FromUtc(2025, 1, 1, 0, 0, 0);
        Duration runRate = Duration.FromSeconds(5);
        IPrimeTestClock clock = new PrimeTestClock(initial);
        using ClockEventCollector collector = new(clock);
        clock.Start(runRate);
        collector.Clear();

        try
        {
            clock.Start(runRate);
            List<PrimeTestClockEvent> snapshot = collector.Snapshot();

            snapshot.Should().NotContain(e => e.EventType == PrimeTestClockEventType.ClockStarted);
        }
        finally
        {
            clock.Stop().Should().BeTrue();
        }
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies that <see cref="IPrimeTestClock.Start"/> does not raise
    ///   <see cref="PrimeTestClockEventType.NewTime"/> synchronously on the calling thread (the automatic runner may
    ///   raise <see cref="PrimeTestClockEventType.NewTime"/> later on a background thread).
    /// </summary>
    [Fact]
    public void Start_FromStopped_DoesNotRaiseNewTimeOnCallingThread ()
    {
        Instant initial = Instant.FromUtc(2025, 1, 1, 0, 0, 0);
        IPrimeTestClock clock = new PrimeTestClock(initial);
        int startCallerThreadId = Environment.CurrentManagedThreadId;
        object sync = new();
        bool newTimeOnStartCallerThread = false;
        clock.ClockEvents += (_, e) =>
        {
            if (e.EventType != PrimeTestClockEventType.NewTime)
                return;
            if (Environment.CurrentManagedThreadId != startCallerThreadId)
                return;
            lock (sync)
            {
                newTimeOnStartCallerThread = true;
            }
        };
        try
        {
            clock.Start(Duration.FromSeconds(1));
            lock (sync)
            {
                newTimeOnStartCallerThread.Should().BeFalse();
            }
        }
        finally
        {
            clock.Stop().Should().BeTrue();
        }
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies that <see cref="IPrimeTestClock.Stop"/> on a stopped clock returns <c>false</c> and does not raise
    ///   <see cref="PrimeTestClockEventType.ClockStopped"/>.
    /// </summary>
    [Fact]
    public void Stop_WhenNotRunning_DoesNotRaiseClockStopped ()
    {
        IPrimeTestClock clock = new PrimeTestClock(Instant.FromUtc(2025, 1, 1, 0, 0, 0));
        using ClockEventCollector collector = new(clock);
        clock.Stop().Should().BeFalse();
        List<PrimeTestClockEvent> snapshot = collector.Snapshot();

        snapshot.Should().NotContain(e => e.EventType == PrimeTestClockEventType.ClockStopped);
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies that <see cref="IPrimeTestClock.Stop"/> raises
    ///   <see cref="PrimeTestClockEventType.ClockStopped"/> after the runner joins, with the final committed instant
    ///   and the rate that was running, and that no <see cref="PrimeTestClockEventType.NewTime"/> follows shutdown.
    /// </summary>
    /// <remarks>
    ///   Virtual time is advanced deterministically via <see cref="IPrimeTestClock.Advance"/> while the runner is
    ///   active (no wall-clock wait). After stop, <see cref="IPrimeTestClock.NowInstant"/> and
    ///   <see cref="IPrimeTestClock.UtcNowDateTimeOffset"/> must match the
    ///   <see cref="PrimeTestClockStoppedEvent"/> payload (post-join
    ///   <c>CommitVirtualUtcInstantLocked</c> / <c>ReadVirtualUtcNowLocked</c> path), including UTC normalization on
    ///   <see cref="PrimeTestClockTimedEvent.ClockTime"/>.
    /// </remarks>
    [Fact]
    public void Stop_WhenRunning_RaisesClockStoppedAsLastEventWithCommittedInstant ()
    {
        Instant initial = Instant.FromUtc(2025, 1, 1, 12, 0, 0);
        Duration runRate = Duration.FromSeconds(10);
        PrimeTestClock clock = new(initial);
        using ClockEventCollector collector = new(clock);
        clock.Start(runRate);
        clock.Advance(Duration.FromMinutes(1));
        clock.Stop().Should().BeTrue();
        List<PrimeTestClockEvent> snapshot = collector.Snapshot();

        int stoppedAt = snapshot.FindLastIndex(e => e.EventType == PrimeTestClockEventType.ClockStopped);
        stoppedAt.Should().BeGreaterThanOrEqualTo(0);
        snapshot[stoppedAt].EventType.Should().Be(PrimeTestClockEventType.ClockStopped);
        snapshot[snapshot.Count - 1].EventType.Should().Be(PrimeTestClockEventType.ClockStopped);
        snapshot.Skip(stoppedAt + 1).Should().BeEmpty();

        PrimeTestClockStoppedEvent stopped = (PrimeTestClockStoppedEvent)snapshot[stoppedAt];
        stopped.RunRateDuration.Should().Be(runRate);

        Instant committedInstant = clock.NowInstant;
        DateTimeOffset committedUtc = clock.UtcNowDateTimeOffset;
        stopped.ClockInstant.Should().Be(committedInstant);
        stopped.ClockTime.Should().Be(committedUtc);
        committedUtc.Should().Be(new DateTimeOffset(committedInstant.ToDateTimeUtc(), TimeSpan.Zero));
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies that a second <see cref="IPrimeTestClock.Start"/> after
    ///   <see cref="IPrimeTestClock.Stop"/> raises <see cref="PrimeTestClockEventType.ClockStarted"/> only after the
    ///   prior run's <see cref="PrimeTestClockEventType.ClockStopped"/>.
    /// </summary>
    [Fact]
    public void StartStopStart_SecondClockStartedOccursAfterFirstClockStopped ()
    {
        Instant initial = Instant.FromUtc(2025, 1, 1, 0, 0, 0);
        Duration runRate = Duration.FromSeconds(5);
        PrimeTestClock clock = new(initial);
        using ClockEventTypeCollector eventTypes = new(clock);
        clock.Start(runRate);
        clock.Stop().Should().BeTrue();
        clock.Start(runRate);
        clock.Stop().Should().BeTrue();
        List<PrimeTestClockEventType> snapshot = eventTypes.Snapshot();

        int firstClockStoppedIndex = snapshot.IndexOf(PrimeTestClockEventType.ClockStopped);
        int secondClockStartedIndex = snapshot.LastIndexOf(PrimeTestClockEventType.ClockStarted);
        firstClockStoppedIndex.Should().BeGreaterThanOrEqualTo(0);
        secondClockStartedIndex.Should().BeGreaterThan(firstClockStoppedIndex);
    }
    /// <summary>
    ///   Wall-clock guard for tests that wait for <see cref="PrimeTestClock.TestRunnerStopJoinPhaseEntered"/>.
    /// </summary>
    private static readonly TimeSpan ConcurrentStopStartCoordinationTimeout = TimeSpan.FromSeconds(5);

    //----------------------------------------------------------------------------
    /// <summary>
    ///   Runs <see cref="IPrimeTestClock.Stop"/> and <see cref="IPrimeTestClock.Start"/> concurrently so
    ///   <c>Start</c> begins only after <c>Stop</c> has entered the runner-join phase.
    /// </summary>
    /// <remarks>
    ///   Holds the automatic runner inside a blocking timer callback before <c>Stop</c> is invoked so
    ///   <see cref="PrimeTestClock.TestRunnerStopJoinPhaseEntered"/> remains set for the duration of the join
    ///   (polling with <c>Wait(0)</c> can miss the brief set/reset window under parallel test scheduling).
    /// </remarks>
    /// <param name="clock">Clock that is already running.</param>
    /// <param name="runRate">Run rate for the overlapping <c>Start</c>.</param>
    /// <param name="cancellationToken">Cancellation token for the test run.</param>
    /// <returns>The completed <c>Stop</c> task result.</returns>
    private static async Task<bool> RunStopWithOverlappingStartAfterStopJoinPhaseBeginsAsync (
        PrimeTestClock clock,
        Duration runRate,
        CancellationToken cancellationToken)
    {
        using ManualResetEventSlim runnerEnteredBlockingCallback = new(initialState: false);
        using ManualResetEventSlim releaseRunner = new(initialState: false);
        using IClockIntervalTimer blockingTimer = RegisterRunnerBlockingTimer(clock,
            runnerEnteredBlockingCallback,
            releaseRunner,
            cancellationToken);
        try
        {
            bool runnerBlockedInCallback = runnerEnteredBlockingCallback.Wait(
                RunnerBlockedInCallbackWaitTimeout,
                cancellationToken);
            runnerBlockedInCallback.Should().BeTrue(
                "the automatic runner should enter the blocking timer callback before Stop is invoked.");

            Task<bool> stopTask = Task.Run(clock.Stop, cancellationToken);
            bool stopJoinPhaseEntered = clock.TestRunnerStopJoinPhaseEntered.Wait(
                ConcurrentStopStartCoordinationTimeout,
                cancellationToken);
            stopJoinPhaseEntered.Should().BeTrue(
                "Stop should enter the runner-join phase before the coordination wait times out.");
            Task startTask = Task.Run(() => clock.Start(runRate), cancellationToken);
            releaseRunner.Set();
            await Task.WhenAll(stopTask, startTask);
            return await stopTask;
        }
        finally
        {
            releaseRunner.Set();
        }
    }

    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies that <see cref="IPrimeTestClock.Start"/> invoked while <see cref="IPrimeTestClock.Stop"/> is joining
    ///   the runner does not raise <see cref="PrimeTestClockEventType.ClockStarted"/> before
    ///   <see cref="PrimeTestClockEventType.ClockStopped"/> for the run being stopped.
    /// </summary>
    [Fact]
    public async Task Start_DuringConcurrentStop_DoesNotRaiseClockStartedBeforeClockStopped ()
    {
        Instant initial = Instant.FromUtc(2025, 1, 1, 0, 0, 0);
        Duration runRate = Duration.FromSeconds(5);
        PrimeTestClock clock = new(initial);
        using ClockEventTypeCollector eventTypes = new(clock);
        clock.Start(runRate);
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        (await RunStopWithOverlappingStartAfterStopJoinPhaseBeginsAsync(clock, runRate, cancellationToken))
            .Should().BeTrue();
        clock.IsRunning.Should().BeTrue();
        clock.Stop().Should().BeTrue();
        List<PrimeTestClockEventType> snapshot = eventTypes.Snapshot();

        int firstClockStoppedIndex = snapshot.IndexOf(PrimeTestClockEventType.ClockStopped);
        int restartClockStartedIndex = snapshot.LastIndexOf(PrimeTestClockEventType.ClockStarted);
        firstClockStoppedIndex.Should().BeGreaterThanOrEqualTo(0);
        restartClockStartedIndex.Should().BeGreaterThan(firstClockStoppedIndex);
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies that overlapping <see cref="IPrimeTestClock.Start"/> and <see cref="IPrimeTestClock.Stop"/> calls
    ///   leave at most one automatic runner active when the calls complete.
    /// </summary>
    [Fact]
    public async Task Start_DuringConcurrentStop_LeavesSingleRunnerWhenCallsComplete ()
    {
        PrimeTestClock clock = new(Instant.FromUtc(2025, 1, 1, 0, 0, 0));
        Duration runRate = Duration.FromSeconds(5);
        clock.Start(runRate);
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        (await RunStopWithOverlappingStartAfterStopJoinPhaseBeginsAsync(clock, runRate, cancellationToken))
            .Should().BeTrue();
        clock.IsRunning.Should().BeTrue();
        clock.Stop().Should().BeTrue();
        clock.IsRunning.Should().BeFalse();
        clock.Stop().Should().BeFalse();
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Real-time guard for tests that wait until the automatic runner enters a blocking timer callback.
    /// </summary>
    private static readonly TimeSpan RunnerBlockedInCallbackWaitTimeout = TimeSpan.FromSeconds(15);

    /// <summary>
    ///   Minimum elapsed real time expected when <see cref="IPrimeTestClock.Stop"/> waits for a blocked runner join.
    /// </summary>
    private static readonly TimeSpan StopJoinTimeoutLowerBound = TimeSpan.FromSeconds(4);

    //----------------------------------------------------------------------------
    /// <summary>
    ///   Registers an interval timer whose callback blocks until <paramref name="releaseRunner"/> is set, so the
    ///   automatic runner thread can be held inside virtual-time dispatch.
    /// </summary>
    /// <param name="clock">Clock that will run the timer on the runner thread.</param>
    /// <param name="runnerEnteredBlockingCallback">
    ///   Signaled when the callback begins waiting on <paramref name="releaseRunner"/>.
    /// </param>
    /// <param name="releaseRunner">Set to unblock the runner callback.</param>
    /// <param name="cancellationToken">Cancellation token for the timer registration and wait.</param>
    /// <returns>The timer registration to dispose when the test completes.</returns>
    private static IClockIntervalTimer RegisterRunnerBlockingTimer (
        IPrimeTestClock clock,
        ManualResetEventSlim runnerEnteredBlockingCallback,
        ManualResetEventSlim releaseRunner,
        CancellationToken cancellationToken)
    {
        return clock.RegisterTimer(Duration.FromMilliseconds(50),
            () =>
            {
                runnerEnteredBlockingCallback.Set();
                releaseRunner.Wait(cancellationToken);
            },
            cancellationToken);
    }

    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies that when <see cref="IPrimeTestClock.Stop"/> cannot join a blocked runner within the join timeout,
    ///   it returns <c>false</c> and subsequent <see cref="IPrimeTestClock.Start"/>,
    ///   <see cref="IPrimeTestClock.Advance"/>, and <see cref="IPrimeTestClock.SetInstant"/> throw.
    /// </summary>
    [Fact]
    public void Stop_WhenRunnerJoinTimesOut_ReturnsFalseAndBlocksStartAndMutations ()
    {
        Instant initial = Instant.FromUtc(2025, 1, 1, 0, 0, 0);
        PrimeTestClock clock = new(initial);
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using ManualResetEventSlim runnerEnteredBlockingCallback = new(false);
        using ManualResetEventSlim releaseRunner = new(false);
        using IClockIntervalTimer blockingTimer = RegisterRunnerBlockingTimer(clock,
            runnerEnteredBlockingCallback,
            releaseRunner,
            cancellationToken);
        try
        {
            clock.Start(Duration.FromSeconds(10));
            bool runnerBlocked = runnerEnteredBlockingCallback.Wait(
                RunnerBlockedInCallbackWaitTimeout,
                cancellationToken);
            runnerBlocked.Should().BeTrue(
                "the automatic runner should enter the blocking timer callback before Stop is invoked.");
            System.Diagnostics.Stopwatch stopElapsed = System.Diagnostics.Stopwatch.StartNew();
            bool stopResult = clock.Stop();
            stopElapsed.Stop();
            stopResult.Should().BeFalse();
            stopElapsed.Elapsed.Should().BeGreaterThanOrEqualTo(StopJoinTimeoutLowerBound);
            clock.IsRunning.Should().BeFalse();
            Action startAct = () => clock.Start(Duration.FromSeconds(1));
            startAct.Should().Throw<InvalidOperationException>().WithMessage("*Stop() returned false*");
            Action advanceAct = () => clock.Advance(Duration.FromSeconds(1));
            advanceAct.Should().Throw<InvalidOperationException>().WithMessage("*Stop() returned false*");
            Action setInstantAct = () => clock.SetInstant(initial + Duration.FromHours(1));
            setInstantAct.Should().Throw<InvalidOperationException>().WithMessage("*Stop() returned false*");
        }
        finally
        {
            releaseRunner.Set();
        }
    }
    //----------------------------------------------------------------------------

    #endregion ClockEvents

    #region Sleep (Duration) driven by virtual time

    /// <summary>
    ///   Verifies that Sleep(Duration) completes when virtual time is advanced by the sleep duration (deterministic).
    /// </summary>
    [Fact]
    public async Task Sleep_Duration_WhenAdvanceCoversDuration_CompletesWithoutRealDelay ()
    {
        Instant initial = Instant.FromUtc(2025, 1, 1, 0, 0, 0);
        IPrimeTestClock clock = new PrimeTestClock(initial);
        bool sleepCompleted = false;
        ManualResetEventSlim sleepRegistered = new(false);
        try
        {
            Task sleepTask = Task.Run(() =>
            {
                sleepRegistered.Set();
                clock.Sleep(Duration.FromSeconds(5));
                sleepCompleted = true;
            }, TestContext.Current.CancellationToken);

            sleepRegistered.Wait(SleepTestRealTimeTimeout.ToTimeSpan(), TestContext.Current.CancellationToken).Should().BeTrue("sleep task should register before advance");
            System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();
            while (!sleepCompleted && sw.Elapsed < SleepTestRealTimeTimeout.ToTimeSpan())
            {
                clock.Advance(Duration.FromSeconds(5));
                Thread.Sleep(0);
            }
            sleepCompleted.Should().BeTrue("sleep should have completed within the real-time timeout so that virtual advance could complete the delay");
            await sleepTask;
        }
        finally
        {
            sleepRegistered.Dispose();
        }
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies that Sleep(Duration.Zero) completes immediately on a test clock.
    /// </summary>
    [Fact]
    public void Sleep_WithDurationZero_CompletesWithoutThrowing ()
    {
        IPrimeTestClock clock = new PrimeTestClock();
        Action act = () => clock.Sleep(Duration.Zero);
        act.Should().NotThrow();
    }
    //----------------------------------------------------------------------------

    #endregion Sleep (Duration) driven by virtual time

    #region DelayAsync (Duration) driven by virtual time

    /// <summary>
    ///   Verifies that DelayAsync(Duration) completes when virtual time is advanced by the delay duration.
    /// </summary>
    [Fact]
    public async Task DelayAsync_Duration_WhenAdvanceCoversDuration_CompletesWithoutRealDelay ()
    {
        Instant initial = Instant.FromUtc(2025, 1, 1, 0, 0, 0);
        IPrimeTestClock clock = new PrimeTestClock(initial);
        Task delayTask = clock.DelayAsync(Duration.FromSeconds(3), TestContext.Current.CancellationToken);

        delayTask.IsCompleted.Should().BeFalse();
        clock.Advance(Duration.FromSeconds(3));
        await delayTask;
        delayTask.Status.Should().Be(TaskStatus.RanToCompletion);
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies that DelayAsync(Duration, CancellationToken) throws
    ///   <see cref="OperationCanceledException"/> when the cancellation token is triggered during the delay.
    /// </summary>
    [Fact]
    public async Task DelayAsync_Duration_WhenTokenCancelledDuringDelay_ThrowsOperationCanceledException ()
    {
        Instant initial = Instant.FromUtc(2025, 1, 1, 0, 0, 0);
        IPrimeTestClock clock = new PrimeTestClock(initial);
        using CancellationTokenSource cts = new();
        using CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cts.Token,
            TestContext.Current.CancellationToken);
        CancellationToken linked = linkedCts.Token;
#pragma warning disable xUnit1051 // Linked token includes TestContext.Current.CancellationToken for test cancellation
        Task delayTask = clock.DelayAsync(Duration.FromSeconds(10), linked);
#pragma warning restore xUnit1051
        delayTask.IsCompleted.Should().BeFalse();
#if NET
        await cts.CancelAsync();
#else
        cts.Cancel();
#endif
        Func<Task> act = async () => await delayTask;
        await act.Should().ThrowAsync<OperationCanceledException>();
    }
    //----------------------------------------------------------------------------

    #endregion DelayAsync (Duration) driven by virtual time

    #region Time cancellation (Duration) driven by virtual time

    /// <summary>
    ///   Verifies that GetTimeCancellationToken(Duration) expires when virtual time reaches the cancel time.
    /// </summary>
    [Fact]
    public void GetTimeCancellationToken_Duration_WhenAdvanceReachesCancelTime_TokenIsCancelled ()
    {
        Instant initial = Instant.FromUtc(2025, 1, 1, 0, 0, 0);
        IPrimeTestClock clock = new PrimeTestClock(initial);
        using TimeCancellationTokenSource tcs = clock.GetTimeCancellationToken(Duration.FromSeconds(2));
        tcs.IsCancellationRequested.Should().BeFalse();
        clock.Advance(Duration.FromSeconds(2));
        tcs.IsCancellationRequested.Should().BeTrue();
    }
    //----------------------------------------------------------------------------

    #endregion Time cancellation (Duration) driven by virtual time

    #region Interval timer driven by virtual time

    /// <summary>
    ///   Verifies that a one-shot interval timer fires when virtual time is advanced past the callback time.
    /// </summary>
    [Fact]
    public void RegisterTimer_OneShot_WhenAdvanceReachesCallbackTime_FiresOnce ()
    {
        Instant initial = Instant.FromUtc(2025, 1, 1, 0, 0, 0);
        IPrimeTestClock clock = new PrimeTestClock(initial);
        int fireCount = 0;
        using IClockIntervalTimer registration = clock.RegisterTimer(Duration.FromSeconds(2),
            () => fireCount++,
            TestContext.Current.CancellationToken);
        fireCount.Should().Be(0);
        clock.Advance(Duration.FromSeconds(1));
        fireCount.Should().Be(0);
        clock.Advance(Duration.FromSeconds(1));
        fireCount.Should().Be(1);
        clock.Advance(Duration.FromSeconds(10));
        fireCount.Should().Be(1);
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies that a repeating interval timer fires once per crossed due instant, including multiple ticks
    ///   inside a single <see cref="IPrimeTestClock.Advance(Duration)"/> when that advance spans several intervals.
    /// </summary>
    [Fact]
    public void RegisterTimer_Repeating_WhenAdvanceCoversMultipleIntervals_FiresMultipleTimes ()
    {
        Instant initial = Instant.FromUtc(2025, 1, 1, 0, 0, 0);
        IPrimeTestClock clock = new PrimeTestClock(initial);
        int fireCount = 0;
        using IClockIntervalTimer registration = clock.RegisterTimer(Duration.FromSeconds(1),
            () => fireCount++,
            TestContext.Current.CancellationToken,
            repeat: true);
        clock.Advance(Duration.FromSeconds(1));
        fireCount.Should().Be(1);
        clock.Advance(Duration.FromSeconds(1));
        fireCount.Should().Be(2);
        clock.Advance(Duration.FromSeconds(2));
        fireCount.Should().Be(4);
    }
    //----------------------------------------------------------------------------

    #endregion Interval timer driven by virtual time

    #region Day-time timer driven by virtual time

    /// <summary>
    ///   Verifies that a local time-of-day timer fires when virtual time reaches the target time of day, and that
    ///   <see cref="IClockDayTimeTimer.ElapsedTime"/> / <see cref="IClockDayTimeTimer.TimeUntilNextCallback"/> match the virtual schedule
    ///   before and after the first fire.
    /// </summary>
    [Fact]
    public void RegisterTimeOfDay_Local_WhenAdvanceReachesTargetTimeOfDay_Fires ()
    {
        // Use UTC zone so "local" and "UTC" coincide, and we can set instant to midnight then advance to 02:00
        Instant initial = Instant.FromUtc(2025, 1, 1, 0, 0, 0);
        IPrimeTestClock clock = new PrimeTestClock(initial, DateTimeZone.Utc);
        int fireCount = 0;
        LocalTime twoAm = new(2, 0, 0);
        using IClockDayTimeTimer registration = clock.RegisterTimeOfDay(twoAm,
            () => fireCount++,
            cancellationToken: TestContext.Current.CancellationToken);
        clock.Advance(Duration.FromHours(1));
        fireCount.Should().Be(0);
        registration.ElapsedTime.Should().Be(-1);
        long msUntilFirstFire = (long)Duration.FromHours(1).TotalMilliseconds;
        registration.TimeUntilNextCallback.Should()
            .BeInRange(msUntilFirstFire - VirtualClockTimerAssertionToleranceMilliseconds,
                msUntilFirstFire + VirtualClockTimerAssertionToleranceMilliseconds);
        clock.Advance(Duration.FromHours(1));
        fireCount.Should().Be(1);
        registration.ElapsedTime.Should().Be(0);
        long msUntilNextDay = (long)Duration.FromHours(24).TotalMilliseconds;
        registration.TimeUntilNextCallback.Should()
            .BeInRange(msUntilNextDay - VirtualClockTimerAssertionToleranceMilliseconds,
                msUntilNextDay + VirtualClockTimerAssertionToleranceMilliseconds);
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies that before the first fire, <see cref="IClockDayTimeTimer.ElapsedTime"/> is <c>-1</c> and
    ///   <see cref="IClockDayTimeTimer.TimeUntilNextCallback"/> matches the gap to the next local occurrence.
    /// </summary>
    [Fact]
    public void RegisterTimeOfDay_Local_BeforeDue_ElapsedTimeNegativeOne_TimeUntilNextMatchesVirtualSchedule ()
    {
        Instant initial = Instant.FromUtc(2025, 1, 1, 1, 0, 0);
        IPrimeTestClock clock = new PrimeTestClock(initial, DateTimeZone.Utc);
        LocalTime threeAm = new(3, 0, 0);
        using IClockDayTimeTimer timer = clock.RegisterTimeOfDay(threeAm, () => { },
            cancellationToken: TestContext.Current.CancellationToken);
        timer.ElapsedTime.Should().Be(-1);
        long expectedMs = (long)Duration.FromHours(2).TotalMilliseconds;
        timer.TimeUntilNextCallback.Should()
            .BeInRange(expectedMs - VirtualClockTimerAssertionToleranceMilliseconds,
                expectedMs + VirtualClockTimerAssertionToleranceMilliseconds);
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies that after a local day-time callback, advancing virtual time increases
    ///   <see cref="IClockDayTimeTimer.ElapsedTime"/> in line with the virtual local clock.
    /// </summary>
    [Fact]
    public void RegisterTimeOfDay_Local_AfterFire_Advance_ElapsedTimeTracksVirtualLocalClock ()
    {
        Instant initial = Instant.FromUtc(2025, 1, 1, 2, 59, 0);
        IPrimeTestClock clock = new PrimeTestClock(initial, DateTimeZone.Utc);
        LocalTime threeAm = new(3, 0, 0);
        int fireCount = 0;
        using IClockDayTimeTimer timer = clock.RegisterTimeOfDay(threeAm, () => fireCount++,
            cancellationToken: TestContext.Current.CancellationToken);
        clock.Advance(Duration.FromMinutes(1));
        fireCount.Should().Be(1);
        clock.Advance(Duration.FromHours(1));
        long expectedMs = (long)Duration.FromHours(1).TotalMilliseconds;
        timer.ElapsedTime.Should()
            .BeInRange(expectedMs - VirtualClockTimerAssertionToleranceMilliseconds,
                expectedMs + VirtualClockTimerAssertionToleranceMilliseconds);
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies that while a synchronous day-time callback runs,
    ///   <see cref="IClockTimer.CallbacksProcessing"/> is <c>true</c>.
    /// </summary>
    [Fact]
    public void RegisterTimeOfDay_Local_WhileCallbackRuns_CallbacksProcessingIsTrue ()
    {
        Instant initial = Instant.FromUtc(2025, 1, 1, 1, 0, 0);
        IPrimeTestClock clock = new PrimeTestClock(initial, DateTimeZone.Utc);
        LocalTime threeAm = new(3, 0, 0);
        bool? seenProcessing = null;
        IClockDayTimeTimer? registration = null;
        registration = clock.RegisterTimeOfDay(threeAm, () =>
            {
                seenProcessing = registration!.CallbacksProcessing;
            },
            cancellationToken: TestContext.Current.CancellationToken);
        using (registration)
        {
            clock.Advance(Duration.FromHours(2));
        }

        seenProcessing.Should().BeTrue();
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies that setting <see cref="IClockTimer.Enabled"/> to <c>false</c> after the first fire
    ///   prevents the next scheduled daily occurrence when virtual time advances.
    /// </summary>
    [Fact]
    public void RegisterTimeOfDay_Local_AfterFirstFire_SetEnabledFalse_AdvanceOneDay_NoSecondFire ()
    {
        Instant initial = Instant.FromUtc(2025, 1, 1, 0, 0, 0);
        IPrimeTestClock clock = new PrimeTestClock(initial, DateTimeZone.Utc);
        LocalTime twoAm = new(2, 0, 0);
        int fireCount = 0;
        using IClockDayTimeTimer timer = clock.RegisterTimeOfDay(twoAm, () => fireCount++,
            cancellationToken: TestContext.Current.CancellationToken);
        clock.Advance(Duration.FromHours(2));
        fireCount.Should().Be(1);
        timer.Enabled = false;
        timer.IsActive.Should().BeTrue("disabled timers are still active until cancelled or disposed");
        clock.Advance(Duration.FromDays(1));
        fireCount.Should().Be(1);
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies that disabling before the first fire, then enabling again, still yields a callback
    ///   when virtual time reaches the scheduled time of day.
    /// </summary>
    [Fact]
    public void RegisterTimeOfDay_Local_DisableBeforeFirstDue_EnableThenAdvance_FiresOnce ()
    {
        Instant initial = Instant.FromUtc(2025, 1, 1, 1, 0, 0);
        IPrimeTestClock clock = new PrimeTestClock(initial, DateTimeZone.Utc);
        LocalTime threeAm = new(3, 0, 0);
        int fireCount = 0;
        using IClockDayTimeTimer timer = clock.RegisterTimeOfDay(threeAm, () => fireCount++,
            cancellationToken: TestContext.Current.CancellationToken);
        timer.Enabled = false;
        clock.Advance(Duration.FromHours(1));
        fireCount.Should().Be(0);
        timer.Enabled = true;
        clock.Advance(Duration.FromHours(1));
        fireCount.Should().Be(1);
    }
    //----------------------------------------------------------------------------

#if NET
    /// <summary>
    ///   Verifies UTC time-of-day registrations use the virtual UTC clock for
    ///   <see cref="IClockTimer.TimeUntilNextCallback"/>.
    /// </summary>
    [Fact]
    public void RegisterTimeOfDay_Utc_BeforeDue_TimeUntilNextCallbackUsesUtcDayBoundary ()
    {
        Instant initial = Instant.FromUtc(2025, 6, 15, 10, 0, 0);
        IPrimeTestClock clock = new PrimeTestClock(initial, DateTimeZone.Utc);
        UtcTimeOfDay twoPmUtc = new UtcTimeOfDay(new TimeOnly(14, 0));
        using IClockDayTimeTimer timer = clock.RegisterTimeOfDay(twoPmUtc, () => { },
            cancellationToken: TestContext.Current.CancellationToken);
        timer.IsLocalTimeRepresentation.Should().BeFalse();
        long expectedMs = (long)Duration.FromHours(4).TotalMilliseconds;
        timer.TimeUntilNextCallback.Should()
            .BeInRange(expectedMs - VirtualClockTimerAssertionToleranceMilliseconds,
                expectedMs + VirtualClockTimerAssertionToleranceMilliseconds);
    }
#endif

    #endregion Day-time timer driven by virtual time
}
//################################################################################


