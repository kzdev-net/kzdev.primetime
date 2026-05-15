// Copyright (c) Kevin Zehrer
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

using AwesomeAssertions;

using KZDev.PrimeTime.Tests;
// ReSharper disable AccessToDisposedClosure

namespace KZDev.SystemClock.PrimeTime.Testing.UnitTests;

//################################################################################
/// <summary>
///   Unit tests for <see cref="IPrimeTestClock"/> and <see cref="PrimeTestClock"/>.
/// </summary>
/// <remarks>
///   Covers <see cref="IPrimeTestClock.SetTime"/>, <see cref="IPrimeTestClock.Advance"/>,
///   <see cref="IPrimeTestClock.RunFor"/>, start/stop, <see cref="IPrimeTestClock.ClockEvents"/>, and
///   virtual-time-driven sleep, delay, time cancellation, and timers.
/// </remarks>
[ExcludeFromCodeCoverage]
public class UsingPrimeTestClock : UnitTestBase
{
    //----------------------------------------------------------------------------
    /// <summary>
    ///   Wall-clock guard for loops that wait for a virtual sleep completion flag while advancing virtual time on
    ///   another path.
    /// </summary>
    private static readonly TimeSpan SleepTestRealTimeTimeout = TimeSpan.FromSeconds(5);

    /// <summary>
    ///   Minimum virtual time per real second allowed by <see cref="PrimeTestClock.Start(TimeSpan?)"/>.
    /// </summary>
    private static readonly TimeSpan MinimumAllowedStartRunRate = TimeSpan.FromMilliseconds(100);

    /// <summary>
    ///   Brief real wall delay while the automatic runner is active: long enough for measurable virtual
    ///   advancement at 10 virtual seconds per real second without slowing the suite.
    /// </summary>
    private static readonly TimeSpan ClockRunningProjectionTestWallDelay = TimeSpan.FromMilliseconds(250);

    /// <summary>
    ///   Virtual-time assertion margin for runner projection tests: separate stopwatch instances,
    ///   imprecise <see cref="Task.Delay(TimeSpan, CancellationToken)"/>, and reads after wall stop.
    /// </summary>
    private static readonly TimeSpan ClockRunningProjectionVirtualTolerance = TimeSpan.FromMilliseconds(100);

    /// <summary>
    ///   Real wall delay before persist-on-read in delay-due tests: slightly exceeds the ~500 ms real time
    ///   needed for a 5 s virtual delay at 10 virtual seconds per real second, plus runner scheduling slack.
    /// </summary>
    private static readonly TimeSpan ClockRunningDelayDueTestWallDelay = TimeSpan.FromMilliseconds(600);

    /// <summary>
    ///   Short real wall delay used when the runner is stopped to confirm virtual time does not drift with wall time.
    /// </summary>
    private static readonly TimeSpan ClockStoppedNoAdvanceTestWallDelay = TimeSpan.FromMilliseconds(50);
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
        DateTimeOffset initial = new(2025, 1, 1, 12, 0, 0, TimeSpan.Zero);
        DateTimeOffset earlier = initial - TimeSpan.FromHours(1);
        IPrimeTestClock clock = new PrimeTestClock(initial);
        using (clock.RegisterTimer(TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1), _ => { },
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
    ///   Verifies that <see cref="IPrimeTestClock.RunFor"/> advances virtual time by the given duration.
    /// </summary>
    [Fact]
    public void RunFor_WithDuration_AdvancesVirtualTimeByDuration ()
    {
        DateTimeOffset initial = new(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);
        IPrimeTestClock clock = new PrimeTestClock(initial);
        clock.RunFor(TimeSpan.FromMinutes(30));
        clock.UtcNowDateTimeOffset.Should().Be(initial + TimeSpan.FromMinutes(30));
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
        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("rate");
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
        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("rate");
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
        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("rate");
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
    ///     clock&apos;s anchor stopwatch is a different instance than the test stopwatch, <see cref="Task.Delay"/> does
    ///     not guarantee exact wall duration, and the clock may advance slightly between <c>Stop()</c> on the wall
    ///     timer and the <see cref="IPrimeClock.UtcNowDateTimeOffset"/> read.
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
        wall.Stop();
        DateTimeOffset observed = clock.UtcNowDateTimeOffset;
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

    #region ClockEvents

    /// <summary>
    ///   Verifies that <see cref="IPrimeTestClock.ClockEvents"/> is raised when SetTime is called.
    /// </summary>
    [Fact]
    public void SetTime_WhenClockEventsSubscribed_RaisesEventWithNewTime ()
    {
        DateTimeOffset setTime = new(2025, 2, 20, 10, 0, 0, TimeSpan.Zero);
        IPrimeTestClock clock = new PrimeTestClock();
        DateTimeOffset? received = null;
        clock.ClockEvents += (_, e) => received = e.UtcNowDateTimeOffset;
        clock.SetTime(setTime);
        received.Should().NotBeNull();
        received!.Value.Should().Be(setTime);
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies that <see cref="IPrimeTestClock.ClockEvents"/> is raised when Advance is called.
    /// </summary>
    [Fact]
    public void Advance_WhenClockEventsSubscribed_RaisesEventWithNewTime ()
    {
        DateTimeOffset initial = new(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);
        IPrimeTestClock clock = new PrimeTestClock(initial);
        DateTimeOffset? received = null;
        clock.ClockEvents += (_, e) => received = e.UtcNowDateTimeOffset;
        clock.Advance(TimeSpan.FromHours(1));
        received.Should().NotBeNull();
        received!.Value.Should().Be(initial + TimeSpan.FromHours(1));
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies that <see cref="IPrimeTestClock.ClockEvents"/> is raised when RunFor is called.
    /// </summary>
    [Fact]
    public void RunFor_WhenClockEventsSubscribed_RaisesEventWithNewTime ()
    {
        DateTimeOffset initial = new(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);
        IPrimeTestClock clock = new PrimeTestClock(initial);
        DateTimeOffset? received = null;
        clock.ClockEvents += (_, e) => received = e.UtcNowDateTimeOffset;
        clock.RunFor(TimeSpan.FromMinutes(15));
        received.Should().NotBeNull();
        received!.Value.Should().Be(initial + TimeSpan.FromMinutes(15));
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
            while (!sleepCompleted && sw.Elapsed < SleepTestRealTimeTimeout)
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



