// Copyright (c) Kevin Zehrer
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

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
        clock.Start(TimeSpan.FromMilliseconds(100));
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
        TimeSpan belowMinimum = TimeSpan.FromMilliseconds(100) - TimeSpan.FromTicks(1);
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



