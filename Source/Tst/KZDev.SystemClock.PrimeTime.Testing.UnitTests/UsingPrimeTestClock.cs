// Copyright (c) Kevin Zehrer
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;

using AwesomeAssertions;

using KZDev.PrimeTime.Tests;
// ReSharper disable AccessToDisposedClosure
// ReSharper disable ChangeFieldTypeToSystemThreadingLock

namespace KZDev.SystemClock.PrimeTime.Testing.UnitTests;

//################################################################################
/// <summary>
///   Unit tests for <see cref="IPrimeTestClock"/> and <see cref="PrimeTestClock"/>.
/// </summary>
/// <remarks>
///   Covers <see cref="IPrimeTestClock.SetTime"/>, <see cref="IPrimeTestClock.Advance"/>,
///   <see cref="IPrimeTestClock.RunFor(System.TimeSpan, System.TimeSpan)"/>, start/stop, <see cref="IPrimeTestClock.ClockEvents"/>, and
///   virtual-time-driven sleep, delay, time cancellation, and timers.
/// </remarks>
[ExcludeFromCodeCoverage]
public class UsingPrimeTestClock : UnitTestBase
{
    //----------------------------------------------------------------------------
    /// <summary>
    ///   Default maximum real wall-clock duration for bounded waits in this fixture (for example
    ///   <see cref="ClockEventCapture{TEvent}.WaitAfterArmedForNextReceived"/> and sleep-completion polling).
    /// </summary>
    private static readonly TimeSpan BoundedRealTimeWaitTimeout = TimeSpan.FromSeconds(5);

    /// <summary>
    ///   Minimum virtual time per real second allowed by <see cref="PrimeTestClock.Start(TimeSpan?)"/>.
    /// </summary>
    private static readonly TimeSpan MinimumAllowedStartRunRate = TimeSpan.FromMilliseconds(100);

    /// <summary>
    ///   Run rate one millisecond below <see cref="MinimumAllowedStartRunRate"/> for out-of-range run-for tests.
    /// </summary>
    private static readonly TimeSpan RunForRateBelowMinimum = MinimumAllowedStartRunRate - TimeSpan.FromMilliseconds(1);

    /// <summary>
    ///   Brief real wall delay while the automatic runner is active: long enough for measurable virtual
    ///   advancement at 10 virtual seconds per real second without slowing the suite.
    /// </summary>
    private static readonly TimeSpan ClockRunningProjectionTestWallDelay = TimeSpan.FromMilliseconds(250);

    /// <summary>
    ///   Virtual-time assertion margin for runner projection tests: the run anchor starts inside
    ///   <see cref="PrimeTestClock.Start(TimeSpan?)"/> before the wall <see cref="Stopwatch"/> begins, separate
    ///   stopwatch instances, imprecise <see cref="Task.Delay(TimeSpan, CancellationToken)"/>, and persist-on-read
    ///   work while the runner is active under parallel scheduling.
    /// </summary>
    private static readonly TimeSpan ClockRunningProjectionVirtualTolerance = TimeSpan.FromMilliseconds(150);

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
    ///   Real wall delay for virtual-minute heartbeat at 10 virtual seconds per real second.
    /// </summary>
    private static readonly TimeSpan ClockRunningHeartbeatTestWallDelay = TimeSpan.FromSeconds(8);

    /// <summary>
    ///   Virtual-time tolerance when asserting heartbeat <see cref="IPrimeTestClock.ClockEvents"/> times.
    /// </summary>
    private static readonly TimeSpan ClockRunningHeartbeatVirtualTolerance = TimeSpan.FromSeconds(2);

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
    ///   Tolerance when asserting a reset heartbeat <see cref="IPrimeTestClock.ClockEvents"/> time after a substantive
    ///   timer at 45 virtual seconds.
    /// </summary>
    private static readonly TimeSpan ClockRunningResetHeartbeatVirtualTolerance = TimeSpan.FromSeconds(5);
    //----------------------------------------------------------------------------

    #region Constructors/Finalizers

    //----------------------------------------------------------------------------
    /// <summary>
    ///   Initializes a new instance of the <see cref="UsingPrimeTestClock"/> class.
    /// </summary>
    /// <param name="xUnitTestOutputHelper">
    ///   The xUnit test output helper that can be used to output test messages.
    /// </param>
    public UsingPrimeTestClock (ITestOutputHelper xUnitTestOutputHelper)
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
    ///   Verifies that <see cref="PrimeTestClock"/> default constructor sets a non-default UtcNowDateTimeOffset.
    /// </summary>
    [Fact]
    public void PrimeTestClock_DefaultConstructor_SetsUtcNow ()
    {
        IPrimeTestClock clock = new PrimeTestClock();
        clock.UtcNowDateTimeOffset.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(2));
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies that <see cref="PrimeTestClock"/> with initial time returns that time from UtcNowDateTimeOffset.
    /// </summary>
    [Fact]
    public void PrimeTestClock_WithInitialTime_ReturnsThatTimeFromUtcNow ()
    {
        DateTimeOffset initial = new(2020, 6, 15, 12, 0, 0, TimeSpan.Zero);
        IPrimeTestClock clock = new PrimeTestClock(initial);
        clock.UtcNowDateTimeOffset.Should().Be(initial);
    }
    //----------------------------------------------------------------------------

    #endregion Contract and construction

    #region SetTime and Advance

    /// <summary>
    ///   Verifies that <see cref="IPrimeTestClock.SetTime"/> updates UtcNowDateTimeOffset and related "now" members.
    /// </summary>
    [Fact]
    public void SetTime_WithUtcTime_UpdatesUtcNowAndRelatedMembers ()
    {
        DateTimeOffset setTime = new(2025, 1, 10, 14, 30, 0, TimeSpan.Zero);
        IPrimeTestClock clock = new PrimeTestClock();
        clock.SetTime(setTime);
        clock.UtcNowDateTimeOffset.Should().Be(setTime);
        clock.UtcNowDateTime.Should().Be(setTime.UtcDateTime);
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies that <see cref="IPrimeTestClock.Advance"/> adds duration to virtual time.
    /// </summary>
    [Fact]
    public void Advance_WithPositiveDuration_AddsToVirtualTime ()
    {
        DateTimeOffset initial = new(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);
        IPrimeTestClock clock = new PrimeTestClock(initial);
        clock.Advance(TimeSpan.FromHours(2));
        clock.UtcNowDateTimeOffset.Should().Be(initial + TimeSpan.FromHours(2));
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies that <see cref="IPrimeTestClock.Advance"/> with zero does not change time.
    /// </summary>
    [Fact]
    public void Advance_WithZero_LeavesTimeUnchanged ()
    {
        DateTimeOffset initial = new(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);
        IPrimeTestClock clock = new PrimeTestClock(initial);
        clock.Advance(TimeSpan.Zero);
        clock.UtcNowDateTimeOffset.Should().Be(initial);
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies that <see cref="IPrimeTestClock.Advance(System.TimeSpan)"/> with negative duration does not move
    ///   virtual time backward (implementation treats it as zero).
    /// </summary>
    [Fact]
    public void Advance_WithNegativeDuration_LeavesTimeUnchanged ()
    {
        DateTimeOffset initial = new(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);
        IPrimeTestClock clock = new PrimeTestClock(initial);
        clock.Advance(TimeSpan.FromHours(-1));
        clock.UtcNowDateTimeOffset.Should().Be(initial);
    }
    //----------------------------------------------------------------------------

    #region Advance — forward virtual march

    /// <summary>
    ///   Verifies that a single <see cref="IPrimeTestClock.Advance(System.TimeSpan)"/> that spans multiple interval
    ///   ticks invokes each callback with <see cref="IPrimeTestClock.UtcNowDateTimeOffset"/> at that tick&apos;s firing instant.
    /// </summary>
    [Fact]
    public void Advance_OverMultipleIntervalTicks_CallbackSeesUtcNowAtEachFiringInstant ()
    {
        DateTimeOffset initial = new(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);
        IPrimeTestClock clock = new PrimeTestClock(initial);
        List<DateTimeOffset> observedNow = [];
        using (clock.RegisterTimer(TimeSpan.FromSeconds(10),
                   _ => observedNow.Add(clock.UtcNowDateTimeOffset),
                   TestContext.Current.CancellationToken,
                   repeat: true))
        {
            clock.Advance(TimeSpan.FromSeconds(30));
        }

        observedNow.Should().HaveCount(3);
        observedNow[0].Should().Be(initial + TimeSpan.FromSeconds(10));
        observedNow[1].Should().Be(initial + TimeSpan.FromSeconds(20));
        observedNow[2].Should().Be(initial + TimeSpan.FromSeconds(30));
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies that <see cref="IPrimeTestClock.ClockEvents"/> is raised once per distinct virtual instant crossed
    ///   during one <see cref="IPrimeTestClock.Advance(System.TimeSpan)"/> when multiple interval ticks occur.
    /// </summary>
    [Fact]
    public void Advance_SpanningMultipleDueInstants_RaisesClockEventsOncePerDistinctInstant ()
    {
        DateTimeOffset initial = new(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);
        IPrimeTestClock clock = new PrimeTestClock(initial);
        int eventCount = 0;
        clock.ClockEvents += (_, _) => eventCount++;
        using (clock.RegisterTimer(TimeSpan.FromSeconds(10),
                   _ => { },
                   TestContext.Current.CancellationToken,
                   repeat: true))
        {
            clock.Advance(TimeSpan.FromSeconds(30));
        }

        eventCount.Should().Be(3);
    }
    //----------------------------------------------------------------------------

    #endregion Advance — forward virtual march

    #region SetTime — forward virtual march

    /// <summary>
    ///   Verifies that a single forward <see cref="IPrimeTestClock.SetTime"/> that lands after multiple interval
    ///   ticks invokes each callback with <see cref="IPrimeTestClock.UtcNowDateTimeOffset"/> at that tick&apos;s firing instant.
    /// </summary>
    [Fact]
    public void SetTime_Forward_OverMultipleIntervalTicks_CallbackSeesUtcNowAtEachFiringInstant ()
    {
        DateTimeOffset initial = new(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);
        DateTimeOffset targetUtc = initial + TimeSpan.FromSeconds(30);
        IPrimeTestClock clock = new PrimeTestClock(initial);
        List<DateTimeOffset> observedNow = [];
        using (clock.RegisterTimer(TimeSpan.FromSeconds(10),
                   _ => observedNow.Add(clock.UtcNowDateTimeOffset),
                   TestContext.Current.CancellationToken,
                   repeat: true))
        {
            clock.SetTime(targetUtc);
        }

        observedNow.Should().HaveCount(3);
        observedNow[0].Should().Be(initial + TimeSpan.FromSeconds(10));
        observedNow[1].Should().Be(initial + TimeSpan.FromSeconds(20));
        observedNow[2].Should().Be(initial + TimeSpan.FromSeconds(30));
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies that <see cref="IPrimeTestClock.ClockEvents"/> is raised once per distinct virtual instant crossed
    ///   during one forward <see cref="IPrimeTestClock.SetTime"/> when multiple interval ticks occur.
    /// </summary>
    [Fact]
    public void SetTime_Forward_SpanningMultipleDueInstants_RaisesClockEventsOncePerDistinctInstant ()
    {
        DateTimeOffset initial = new(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);
        DateTimeOffset targetUtc = initial + TimeSpan.FromSeconds(30);
        IPrimeTestClock clock = new PrimeTestClock(initial);
        int eventCount = 0;
        clock.ClockEvents += (_, _) => eventCount++;
        using (clock.RegisterTimer(TimeSpan.FromSeconds(10),
                   _ => { },
                   TestContext.Current.CancellationToken,
                   repeat: true))
        {
            clock.SetTime(targetUtc);
        }

        eventCount.Should().Be(3);
    }
    //----------------------------------------------------------------------------

    #endregion SetTime — forward virtual march

    #region SetTime — backward virtual time rules

    /// <summary>
    ///   Verifies that <see cref="IPrimeTestClock.SetTime"/> to a strictly earlier virtual instant throws while the
    ///   clock is running.
    /// </summary>
    [Fact]
    public void ClockRunning_SetTimeToPastValue_ThrowsInvalidOperationException ()
    {
        DateTimeOffset initial = new(2025, 1, 1, 12, 0, 0, TimeSpan.Zero);
        DateTimeOffset earlier = initial - TimeSpan.FromHours(1);
        IPrimeTestClock clock = new PrimeTestClock(initial);
        clock.Start(null);
        Action act = () => clock.SetTime(earlier);
        act.Should().Throw<InvalidOperationException>()
            .WithMessage(ThrowHelperContractMessages.InvalidOperation_VirtualTimeBackwardWhileRunning);
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
        DateTimeOffset initial = new(2025, 1, 1, 12, 0, 0, TimeSpan.Zero);
        DateTimeOffset earlier = initial - TimeSpan.FromHours(1);
        IPrimeTestClock clock = new PrimeTestClock(initial);
        using (clock.RegisterTimer(TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1), _ => { },
                   TestContext.Current.CancellationToken))
        {
            Action act = () => clock.SetTime(earlier);
            act.Should().Throw<InvalidOperationException>()
                .WithMessage(ThrowHelperContractMessages.InvalidOperation_VirtualTimeBackwardWhileIntervalTimerActive);
        }
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies that a strictly backward <see cref="IPrimeTestClock.SetTime"/> succeeds when the clock is stopped
    ///   and the only interval registration has been disposed (no longer active).
    /// </summary>
    [Fact]
    public void ClockStopped_DisposedIntervalTimer_SetTimeBackward_UpdatesUtcNow ()
    {
        DateTimeOffset initial = new(2025, 1, 1, 12, 0, 0, TimeSpan.Zero);
        DateTimeOffset earlier = initial - TimeSpan.FromHours(1);
        IPrimeTestClock clock = new PrimeTestClock(initial);
        IClockIntervalTimer timer = clock.RegisterTimer(TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1), _ => { },
            TestContext.Current.CancellationToken);
        timer.Dispose();
        clock.SetTime(earlier);
        clock.UtcNowDateTimeOffset.Should().Be(earlier);
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies that a permitted strictly backward <see cref="IPrimeTestClock.SetTime"/> raises
    ///   <see cref="IPrimeTestClock.ClockEvents"/> exactly once for the new instant.
    /// </summary>
    [Fact]
    public void ClockStopped_PermittedBackwardSetTime_RaisesClockEventsOnce ()
    {
        DateTimeOffset initial = new(2025, 1, 1, 12, 0, 0, TimeSpan.Zero);
        DateTimeOffset earlier = initial - TimeSpan.FromHours(1);
        IPrimeTestClock clock = new PrimeTestClock(initial);
        int eventCount = 0;
        clock.ClockEvents += (_, _) => eventCount++;
        clock.SetTime(earlier);
        eventCount.Should().Be(1);
    }
    //----------------------------------------------------------------------------

#if NET

    /// <summary>
    ///   Verifies that after a forward march fires a UTC day-time timer and a permitted backward jump,
    ///   <see cref="IClockTimer.TimeUntilNextCallback"/> reflects the next same-calendar occurrence (recomputed
    ///   <c>NextDueUtc</c>), not the post-fire next day.
    /// </summary>
    [Fact]
    public void ClockStopped_OnlyUtcDayTimeTimer_AfterForwardFireAndBackwardJump_TimeUntilNextCallback_MatchesNextSameDayOccurrence ()
    {
        DateTimeOffset morning = new(2025, 1, 1, 10, 0, 0, TimeSpan.Zero);
        DateTimeOffset evening = new(2025, 1, 1, 18, 0, 0, TimeSpan.Zero);
        IPrimeTestClock clock = new PrimeTestClock(morning);
        using IClockDayTimeTimer timer = clock.RegisterTimeOfDay(new UtcTimeOfDay(new TimeOnly(12, 0)),
            _ => { },
            TestContext.Current.CancellationToken);
        clock.SetTime(evening);
        long msAfterForward = timer.TimeUntilNextCallback;
        msAfterForward.Should().Be((long)TimeSpan.FromHours(18).TotalMilliseconds);
        clock.SetTime(morning);
        timer.TimeUntilNextCallback.Should().Be((long)TimeSpan.FromHours(2).TotalMilliseconds);
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies that a UTC day-time timer fires once per discrete local occurrence when virtual time is moved
    ///   backward (permitted) between two forward marches across the same nominal fire instant.
    /// </summary>
    [Fact]
    public void ClockStopped_OnlyUtcDayTimeTimer_PermittedBackwardThenForwardAgain_FiresOncePerCrossing ()
    {
        DateTimeOffset morning = new(2025, 1, 1, 10, 0, 0, TimeSpan.Zero);
        DateTimeOffset evening = new(2025, 1, 1, 18, 0, 0, TimeSpan.Zero);
        IPrimeTestClock clock = new PrimeTestClock(morning);
        int fireCount = 0;
        using (clock.RegisterTimeOfDay(new UtcTimeOfDay(new TimeOnly(12, 0)),
                   _ => fireCount++,
                   TestContext.Current.CancellationToken))
        {
            clock.SetTime(evening);
            fireCount.Should().Be(1);
            clock.SetTime(morning);
            fireCount.Should().Be(1);
            clock.SetTime(evening);
            fireCount.Should().Be(2);
        }
    }
    //----------------------------------------------------------------------------

#endif

    #endregion SetTime — backward virtual time rules

    #endregion SetTime and Advance

    #region RunFor

    /// <summary>
    ///   Verifies that <see cref="IPrimeTestClock.RunFor(System.TimeSpan, System.TimeSpan)"/> advances virtual time by the given duration when the
    ///   bounded run completes.
    /// </summary>
    [Fact]
    public void RunFor_WithDuration_AdvancesVirtualTimeByDuration ()
    {
        DateTimeOffset initial = new(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);
        TimeSpan runDuration = TimeSpan.FromMinutes(30);
        IPrimeTestClock clock = new PrimeTestClock(initial);
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        clock.RunFor(runDuration, RunForTestFastPerSecondRate).Should().BeTrue();
        WaitUntilClockStopped(() => clock.IsRunning, BoundedRealTimeWaitTimeout, cancellationToken);
        clock.UtcNowDateTimeOffset.Should().Be(initial + runDuration);
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies that <see cref="IPrimeTestClock.RunFor(System.TimeSpan, System.TimeSpan)"/> returns
    ///   <c>false</c> when the clock is already running.
    /// </summary>
    [Fact]
    public void RunFor_WhenAlreadyRunning_ReturnsFalse ()
    {
        IPrimeTestClock clock = new PrimeTestClock();
        clock.Start(RunForTestFastPerSecondRate);
        clock.RunFor(TimeSpan.FromMinutes(1), RunForTestFastPerSecondRate).Should().BeFalse();
        clock.Stop().Should().BeTrue();
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies that zero-duration <see cref="IPrimeTestClock.RunFor(System.TimeSpan, System.TimeSpan)"/> raises started and stopped lifecycle
    ///   events and leaves the clock not running.
    /// </summary>
    [Fact]
    public void RunFor_WithZeroDuration_RaisesStartedAndStoppedAndNotRunning ()
    {
        IPrimeTestClock clock = new PrimeTestClock();
        using ClockEventTypeCollector eventTypes = new(clock);
        clock.RunFor(TimeSpan.Zero).Should().BeTrue();
        clock.IsRunning.Should().BeFalse();
        eventTypes.Snapshot().Should().Equal(
            PrimeTestClockEventType.ClockStarted,
            PrimeTestClockEventType.ClockStopped);
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies that <see cref="IPrimeTestClock.RunFor(System.TimeSpan, System.TimeSpan)"/> rejects an
    ///   out-of-range per-second rate.
    /// </summary>
    [Fact]
    public void RunFor_WithInvalidPerSecondRate_ThrowsArgumentOutOfRangeException ()
    {
        IPrimeTestClock clock = new PrimeTestClock();
        Action act = () => clock.RunFor(TimeSpan.FromMinutes(1), RunForRateBelowMinimum);
        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("perSecondRate");
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies that negative <see cref="IPrimeTestClock.RunFor(TimeSpan, TimeSpan)"/> duration is treated like zero duration.
    /// </summary>
    [Fact]
    public void RunFor_WithNegativeDuration_TreatedAsZero ()
    {
        DateTimeOffset initial = new(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);
        IPrimeTestClock clock = new PrimeTestClock(initial);
        using ClockEventTypeCollector eventTypes = new(clock);
        clock.RunFor(TimeSpan.FromMinutes(-5)).Should().BeTrue();
        clock.IsRunning.Should().BeFalse();
        clock.UtcNowDateTimeOffset.Should().Be(initial);
        eventTypes.Snapshot().Should().Equal(
            PrimeTestClockEventType.ClockStarted,
            PrimeTestClockEventType.ClockStopped);
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies that a non-zero <see cref="IPrimeTestClock.RunFor(TimeSpan, TimeSpan)"/> raises
    ///   <see cref="PrimeTestClockEventType.ClockStarted"/> before
    ///   <see cref="PrimeTestClockEventType.ClockStopped"/> when the run completes.
    /// </summary>
    [Fact]
    public void RunFor_WhenStarted_RaisesClockStartedThenClockStopped ()
    {
        IPrimeTestClock clock = new PrimeTestClock();
        using ClockEventTypeCollector eventTypes = new(clock);
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        clock.RunFor(TimeSpan.FromSeconds(10), RunForTestFastPerSecondRate).Should().BeTrue();
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
    ///   Verifies that when a bounded <see cref="IPrimeTestClock.RunFor(TimeSpan, TimeSpan)"/> completes naturally on the automatic
    ///   runner thread (without external <see cref="IPrimeTestClock.Advance"/> or <see cref="IPrimeTestClock.SetTime"/>
    ///   mutations), the clock stops cleanly and raises exactly one
    ///   <see cref="PrimeTestClockEventType.ClockStopped"/> at the stop horizon.
    /// </summary>
    [Fact]
    public void RunFor_WhenRunnerLoopReachesStopHorizon_CompletesWithSingleClockStoppedAtHorizon ()
    {
        DateTimeOffset initial = new(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);
        TimeSpan runForDuration = TimeSpan.FromSeconds(10);
        DateTimeOffset runForStopUtc = initial + runForDuration;
        IPrimeTestClock clock = new PrimeTestClock(initial);
        using ClockEventCollector collector = new(clock);
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        clock.RunFor(runForDuration, RunForTestFastPerSecondRate).Should().BeTrue();
        PrimeTestClockBoundedRunAssertionHelpers.AssertBoundedRunCompletedWithSingleClockStoppedAtHorizon(
            clock,
            collector,
            runForStopUtc,
            runForStopUtc,
            RunForTestWaitTimeout,
            cancellationToken,
            "Timed out waiting for ClockStopped after runner-loop bounded completion.");
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies that completing an active bounded <see cref="IPrimeTestClock.RunFor(TimeSpan, TimeSpan)"/> via the external march path
    ///   while executing on the automatic runner thread (for example <see cref="IPrimeTestClock.Advance"/> from a
    ///   <see cref="IPrimeTestClock.ClockEvents"/> handler) does not self-join deadlock and raises exactly one
    ///   <see cref="PrimeTestClockEventType.ClockStopped"/> at the stop horizon.
    /// </summary>
    [Fact]
    public void RunFor_WhenAdvanceFromClockEventsOnRunnerThread_CompletesWithSingleClockStoppedAtHorizon ()
    {
        DateTimeOffset initial = new(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);
        TimeSpan runForDuration = TimeSpan.FromMinutes(10);
        TimeSpan advanceDuration = TimeSpan.FromMinutes(30);
        DateTimeOffset runForStopUtc = initial + runForDuration;
        IPrimeTestClock clock = new PrimeTestClock(initial);
        using ClockEventCollector collector = new(clock);
        object advanceFromRunnerCallbackSync = new();
        bool advancedFromRunnerCallback = false;
        DateTimeOffset advanceTargetUtc = initial;
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
                advanceTargetUtc = clock.UtcNowDateTimeOffset + advanceDuration;
            }

            clock.Advance(advanceDuration);
        };
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        clock.RunFor(runForDuration, RunForTestFastPerSecondRate).Should().BeTrue();
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
        DateTimeOffset expectedAdvanceTargetUtc;
        lock (advanceFromRunnerCallbackSync)
        {
            expectedAdvanceTargetUtc = advanceTargetUtc;
        }

        PrimeTestClockBoundedRunAssertionHelpers.AssertBoundedRunCompletedWithSingleClockStoppedAtHorizon(
            clock,
            collector,
            expectedAdvanceTargetUtc,
            runForStopUtc,
            RunForTestWaitTimeout,
            cancellationToken,
            "Timed out waiting for ClockStopped after bounded completion from a runner-thread ClockEvents callback.");
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies that a second <see cref="IPrimeTestClock.RunFor(TimeSpan, TimeSpan)"/> while the first bounded run is still active
    ///   returns <c>false</c> and does not complete the first run at the wrong horizon when the first run is
    ///   finished via <see cref="IPrimeTestClock.Advance"/>.
    /// </summary>
    [Fact]
    public void RunFor_WhenSecondRunForWhileFirstActive_ReturnsFalseAndCompletesFirstRunAtHorizon ()
    {
        DateTimeOffset initial = new(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);
        TimeSpan firstRunForDuration = TimeSpan.FromMinutes(10);
        TimeSpan secondRunForDuration = TimeSpan.FromMinutes(5);
        TimeSpan advancePastHorizon = TimeSpan.FromMinutes(30);
        DateTimeOffset firstRunForStopUtc = initial + firstRunForDuration;
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

            clock.RunFor(secondRunForDuration, RunForTestFastPerSecondRate).Should().BeFalse();
        };
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        clock.RunFor(firstRunForDuration, RunForTestFastPerSecondRate).Should().BeTrue();
        clock.RunFor(secondRunForDuration, RunForTestFastPerSecondRate).Should().BeFalse();
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
        DateTimeOffset advanceTargetUtc = clock.UtcNowDateTimeOffset;
        PrimeTestClockBoundedRunAssertionHelpers.AssertBoundedRunCompletedWithSingleClockStoppedAtHorizon(
            clock,
            collector,
            advanceTargetUtc,
            firstRunForStopUtc,
            RunForTestWaitTimeout,
            cancellationToken);
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies that two sequential bounded <see cref="IPrimeTestClock.RunFor(TimeSpan, TimeSpan)"/> runs each raise exactly one
    ///   <see cref="PrimeTestClockEventType.ClockStopped"/> at that run's stop horizon and do not complete the other
    ///   run's generation.
    /// </summary>
    [Fact]
    public void RunFor_WhenTwoSequentialBoundedRuns_EachRaisesClockStoppedAtOwnHorizon ()
    {
        DateTimeOffset initial = new(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);
        TimeSpan firstRunForDuration = TimeSpan.FromMinutes(10);
        TimeSpan secondRunForDuration = TimeSpan.FromMinutes(5);
        TimeSpan advancePastHorizon = TimeSpan.FromMinutes(30);
        DateTimeOffset firstRunForStopUtc = initial + firstRunForDuration;
        IPrimeTestClock clock = new PrimeTestClock(initial);
        using ClockEventCollector collector = new(clock);
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        clock.RunFor(firstRunForDuration, RunForTestFastPerSecondRate).Should().BeTrue();
        clock.Advance(advancePastHorizon);
        DateTimeOffset afterFirstRunCommittedUtc = clock.UtcNowDateTimeOffset;
        PrimeTestClockBoundedRunAssertionHelpers.AssertBoundedRunCompletedWithSingleClockStoppedAtHorizon(
            clock,
            collector,
            afterFirstRunCommittedUtc,
            firstRunForStopUtc,
            RunForTestWaitTimeout,
            cancellationToken);
        DateTimeOffset secondRunStartUtc = clock.UtcNowDateTimeOffset;
        DateTimeOffset secondRunForStopUtc = secondRunStartUtc + secondRunForDuration;
        clock.RunFor(secondRunForDuration, RunForTestFastPerSecondRate).Should().BeTrue();
        clock.Advance(advancePastHorizon);
        DateTimeOffset afterSecondRunCommittedUtc = clock.UtcNowDateTimeOffset;
        WaitUntilCondition(
            () => collector.Snapshot().Count(e => e.EventType == PrimeTestClockEventType.ClockStopped) == 2,
            RunForTestWaitTimeout,
            cancellationToken,
            "Timed out waiting for ClockStopped from the second bounded RunFor.");
        clock.IsRunning.Should().BeFalse();
        clock.UtcNowDateTimeOffset.Should().Be(afterSecondRunCommittedUtc);
        List<PrimeTestClockEvent> snapshot = collector.Snapshot();
        snapshot.Count(e => e.EventType == PrimeTestClockEventType.ClockStopped).Should().Be(2);
        PrimeTestClockStoppedEvent[] stoppedEvents = [.. snapshot.OfType<PrimeTestClockStoppedEvent>()];
        stoppedEvents[0].ClockTime.Should().Be(firstRunForStopUtc);
        stoppedEvents[1].ClockTime.Should().Be(secondRunForStopUtc);
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies that when two concurrent <see cref="IPrimeTestClock.Advance"/> calls both cross an active
    ///   <see cref="IPrimeTestClock.RunFor(TimeSpan, TimeSpan)"/> stop horizon, bounded completion is committed exactly once, one competing
    ///   completion path safely returns without duplicating <see cref="PrimeTestClockEventType.ClockStopped"/>, and both
    ///   callers make forward progress.
    /// </summary>
    [Fact]
    public void RunFor_WhenConcurrentAdvanceCallsRaceAcrossStopHorizon_RaisesSingleClockStoppedAndMakesProgress ()
    {
        DateTimeOffset initial = new(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);
        TimeSpan runForDuration = TimeSpan.FromHours(6);
        TimeSpan advanceDuration = TimeSpan.FromHours(12);
        DateTimeOffset runForStopUtc = initial + runForDuration;
        IPrimeTestClock clock = new PrimeTestClock(initial);
        using ClockEventCollector collector = new(clock);
        using ManualResetEventSlim releaseConcurrentAdvances = new(false);
        int readyAdvanceCallers = 0;
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        clock.RunFor(runForDuration, RunForTestFastPerSecondRate).Should().BeTrue();
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
        clock.UtcNowDateTimeOffset.Should().BeAfter(initial);
        List<PrimeTestClockEvent> snapshot = collector.Snapshot();
        snapshot.Count(e => e.EventType == PrimeTestClockEventType.ClockStopped).Should().Be(1);
        PrimeTestClockStoppedEvent stoppedEvent = snapshot.OfType<PrimeTestClockStoppedEvent>().Should().ContainSingle().Subject;
        stoppedEvent.ClockTime.Should().Be(runForStopUtc);
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies that <see cref="IPrimeTestClock.Advance"/> past an active <see cref="IPrimeTestClock.RunFor(TimeSpan, TimeSpan)"/>
    ///   stop horizon raises <see cref="PrimeTestClockEventType.ClockStopped"/> at the horizon, leaves the clock
    ///   stopped, and commits the <see cref="IPrimeTestClock.Advance"/> target instant.
    /// </summary>
    [Fact]
    public void RunFor_WhenAdvancePassesStopHorizon_StopsAtHorizonAndReachesAdvanceTarget ()
    {
        DateTimeOffset initial = new(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);
        TimeSpan runForDuration = TimeSpan.FromMinutes(10);
        TimeSpan advanceDuration = TimeSpan.FromMinutes(30);
        DateTimeOffset runForStopUtc = initial + runForDuration;
        DateTimeOffset advanceTargetUtc = initial + advanceDuration;
        IPrimeTestClock clock = new PrimeTestClock(initial);
        using ClockEventCollector collector = new(clock);
        clock.RunFor(runForDuration, RunForTestFastPerSecondRate).Should().BeTrue();
        clock.Advance(advanceDuration);
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        PrimeTestClockBoundedRunAssertionHelpers.AssertBoundedRunCompletedWithSingleClockStoppedAtHorizon(
            clock,
            collector,
            advanceTargetUtc,
            runForStopUtc,
            RunForTestWaitTimeout,
            cancellationToken);
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies that <see cref="IPrimeTestClock.Advance"/> to exactly the active
    ///   <see cref="IPrimeTestClock.RunFor(TimeSpan, TimeSpan)"/> stop horizon completes the bounded run at that instant.
    /// </summary>
    [Fact]
    public void RunFor_WhenAdvanceReachesStopHorizonExactly_StopsAtHorizonAndNotRunning ()
    {
        DateTimeOffset initial = new(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);
        TimeSpan runForDuration = TimeSpan.FromMinutes(10);
        DateTimeOffset runForStopUtc = initial + runForDuration;
        IPrimeTestClock clock = new PrimeTestClock(initial);
        using ClockEventCollector collector = new(clock);
        clock.RunFor(runForDuration, RunForTestFastPerSecondRate).Should().BeTrue();
        clock.Advance(runForDuration);
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        PrimeTestClockBoundedRunAssertionHelpers.AssertBoundedRunCompletedWithSingleClockStoppedAtHorizon(
            clock,
            collector,
            runForStopUtc,
            runForStopUtc,
            RunForTestWaitTimeout,
            cancellationToken);
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies that forward <see cref="IPrimeTestClock.SetTime(DateTimeOffset)"/> past an active
    ///   <see cref="IPrimeTestClock.RunFor(TimeSpan, TimeSpan)"/> stop horizon raises
    ///   <see cref="PrimeTestClockEventType.ClockStopped"/> at the horizon, leaves the clock stopped, and commits the
    ///   requested instant.
    /// </summary>
    [Fact]
    public void RunFor_WhenSetTimePassesStopHorizon_StopsAtHorizonAndReachesSetTimeTarget ()
    {
        DateTimeOffset initial = new(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);
        TimeSpan runForDuration = TimeSpan.FromMinutes(10);
        DateTimeOffset runForStopUtc = initial + runForDuration;
        DateTimeOffset setTimeTarget = initial + TimeSpan.FromMinutes(45);
        IPrimeTestClock clock = new PrimeTestClock(initial);
        using ClockEventCollector collector = new(clock);
        clock.RunFor(runForDuration, RunForTestFastPerSecondRate).Should().BeTrue();
        clock.SetTime(setTimeTarget);
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        PrimeTestClockBoundedRunAssertionHelpers.AssertBoundedRunCompletedWithSingleClockStoppedAtHorizon(
            clock,
            collector,
            setTimeTarget,
            runForStopUtc,
            RunForTestWaitTimeout,
            cancellationToken);
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies that forward <see cref="IPrimeTestClock.SetTime(DateTimeOffset)"/> to exactly the active
    ///   <see cref="IPrimeTestClock.RunFor(TimeSpan, TimeSpan)"/> stop horizon completes the bounded run at that instant.
    /// </summary>
    [Fact]
    public void RunFor_WhenSetTimeReachesStopHorizonExactly_StopsAtHorizonAndNotRunning ()
    {
        DateTimeOffset initial = new(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);
        TimeSpan runForDuration = TimeSpan.FromMinutes(10);
        DateTimeOffset runForStopUtc = initial + runForDuration;
        IPrimeTestClock clock = new PrimeTestClock(initial);
        using ClockEventCollector collector = new(clock);
        clock.RunFor(runForDuration, RunForTestFastPerSecondRate).Should().BeTrue();
        clock.SetTime(runForStopUtc);
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        PrimeTestClockBoundedRunAssertionHelpers.AssertBoundedRunCompletedWithSingleClockStoppedAtHorizon(
            clock,
            collector,
            runForStopUtc,
            runForStopUtc,
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
    ///   Verifies that <see cref="IPrimeTestClock.Start"/> and <see cref="IPrimeTestClock.Stop"/> set
    ///   IsRunning and that Stop returns true when was running.
    /// </summary>
    [Fact]
    public void Start_ThenStop_SetsIsRunningAndStopReturnsTrue ()
    {
        IPrimeTestClock clock = new PrimeTestClock(new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero));
        clock.Start(TimeSpan.FromSeconds(1));
        clock.IsRunning.Should().BeTrue();
        bool stopped = clock.Stop();
        stopped.Should().BeTrue();
        clock.IsRunning.Should().BeFalse();
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies that <see cref="IPrimeTestClock.Start(System.TimeSpan?)"/> with <c>null</c> starts at the default
    ///   1:1 virtual-to-real rate.
    /// </summary>
    [Fact]
    public void Start_WithNullRate_StartsSuccessfully ()
    {
        IPrimeTestClock clock = new PrimeTestClock(new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero));
        clock.Start(null);
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
        IPrimeTestClock clock = new PrimeTestClock(new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero));
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
        IPrimeTestClock clock = new PrimeTestClock(new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero));
        clock.Start(TimeSpan.FromHours(1));
        clock.IsRunning.Should().BeTrue();
        clock.Stop().Should().BeTrue();
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies that a run rate one tick below the minimum throws and does not start the clock.
    /// </summary>
    [Fact]
    public void Start_WithRateOneTickBelowMinimum_ThrowsArgumentOutOfRangeException ()
    {
        IPrimeTestClock clock = new PrimeTestClock();
        TimeSpan belowMinimum = MinimumAllowedStartRunRate - TimeSpan.FromTicks(1);
        Action act = () => clock.Start(belowMinimum);
        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("perSecondRate")
            .WithMessage($"{ThrowHelperContractMessages.ArgumentOutOfRange_StartRunRateOutOfRange}*");
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
        TimeSpan aboveMaximum = TimeSpan.FromHours(1) + TimeSpan.FromTicks(1);
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
        Action act = () => clock.Start(TimeSpan.Zero);
        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("perSecondRate");
        clock.IsRunning.Should().BeFalse();
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies that while the automatic runner is active, <see cref="IPrimeClock.UtcNowDateTimeOffset"/> reflects
    ///   linear projection of virtual time from the committed instant and monotonic anchor between explicit commits.
    /// </summary>
    /// <remarks>
    ///   <para>
    ///     Expected virtual time is computed from a wall <see cref="Stopwatch"/> around the same real delay, using the
    ///     same virtual-per-real-second scaling as the clock (not a fixed nominal delay), so slow or parallel test
    ///     scheduling does not skew the assertion. A small fixed virtual tolerance remains for residual skew: the
    ///     clock&apos;s anchor stopwatch is a different instance than the test stopwatch and starts before the wall
    ///     timer begins, <see cref="Task.Delay(TimeSpan, CancellationToken)"/> does not guarantee exact wall duration, and persist-on-read on
    ///     <see cref="IPrimeClock.UtcNowDateTimeOffset"/> is measured before the wall timer stops so post-read
    ///     scheduling does not inflate the observed instant.
    ///   </para>
    /// </remarks>
    [Fact]
    public async Task ClockRunning_UtcNowRead_AfterRealElapsed_ApproximatesProjectedVirtualTime ()
    {
        DateTimeOffset initial = new(2020, 6, 1, 0, 0, 0, TimeSpan.Zero);
        TimeSpan runRate = TimeSpan.FromSeconds(10);
        IPrimeTestClock clock = new PrimeTestClock(initial);
        clock.Start(runRate);
        Stopwatch wall = Stopwatch.StartNew();
        await Task.Delay(ClockRunningProjectionTestWallDelay, TestContext.Current.CancellationToken);
        DateTimeOffset observed = clock.UtcNowDateTimeOffset;
        wall.Stop();
        clock.Stop().Should().BeTrue();
        TimeSpan virtualElapsedFromWall = PrimeTestClock.ScaleRealElapsedToVirtualTime(wall.Elapsed, runRate);
        DateTimeOffset expectedFromWall = initial + virtualElapsedFromWall;
        observed.Should().BeCloseTo(expectedFromWall, ClockRunningProjectionVirtualTolerance);
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies that a persist-on-read on <see cref="IPrimeClock.UtcNowDateTimeOffset"/> while the runner is active
    ///   marches through due virtual-time work so a pending delay can complete without waiting for the coarse runner
    ///   sleep.
    /// </summary>
    [Fact]
    public async Task ClockRunning_DelayDue_UtcNowRead_CompletesDelayTask ()
    {
        DateTimeOffset initial = new(2020, 6, 1, 12, 0, 0, TimeSpan.Zero);
        IPrimeTestClock clock = new PrimeTestClock(initial);
        Task delayTask = clock.DelayAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        clock.Start(TimeSpan.FromSeconds(10));
        await Task.Delay(ClockRunningDelayDueTestWallDelay, TestContext.Current.CancellationToken);
        _ = clock.UtcNowDateTimeOffset;
        clock.Stop().Should().BeTrue();
        delayTask.Status.Should().Be(TaskStatus.RanToCompletion);
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies that when the automatic runner is not active, <see cref="IPrimeClock.UtcNowDateTimeOffset"/> does
    ///   not advance with real time between explicit virtual-time operations.
    /// </summary>
    [Fact]
    public async Task ClockStopped_UtcNowRead_AfterRealElapsed_ReturnsPersistedInstant ()
    {
        DateTimeOffset initial = new(2020, 6, 1, 0, 0, 0, TimeSpan.Zero);
        IPrimeTestClock clock = new PrimeTestClock(initial);
        await Task.Delay(ClockStoppedNoAdvanceTestWallDelay, TestContext.Current.CancellationToken);
        clock.UtcNowDateTimeOffset.Should().Be(initial);
    }
    //----------------------------------------------------------------------------

    #endregion Start and Stop

    #region Runner loop

    /// <summary>
    ///   Verifies that the deadline-driven runner completes a delay registered after automatic virtual-time advancement
    ///   is started on <see cref="IPrimeTestClock"/>, without requiring a persist-on-read on
    ///   <see cref="IPrimeClock.UtcNowDateTimeOffset"/>.
    /// </summary>
    /// <remarks>
    ///   <para>
    ///     Uses a wall-clock ceiling (<see cref="BoundedRealTimeWaitTimeout"/>) so the delay task races a maximum wait,
    ///     allowing slow CI to pass when the runner still completes well under that budget.
    ///   </para>
    /// </remarks>
    [Fact]
    public async Task ClockRunning_DelayRegisteredWhileRunning_RunnerCompletesDelayWithoutPersistOnRead ()
    {
        DateTimeOffset initial = new(2020, 6, 1, 12, 0, 0, TimeSpan.Zero);
        IPrimeTestClock clock = new PrimeTestClock(initial);
        clock.Start(TimeSpan.FromSeconds(10));
        await Task.Delay(TimeSpan.FromMilliseconds(50), TestContext.Current.CancellationToken);
        Task delayTask = clock.DelayAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        Task delayOrTimeout = await Task.WhenAny(delayTask,
            Task.Delay(BoundedRealTimeWaitTimeout, TestContext.Current.CancellationToken));
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
    ///   Verifies that cancelling a delay while the runner is active completes the delay task promptly.
    /// </summary>
    [Fact]
    public async Task ClockRunning_CancelDelayWhileRunning_CancelsPromptly ()
    {
        DateTimeOffset initial = new(2020, 6, 1, 12, 0, 0, TimeSpan.Zero);
        using CancellationTokenSource cancelSource = new();
        IPrimeTestClock clock = new PrimeTestClock(initial);
        Task delayTask = clock.DelayAsync(TimeSpan.FromHours(1), cancelSource.Token);
        clock.Start(TimeSpan.FromSeconds(1));
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
        DateTimeOffset initial = new(2020, 6, 1, 0, 0, 0, TimeSpan.Zero);
        IPrimeTestClock clock = new PrimeTestClock(initial);
        int newTimeEventCount = 0;
        DateTimeOffset? lastHeartbeatUtc = null;
        clock.ClockEvents += (_, e) =>
        {
            if (e is not PrimeTestClockNewTimeEvent newTime)
            {
                return;
            }

            newTimeEventCount++;
            lastHeartbeatUtc = newTime.ClockTime;
        };
        clock.Start(TimeSpan.FromSeconds(10));
        await Task.Delay(ClockRunningHeartbeatTestWallDelay, TestContext.Current.CancellationToken);
        clock.Stop().Should().BeTrue();
        newTimeEventCount.Should().BeGreaterThanOrEqualTo(1);
        lastHeartbeatUtc.Should().NotBeNull();
        DateTimeOffset expectedHeartbeat = initial + TimeSpan.FromMinutes(1);
        lastHeartbeatUtc!.Value.Should().BeCloseTo(expectedHeartbeat, ClockRunningHeartbeatVirtualTolerance);
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies that <see cref="IPrimeTestClock.Stop"/> returns promptly while the runner would otherwise wait for a
    ///   virtual-minute heartbeat.
    /// </summary>
    [Fact]
    public async Task ClockRunning_StopWhileWaitingForHeartbeat_ReturnsPromptly ()
    {
        IPrimeTestClock clock = new PrimeTestClock(new DateTimeOffset(2020, 6, 1, 0, 0, 0, TimeSpan.Zero));
        clock.Start(TimeSpan.FromSeconds(10));
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
        DateTimeOffset initial = new(2020, 6, 1, 0, 0, 0, TimeSpan.Zero);
        IPrimeTestClock clock = new PrimeTestClock(initial);
        Task delayTask = clock.DelayAsync(TimeSpan.FromMilliseconds(50), TestContext.Current.CancellationToken);
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
        DateTimeOffset initial = new(2020, 6, 1, 0, 0, 0, TimeSpan.Zero);
        IPrimeTestClock clock = new PrimeTestClock(initial);
        Task delay10Ms = clock.DelayAsync(TimeSpan.FromMilliseconds(10), TestContext.Current.CancellationToken);
        Task delay20Ms = clock.DelayAsync(TimeSpan.FromMilliseconds(20), TestContext.Current.CancellationToken);
        Task delay30Ms = clock.DelayAsync(TimeSpan.FromMilliseconds(30), TestContext.Current.CancellationToken);
        Stopwatch wall = Stopwatch.StartNew();
        clock.Start(TimeSpan.FromSeconds(10));
        await Task.WhenAll(delay10Ms, delay20Ms, delay30Ms);
        wall.Stop();
        try
        {
            wall.Elapsed.Should().BeLessThan(ClockRunningBurstPathMaxWallDelay);
            clock.UtcNowDateTimeOffset.Should().BeOnOrAfter(initial + TimeSpan.FromMilliseconds(30));
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
        IPrimeTestClock clock = new PrimeTestClock(new DateTimeOffset(2020, 6, 1, 0, 0, 0, TimeSpan.Zero));
        Task delay300Ms = clock.DelayAsync(TimeSpan.FromMilliseconds(300), TestContext.Current.CancellationToken);
        Task delay400Ms = clock.DelayAsync(TimeSpan.FromMilliseconds(400), TestContext.Current.CancellationToken);
        Task delay500Ms = clock.DelayAsync(TimeSpan.FromMilliseconds(500), TestContext.Current.CancellationToken);
        Stopwatch wall = Stopwatch.StartNew();
        clock.Start(TimeSpan.FromSeconds(10));
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
        DateTimeOffset initial = new(2020, 6, 1, 0, 0, 0, TimeSpan.Zero);
        IPrimeTestClock clock = new PrimeTestClock(initial);
        List<DateTimeOffset> eventTimes = [];
        object sync = new();
        clock.ClockEvents += (_, e) =>
        {
            if (e is not PrimeTestClockTimedEvent timed)
            {
                return;
            }

            lock (sync)
            {
                eventTimes.Add(timed.ClockTime);
            }
        };
        int timerFireCount = 0;
        using IClockIntervalTimer registration = clock.RegisterTimer(TimeSpan.FromSeconds(45),
            () => Interlocked.Increment(ref timerFireCount),
            TestContext.Current.CancellationToken);
        clock.Start(TimeSpan.FromSeconds(10));
        await Task.Delay(ClockRunningHeartbeatResetTestWallDelay, TestContext.Current.CancellationToken);
        clock.Stop().Should().BeTrue();
        timerFireCount.Should().BeGreaterThanOrEqualTo(1);
        List<DateTimeOffset> copy;
        lock (sync)
        {
            copy = [.. eventTimes];
        }

        TimeSpan uncanceledHeartbeatWindowStart = TimeSpan.FromSeconds(58);
        TimeSpan uncanceledHeartbeatWindowEnd = TimeSpan.FromSeconds(62);
        foreach (DateTimeOffset eventTime in copy)
        {
            TimeSpan delta = eventTime - initial;
            bool inUncanceledHeartbeatWindow = delta >= uncanceledHeartbeatWindowStart
                && delta <= uncanceledHeartbeatWindowEnd;
            inUncanceledHeartbeatWindow.Should().BeFalse(
                "a 45 s substantive timer should reset the heartbeat window so no heartbeat-only raise occurs near T+60 s ({0:o})",
                eventTime);
        }

        DateTimeOffset resetHeartbeatLower = initial + TimeSpan.FromSeconds(45) + TimeSpan.FromMinutes(1)
            - ClockRunningResetHeartbeatVirtualTolerance;
        DateTimeOffset resetHeartbeatUpper = initial + TimeSpan.FromSeconds(45) + TimeSpan.FromMinutes(1)
            + ClockRunningResetHeartbeatVirtualTolerance;
        copy.Should().Contain(eventTime => eventTime >= resetHeartbeatLower && eventTime <= resetHeartbeatUpper);
    }
    //----------------------------------------------------------------------------

    #endregion Runner loop

    #region ClockEvents

    /// <summary>
    ///   Subscribes to <see cref="IPrimeTestClock.ClockEvents"/> and captures the first matching event of
    ///   <typeparamref name="TEvent"/> after each <see cref="ArmForNextReceived"/> call.
    /// </summary>
    /// <typeparam name="TEvent">Derived <see cref="PrimeTestClockEvent"/> type to capture.</typeparam>
    private sealed class ClockEventCapture<TEvent> : IDisposable where TEvent : PrimeTestClockEvent
    {
        private readonly IPrimeTestClock _clock;
        private readonly PrimeTestClockEventHandler _handler;
        private readonly object _sync = new();
        private bool _disposed;
        private TEvent? _lastReceived;
        private TaskCompletionSource<TEvent>? _waitForNext;

        public ClockEventCapture (IPrimeTestClock clock)
        {
            _clock = clock;
            _handler = OnClockEvent;
            _clock.ClockEvents += _handler;
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

        /// <summary>
        ///   Clears prior captures and starts a new wait so the next matching raise can be observed.
        /// </summary>
        /// <remarks>
        ///   Call before the clock operation that should raise the event, then call
        ///   <see cref="WaitAfterArmedForNextReceived"/>.
        /// </remarks>
        /// <exception cref="ObjectDisposedException">Thrown when the capture has been disposed.</exception>
        public void ArmForNextReceived ()
        {
            lock (_sync)
            {
                if (_disposed)
                {
                    throw new ObjectDisposedException(nameof(ClockEventCapture<TEvent>));
                }

                _lastReceived = null;
                _waitForNext = new TaskCompletionSource<TEvent>(TaskCreationOptions.RunContinuationsAsynchronously);
            }
        }

        /// <summary>
        ///   Waits until a matching event is raised after <see cref="ArmForNextReceived"/>, or the timeout elapses.
        /// </summary>
        /// <param name="timeout">Maximum real time to wait.</param>
        /// <param name="cancellationToken">Cancellation token for the wait.</param>
        /// <returns>The matching event received after arming.</returns>
        /// <exception cref="TimeoutException">
        ///   Thrown when no matching event is received before <paramref name="timeout"/> expires.
        /// </exception>
        /// <exception cref="OperationCanceledException">
        ///   Thrown when <paramref name="cancellationToken"/> is canceled during the wait.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        ///   Thrown when the capture is disposed while this method is waiting.
        /// </exception>
        public async Task<TEvent> WaitAfterArmedForNextReceived (
            TimeSpan timeout,
            CancellationToken cancellationToken)
        {
            Task<TEvent> waitTask;
            lock (_sync)
            {
                if (_disposed)
                {
                    throw new ObjectDisposedException(nameof(ClockEventCapture<TEvent>));
                }

                if (_waitForNext is null)
                {
                    throw new InvalidOperationException(
                        $"Call {nameof(ArmForNextReceived)} before waiting for the next {typeof(TEvent).Name}.");
                }

                waitTask = _waitForNext.Task;
            }

            Task delayTask = Task.Delay(timeout, cancellationToken);
            Task completedTask = await Task.WhenAny(waitTask, delayTask).ConfigureAwait(false);
            if (completedTask == waitTask)
            {
                lock (_sync)
                {
                    if (_waitForNext?.Task == waitTask)
                    {
                        _waitForNext = null;
                    }
                }

                return await waitTask.ConfigureAwait(false);
            }

            TaskCompletionSource<TEvent>? abandoned;
            lock (_sync)
            {
                abandoned = _waitForNext?.Task == waitTask ? _waitForNext : null;
                if (abandoned is not null)
                {
                    _waitForNext = null;
                    _lastReceived = null;
                }
            }

            if (cancellationToken.IsCancellationRequested)
            {
                abandoned?.TrySetCanceled(cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
            }

            abandoned?.TrySetException(new TimeoutException(
                $"No {typeof(TEvent).Name} was received within {timeout}."));
            throw new TimeoutException(
                $"No {typeof(TEvent).Name} was received within {timeout}.");
        }

        /// <summary>
        ///   Arms for the next matching event and waits until it is raised or the timeout elapses.
        /// </summary>
        /// <param name="timeout">Maximum real time to wait.</param>
        /// <param name="cancellationToken">Cancellation token for the wait.</param>
        /// <returns>The matching event received after this call begins waiting.</returns>
        /// <exception cref="TimeoutException">
        ///   Thrown when no matching event is received before <paramref name="timeout"/> expires.
        /// </exception>
        /// <exception cref="OperationCanceledException">
        ///   Thrown when <paramref name="cancellationToken"/> is canceled during the wait.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        ///   Thrown when the capture is disposed while this method is waiting.
        /// </exception>
        public async Task<TEvent> WaitForNextReceived (TimeSpan timeout, CancellationToken cancellationToken)
        {
            ArmForNextReceived();
            return await WaitAfterArmedForNextReceived(timeout, cancellationToken).ConfigureAwait(false);
        }

        public void Dispose ()
        {
            lock (_sync)
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
                _waitForNext?.TrySetException(new ObjectDisposedException(
                    nameof(ClockEventCapture<TEvent>),
                    $"The capture was disposed while waiting for the next {typeof(TEvent).Name}."));
            }

            _clock.ClockEvents -= _handler;
        }

        private void OnClockEvent (object? sender, PrimeTestClockEvent e)
        {
            if (e is not TEvent typed)
            {
                return;
            }

            lock (_sync)
            {
                if (_disposed)
                {
                    return;
                }

                if (_lastReceived is not null)
                {
                    return;
                }

                _lastReceived = typed;
                _waitForNext?.TrySetResult(typed);
            }
        }
    }

    /// <summary>
    ///   Subscribes to <see cref="IPrimeTestClock.ClockEvents"/> and collects all raised events of
    ///   <typeparamref name="TEvent"/>.
    /// </summary>
    /// <typeparam name="TEvent">Derived <see cref="PrimeTestClockEvent"/> type to capture.</typeparam>
    private sealed class ClockEventCollector<TEvent> : IDisposable where TEvent : PrimeTestClockEvent
    {
        private readonly IPrimeTestClock _clock;
        private readonly PrimeTestClockEventHandler _handler;
        private readonly object _sync = new();
        private bool _disposed;
        private readonly List<TEvent> _received = [];

        public ClockEventCollector (IPrimeTestClock clock)
        {
            _clock = clock;
            _handler = OnClockEvent;
            _clock.ClockEvents += _handler;
        }

        /// <summary>
        ///   Clears events collected so far.
        /// </summary>
        public void Clear ()
        {
            lock (_sync)
            {
                _received.Clear();
            }
        }

        /// <summary>
        ///   Returns a read-only copy of events collected so far.
        /// </summary>
        /// <returns>
        ///   A snapshot that does not reflect later raises and cannot be used to mutate the collector's buffer.
        /// </returns>
        public IReadOnlyList<TEvent> Snapshot ()
        {
            lock (_sync)
            {
                return [.. _received];
            }
        }

        public void Dispose ()
        {
            lock (_sync)
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
            }

            _clock.ClockEvents -= _handler;
        }

        private void OnClockEvent (object? sender, PrimeTestClockEvent e)
        {
            if (e is not TEvent typed)
            {
                return;
            }

            lock (_sync)
            {
                if (_disposed)
                {
                    return;
                }

                _received.Add(typed);
            }
        }
    }

    //----------------------------------------------------------------------------

    /// <summary>
    ///   Arms <paramref name="capture"/>, runs <paramref name="triggerClockAction"/>, and waits for the next
    ///   matching event (see <see cref="ClockEventCapture{TEvent}.ArmForNextReceived"/>).
    /// </summary>
    /// <typeparam name="TEvent">Derived <see cref="PrimeTestClockEvent"/> type to capture.</typeparam>
    /// <param name="capture">The subscribed capture helper.</param>
    /// <param name="triggerClockAction">The clock operation expected to raise the next event.</param>
    /// <param name="cancellationToken">Cancellation token for the wait.</param>
    /// <returns>The matching event raised by <paramref name="triggerClockAction"/>.</returns>
    private static async Task<TEvent> TriggerAndWaitForNextClockEvent<TEvent> (
        ClockEventCapture<TEvent> capture,
        Action triggerClockAction,
        CancellationToken cancellationToken) where TEvent : PrimeTestClockEvent
    {
        capture.ArmForNextReceived();
        triggerClockAction();
        return await capture.WaitAfterArmedForNextReceived(BoundedRealTimeWaitTimeout, cancellationToken)
            .ConfigureAwait(false);
    }

    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies that <see cref="IPrimeTestClock.ClockEvents"/> raises
    ///   <see cref="PrimeTestClockEventType.NewTime"/> when <see cref="IPrimeTestClock.SetTime"/> is called.
    /// </summary>
    [Fact]
    public async Task SetTime_WhenClockEventsSubscribed_RaisesEventWithNewTime ()
    {
        DateTimeOffset setTime = new(2025, 2, 20, 10, 0, 0, TimeSpan.Zero);
        IPrimeTestClock clock = new PrimeTestClock();
        using ClockEventCapture<PrimeTestClockNewTimeEvent> capture = new(clock);
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        PrimeTestClockNewTimeEvent received = await TriggerAndWaitForNextClockEvent(
            capture,
            () => clock.SetTime(setTime),
            cancellationToken);
        received.EventType.Should().Be(PrimeTestClockEventType.NewTime);
        received.ClockTime.Should().Be(setTime);
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies that <see cref="IPrimeTestClock.ClockEvents"/> raises
    ///   <see cref="PrimeTestClockEventType.NewTime"/> when <see cref="IPrimeTestClock.Advance"/> is called.
    /// </summary>
    [Fact]
    public async Task Advance_WhenClockEventsSubscribed_RaisesEventWithNewTime ()
    {
        DateTimeOffset initial = new(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);
        DateTimeOffset expected = initial + TimeSpan.FromHours(1);
        IPrimeTestClock clock = new PrimeTestClock(initial);
        using ClockEventCapture<PrimeTestClockNewTimeEvent> capture = new(clock);
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        PrimeTestClockNewTimeEvent received = await TriggerAndWaitForNextClockEvent(
            capture,
            () => clock.Advance(TimeSpan.FromHours(1)),
            cancellationToken);
        received.EventType.Should().Be(PrimeTestClockEventType.NewTime);
        received.ClockTime.Should().Be(expected);
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies that <see cref="IPrimeTestClock.ClockEvents"/> raises
    ///   <see cref="PrimeTestClockEventType.NewTime"/> at the bounded run stop instant when
    ///   <see cref="IPrimeTestClock.RunFor(TimeSpan)"/> completes.
    /// </summary>
    [Fact]
    public void RunFor_WhenClockEventsSubscribed_RaisesEventWithNewTime ()
    {
        DateTimeOffset initial = new(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);
        DateTimeOffset expected = initial + TimeSpan.FromMinutes(15);
        IPrimeTestClock clock = new PrimeTestClock(initial);
        using ClockEventListCapture<PrimeTestClockNewTimeEvent> capture = new(clock);
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        clock.RunFor(TimeSpan.FromMinutes(15), RunForTestFastPerSecondRate).Should().BeTrue();
        WaitUntilClockStopped(() => clock.IsRunning, BoundedRealTimeWaitTimeout, cancellationToken);
        WaitUntilCondition(
            () => capture.Any(e => e.ClockTime == expected),
            BoundedRealTimeWaitTimeout,
            cancellationToken,
            "Timed out waiting for NewTime at the RunFor stop instant after "
                + BoundedRealTimeWaitTimeout.TotalSeconds.ToString("g0", CultureInfo.InvariantCulture)
                + " seconds.");
        clock.UtcNowDateTimeOffset.Should().Be(expected);
        capture.Snapshot().Should().Contain(e => e.ClockTime == expected);
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies that <see cref="PrimeTestClockEventType.NewTime"/> raised while the clock is stopped exposes
    ///   <see langword="null"/> <see cref="PrimeTestClockTimedEvent.RunRateTimeSpan"/>.
    /// </summary>
    [Fact]
    public async Task SetTime_WhileStopped_NewTimeHasNullRunRate ()
    {
        DateTimeOffset setTime = new(2025, 2, 20, 10, 0, 0, TimeSpan.Zero);
        IPrimeTestClock clock = new PrimeTestClock(new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero));
        using ClockEventCapture<PrimeTestClockNewTimeEvent> capture = new(clock);
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        PrimeTestClockNewTimeEvent received = await TriggerAndWaitForNextClockEvent(
            capture,
            () => clock.SetTime(setTime),
            cancellationToken);
        received.RunRateTimeSpan.Should().BeNull();
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies that each <see cref="PrimeTestClockEventType.NewTime"/> raised during
    ///   <see cref="IPrimeTestClock.Advance"/> while the automatic runner is active includes the current run rate.
    /// </summary>
    [Fact]
    public void Advance_WhileRunning_NewTimeIncludesRunRate ()
    {
        DateTimeOffset initial = new(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);
        TimeSpan runRate = TimeSpan.FromSeconds(5);
        IPrimeTestClock clock = new PrimeTestClock(initial);
        using ClockEventCollector<PrimeTestClockNewTimeEvent> capture = new(clock);
        clock.Start(runRate);
        IReadOnlyList<PrimeTestClockNewTimeEvent> snapshot = [];
        try
        {
            clock.Advance(TimeSpan.FromMinutes(1));
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
            newTime.RunRateTimeSpan.Should().Be(runRate);
        }
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies that <see cref="IPrimeTestClock.Start"/> raises
    ///   <see cref="PrimeTestClockEventType.ClockStarted"/> on a stopped-to-running transition.
    /// </summary>
    [Fact]
    public async Task Start_FromStopped_RaisesClockStartedWithEventType ()
    {
        DateTimeOffset initial = new(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);
        TimeSpan runRate = TimeSpan.FromSeconds(5);
        IPrimeTestClock clock = new PrimeTestClock(initial);
        using ClockEventCapture<PrimeTestClockStartedEvent> capture = new(clock);
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        try
        {
            PrimeTestClockStartedEvent started = await TriggerAndWaitForNextClockEvent(
                capture,
                () => clock.Start(runRate),
                cancellationToken);
            started.EventType.Should().Be(PrimeTestClockEventType.ClockStarted);
            started.ClockTime.Should().Be(initial);
            started.RunRateTimeSpan.Should().Be(runRate);
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
        DateTimeOffset initial = new(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);
        TimeSpan runRate = TimeSpan.FromSeconds(5);
        IPrimeTestClock clock = new PrimeTestClock(initial);
        using ClockEventCollector<PrimeTestClockEvent> collector = new(clock);
        clock.Start(runRate);
        collector.Clear();

        try
        {
            clock.Start(runRate);
            IReadOnlyList<PrimeTestClockEvent> snapshot = collector.Snapshot();

            snapshot.Should().NotContain(e => e.EventType == PrimeTestClockEventType.ClockStarted);
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
        IPrimeTestClock clock = new PrimeTestClock(new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero));
        using ClockEventCollector<PrimeTestClockEvent> collector = new(clock);
        clock.Stop().Should().BeFalse();
        IReadOnlyList<PrimeTestClockEvent> snapshot = collector.Snapshot();

        snapshot.Should().NotContain(e => e.EventType == PrimeTestClockEventType.ClockStopped);
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies that <see cref="IPrimeTestClock.Stop"/> raises
    ///   <see cref="PrimeTestClockEventType.ClockStopped"/> after the runner joins, with the final committed UTC and
    ///   the rate that was running, and that no event follows shutdown.
    /// </summary>
    [Fact]
    public void Stop_WhenRunning_RaisesClockStoppedAsLastEventWithCommittedUtc ()
    {
        DateTimeOffset initial = new(2025, 1, 1, 12, 0, 0, TimeSpan.Zero);
        TimeSpan runRate = TimeSpan.FromSeconds(10);
        PrimeTestClock clock = new(initial);
        using ClockEventCollector<PrimeTestClockEvent> collector = new(clock);
        clock.Start(runRate);
        clock.Advance(TimeSpan.FromMinutes(1));
        clock.Stop().Should().BeTrue();
        IReadOnlyList<PrimeTestClockEvent> snapshot = collector.Snapshot();

        int stoppedAt = -1;
        for (int i = snapshot.Count - 1; i >= 0; i--)
        {
            if (snapshot[i].EventType != PrimeTestClockEventType.ClockStopped)
            {
                continue;
            }

            stoppedAt = i;
            break;
        }
        stoppedAt.Should().BeGreaterThanOrEqualTo(0);
        snapshot[stoppedAt].EventType.Should().Be(PrimeTestClockEventType.ClockStopped);
        snapshot[snapshot.Count - 1].EventType.Should().Be(PrimeTestClockEventType.ClockStopped);
        snapshot.Skip(stoppedAt + 1).Should().BeEmpty();

        PrimeTestClockStoppedEvent stopped = snapshot[stoppedAt]
            .Should()
            .BeOfType<PrimeTestClockStoppedEvent>()
            .Subject;
        stopped.RunRateTimeSpan.Should().Be(runRate);

        DateTimeOffset committedUtc = clock.UtcNowDateTimeOffset;
        committedUtc.Offset.Should().Be(TimeSpan.Zero);
        stopped.ClockTime.Should().Be(committedUtc);
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies that after <see cref="IPrimeTestClock.Stop"/> on a running clock with real elapsed time,
    ///   <see cref="IPrimeTestClock.UtcNowDateTimeOffset"/> matches the committed final instant and the
    ///   <see cref="PrimeTestClockStoppedEvent"/> payload (post-join commit and re-read), including UTC normalization
    ///   on <see cref="PrimeTestClockTimedEvent.ClockTime"/>.
    /// </summary>
    [Fact]
    public async Task Stop_WhenRunning_UtcNowMatchesCommittedFinalUtcAndClockStoppedPayload ()
    {
        DateTimeOffset initial = new(2025, 1, 1, 12, 0, 0, TimeSpan.Zero);
        TimeSpan runRate = TimeSpan.FromSeconds(10);
        PrimeTestClock clock = new(initial);
        PrimeTestClockStoppedEvent? stoppedEvent = null;
        clock.ClockEvents += (_, e) =>
        {
            if (e is PrimeTestClockStoppedEvent stopped)
            {
                stoppedEvent = stopped;
            }
        };
        clock.Start(runRate);
        await Task.Delay(ClockRunningProjectionTestWallDelay, TestContext.Current.CancellationToken);
        clock.Stop().Should().BeTrue();
        stoppedEvent.Should().NotBeNull();
        DateTimeOffset committedUtc = clock.UtcNowDateTimeOffset;
        committedUtc.Offset.Should().Be(TimeSpan.Zero);
        stoppedEvent!.ClockTime.Should().Be(committedUtc);
        stoppedEvent.ClockTime.Offset.Should().Be(TimeSpan.Zero);
        stoppedEvent.RunRateTimeSpan.Should().Be(runRate);
    }
    //----------------------------------------------------------------------------

    #endregion ClockEvents

    #region Sleep driven by virtual time

    /// <summary>
    ///   Verifies that Sleep completes when virtual time is advanced by the sleep duration (deterministic).
    /// </summary>
    [Fact]
    public async Task Sleep_WhenAdvanceCoversDuration_CompletesWithoutRealDelay ()
    {
        DateTimeOffset initial = new(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);
        IPrimeTestClock clock = new PrimeTestClock(initial);
        bool sleepCompleted = false;
        ManualResetEventSlim sleepRegistered = new(false);
        try
        {
            Task sleepTask = Task.Run(() =>
            {
                sleepRegistered.Set();
                clock.Sleep(TimeSpan.FromSeconds(5));
                sleepCompleted = true;
            }, TestContext.Current.CancellationToken);

            sleepRegistered.Wait(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken).Should().BeTrue("sleep task should register before advance");
            // Advance virtual time until the sleep completes (task may register late under parallel load). Poll with
            // yields so the task gets CPU; cap real time to avoid hanging.
            System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();
            while (!sleepCompleted && sw.Elapsed < BoundedRealTimeWaitTimeout)
            {
                clock.Advance(TimeSpan.FromSeconds(5));
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
    ///   Verifies that Sleep(0) completes immediately on a test clock.
    /// </summary>
    [Fact]
    public void Sleep_WithTimeSpanZero_CompletesWithoutThrowing ()
    {
        IPrimeTestClock clock = new PrimeTestClock();
        Action act = () => clock.Sleep(TimeSpan.Zero);
        act.Should().NotThrow();
    }
    //----------------------------------------------------------------------------

    #endregion Sleep driven by virtual time

    #region DelayAsync driven by virtual time

    /// <summary>
    ///   Verifies that DelayAsync completes when virtual time is advanced by the delay duration.
    /// </summary>
    [Fact]
    public async Task DelayAsync_WhenAdvanceCoversDuration_CompletesWithoutRealDelay ()
    {
        DateTimeOffset initial = new(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);
        IPrimeTestClock clock = new PrimeTestClock(initial);
        Task delayTask = clock.DelayAsync(TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken);

        delayTask.IsCompleted.Should().BeFalse();
        clock.Advance(TimeSpan.FromSeconds(3));
        await delayTask;
        delayTask.Status.Should().Be(TaskStatus.RanToCompletion);
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies that DelayAsync(TimeSpan, CancellationToken) throws
    ///   <see cref="OperationCanceledException"/> when the cancellation token is triggered during the delay.
    /// </summary>
    [Fact]
    public async Task DelayAsync_WhenTokenCancelledDuringDelay_ThrowsOperationCanceledException ()
    {
        DateTimeOffset initial = new(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);
        IPrimeTestClock clock = new PrimeTestClock(initial);
        using CancellationTokenSource cts = new();
        using CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cts.Token,
            TestContext.Current.CancellationToken);
        CancellationToken linked = linkedCts.Token;
#pragma warning disable xUnit1051 // Linked token includes TestContext.Current.CancellationToken for test cancellation
        Task delayTask = clock.DelayAsync(TimeSpan.FromSeconds(10), linked);
#pragma warning restore xUnit1051
        delayTask.IsCompleted.Should().BeFalse();
        cts.Cancel();
        Func<Task> act = async () => await delayTask;
        await act.Should().ThrowAsync<OperationCanceledException>();
    }
    //----------------------------------------------------------------------------

    #endregion DelayAsync driven by virtual time

    #region Time cancellation driven by virtual time

    /// <summary>
    ///   Verifies that GetTimeCancellationToken expires when virtual time reaches the cancel time.
    /// </summary>
    [Fact]
    public void GetTimeCancellationToken_WhenAdvanceReachesCancelTime_TokenIsCancelled ()
    {
        DateTimeOffset initial = new(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);
        IPrimeTestClock clock = new PrimeTestClock(initial);
        using TimeCancellationTokenSource tcs = clock.GetTimeCancellationToken(TimeSpan.FromSeconds(2));
        tcs.IsCancellationRequested.Should().BeFalse();
        clock.Advance(TimeSpan.FromSeconds(2));
        tcs.IsCancellationRequested.Should().BeTrue();
    }
    //----------------------------------------------------------------------------

    #endregion Time cancellation driven by virtual time

    #region Interval timer driven by virtual time

    /// <summary>
    ///   Verifies that a one-shot interval timer fires when virtual time is advanced past the callback time.
    /// </summary>
    [Fact]
    public void RegisterTimer_OneShot_WhenAdvanceReachesCallbackTime_FiresOnce ()
    {
        DateTimeOffset initial = new(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);
        IPrimeTestClock clock = new PrimeTestClock(initial);
        int fireCount = 0;
        using IClockIntervalTimer registration = clock.RegisterTimer(TimeSpan.FromSeconds(2),
            () => fireCount++,
            TestContext.Current.CancellationToken);
        fireCount.Should().Be(0);
        clock.Advance(TimeSpan.FromSeconds(1));
        fireCount.Should().Be(0);
        clock.Advance(TimeSpan.FromSeconds(1));
        fireCount.Should().Be(1);
        clock.Advance(TimeSpan.FromSeconds(10));
        fireCount.Should().Be(1);
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies that a repeating interval timer fires once per crossed due instant, including multiple ticks
    ///   inside a single <see cref="IPrimeTestClock.Advance(System.TimeSpan)"/> when that advance spans several intervals.
    /// </summary>
    [Fact]
    public void RegisterTimer_Repeating_WhenAdvanceCoversMultipleIntervals_FiresMultipleTimes ()
    {
        DateTimeOffset initial = new(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);
        IPrimeTestClock clock = new PrimeTestClock(initial);
        int fireCount = 0;
        using IClockIntervalTimer registration = clock.RegisterTimer(TimeSpan.FromSeconds(1),
            () => fireCount++,
            TestContext.Current.CancellationToken,
            repeat: true);
        clock.Advance(TimeSpan.FromSeconds(1));
        fireCount.Should().Be(1);
        clock.Advance(TimeSpan.FromSeconds(1));
        fireCount.Should().Be(2);
        clock.Advance(TimeSpan.FromSeconds(2));
        fireCount.Should().Be(4);
    }
    //----------------------------------------------------------------------------

    #endregion Interval timer driven by virtual time

#if NET
    #region Day-time timer driven by virtual time

    /// <summary>
    ///   Verifies that a UTC time-of-day timer fires when virtual time reaches the target time of day.
    /// </summary>
    [Fact]
    public void RegisterTimeOfDay_Utc_WhenAdvanceReachesTargetTimeOfDay_Fires ()
    {
        DateTimeOffset initial = new(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);
        IPrimeTestClock clock = new PrimeTestClock(initial);
        int fireCount = 0;
        UtcTimeOfDay twoAm = new(new TimeOnly(2, 0, 0));
        using IClockDayTimeTimer registration = clock.RegisterTimeOfDay(twoAm,
            () => fireCount++,
            cancellationToken: TestContext.Current.CancellationToken);
        clock.Advance(TimeSpan.FromHours(1));
        fireCount.Should().Be(0);
        clock.Advance(TimeSpan.FromHours(1));
        fireCount.Should().Be(1);
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies that while a synchronous UTC day-time callback runs,
    ///   <see cref="IClockTimer.CallbacksProcessing"/> is <c>true</c>.
    /// </summary>
    [Fact]
    public void RegisterTimeOfDay_Utc_WhileCallbackRuns_CallbacksProcessingIsTrue ()
    {
        DateTimeOffset initial = new(2025, 1, 1, 1, 0, 0, TimeSpan.Zero);
        IPrimeTestClock clock = new PrimeTestClock(initial);
        UtcTimeOfDay threeAmUtc = new(new TimeOnly(3, 0, 0));
        bool? seenProcessing = null;
        IClockDayTimeTimer? registration = null;
        registration = clock.RegisterTimeOfDay(threeAmUtc, () =>
            {
                seenProcessing = registration!.CallbacksProcessing;
            },
            cancellationToken: TestContext.Current.CancellationToken);
        using (registration)
        {
            clock.Advance(TimeSpan.FromHours(2));
        }

        seenProcessing.Should().BeTrue();
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies that setting <see cref="IClockTimer.Enabled"/> to <c>false</c> after the first fire
    ///   prevents the next scheduled daily occurrence when virtual time advances.
    /// </summary>
    [Fact]
    public void RegisterTimeOfDay_Utc_AfterFirstFire_SetEnabledFalse_AdvanceOneDay_NoSecondFire ()
    {
        DateTimeOffset initial = new(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);
        IPrimeTestClock clock = new PrimeTestClock(initial);
        UtcTimeOfDay twoAmUtc = new(new TimeOnly(2, 0, 0));
        int fireCount = 0;
        using IClockDayTimeTimer timer = clock.RegisterTimeOfDay(twoAmUtc, () => fireCount++,
            cancellationToken: TestContext.Current.CancellationToken);
        clock.Advance(TimeSpan.FromHours(2));
        fireCount.Should().Be(1);
        timer.Enabled = false;
        clock.Advance(TimeSpan.FromDays(1));
        fireCount.Should().Be(1);
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies that disabling before the first fire, then enabling again, still yields a callback
    ///   when virtual time reaches the scheduled UTC time of day.
    /// </summary>
    [Fact]
    public void RegisterTimeOfDay_Utc_DisableBeforeFirstDue_EnableThenAdvance_FiresOnce ()
    {
        DateTimeOffset initial = new(2025, 1, 1, 1, 0, 0, TimeSpan.Zero);
        IPrimeTestClock clock = new PrimeTestClock(initial);
        UtcTimeOfDay threeAmUtc = new(new TimeOnly(3, 0, 0));
        int fireCount = 0;
        using IClockDayTimeTimer timer = clock.RegisterTimeOfDay(threeAmUtc, () => fireCount++,
            cancellationToken: TestContext.Current.CancellationToken);
        timer.Enabled = false;
        clock.Advance(TimeSpan.FromHours(1));
        fireCount.Should().Be(0);
        timer.Enabled = true;
        clock.Advance(TimeSpan.FromHours(1));
        fireCount.Should().Be(1);
    }
    //----------------------------------------------------------------------------

    #endregion Day-time timer driven by virtual time
#endif
}
//################################################################################



