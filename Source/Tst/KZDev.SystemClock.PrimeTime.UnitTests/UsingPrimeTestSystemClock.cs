// Copyright (c) Kevin Zehrer
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

// Unit tests for IPrimeTestSystemClock and PrimeTestSystemClock.
// Verifies SetTime, Advance, RunFor, Start/Stop, ClockEvents, and that Sleep, DelayAsync,
// time cancellation, and timers are driven by virtual time.

using AwesomeAssertions;
using KZDev.PrimeTime.Tests;

namespace KZDev.SystemClock.PrimeTime.UnitTests;

/// <summary>
///   Unit tests for <see cref="IPrimeTestSystemClock"/> and <see cref="PrimeTestSystemClock"/>.
/// </summary>
public class UsingPrimeTestSystemClock : UnitTestBase
{
    #region Constructors/Finalizers

    /// <summary>
    ///   Initializes a new instance of the <see cref="UsingPrimeTestSystemClock"/> class.
    /// </summary>
    /// <param name="xUnitTestOutputHelper">
    ///   The xUnit test output helper that can be used to output test messages.
    /// </param>
    public UsingPrimeTestSystemClock (ITestOutputHelper xUnitTestOutputHelper)
        : base(xUnitTestOutputHelper)
    {
    }

    #endregion Constructors/Finalizers

    private static readonly TimeSpan SleepTestRealTimeTimeout = TimeSpan.FromSeconds(5);

    #region Contract and construction

    /// <summary>
    ///   Verifies that <see cref="IPrimeTestSystemClock"/> extends <see cref="IPrimeTestTime"/> and
    ///   <see cref="IPrimeSystemClock"/>.
    /// </summary>
    [Fact]
    public void IPrimeTestSystemClock_ExtendsIPrimeTestTimeAndIPrimeSystemClock ()
    {
        typeof(IPrimeTestSystemClock).GetInterfaces().Should().Contain(typeof(IPrimeTestTime));
        typeof(IPrimeTestSystemClock).GetInterfaces().Should().Contain(typeof(IPrimeSystemClock));
    }

    /// <summary>
    ///   Verifies that <see cref="PrimeTestSystemClock"/> implements <see cref="IPrimeTestSystemClock"/>.
    /// </summary>
    [Fact]
    public void PrimeTestSystemClock_ImplementsIPrimeTestSystemClock ()
    {
        IPrimeTestSystemClock clock = new PrimeTestSystemClock();
        clock.Should().NotBeNull();
    }

    /// <summary>
    ///   Verifies that <see cref="PrimeTestSystemClock"/> default constructor sets a non-default UtcNow.
    /// </summary>
    [Fact]
    public void PrimeTestSystemClock_DefaultConstructor_SetsUtcNow ()
    {
        IPrimeTestSystemClock clock = new PrimeTestSystemClock();
        clock.UtcNow.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(2));
    }

    /// <summary>
    ///   Verifies that <see cref="PrimeTestSystemClock"/> with initial time returns that time from UtcNow.
    /// </summary>
    [Fact]
    public void PrimeTestSystemClock_WithInitialTime_ReturnsThatTimeFromUtcNow ()
    {
        DateTimeOffset initial = new(2020, 6, 15, 12, 0, 0, TimeSpan.Zero);
        IPrimeTestSystemClock clock = new PrimeTestSystemClock(initial);
        clock.UtcNow.Should().Be(initial);
    }

    #endregion Contract and construction

    #region SetTime and Advance

    /// <summary>
    ///   Verifies that <see cref="IPrimeTestSystemClock.SetTime"/> updates UtcNow and related "now" members.
    /// </summary>
    [Fact]
    public void SetTime_WithUtcTime_UpdatesUtcNowAndRelatedMembers ()
    {
        DateTimeOffset setTime = new(2025, 1, 10, 14, 30, 0, TimeSpan.Zero);
        IPrimeTestSystemClock clock = new PrimeTestSystemClock();
        clock.SetTime(setTime);
        clock.UtcNow.Should().Be(setTime);
        clock.UtcDateTimeNow.Should().Be(setTime.UtcDateTime);
    }

    /// <summary>
    ///   Verifies that <see cref="IPrimeTestSystemClock.Advance"/> adds duration to virtual time.
    /// </summary>
    [Fact]
    public void Advance_WithPositiveDuration_AddsToVirtualTime ()
    {
        DateTimeOffset initial = new(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);
        IPrimeTestSystemClock clock = new PrimeTestSystemClock(initial);
        clock.Advance(TimeSpan.FromHours(2));
        clock.UtcNow.Should().Be(initial + TimeSpan.FromHours(2));
    }

    /// <summary>
    ///   Verifies that <see cref="IPrimeTestSystemClock.Advance"/> with zero does not change time.
    /// </summary>
    [Fact]
    public void Advance_WithZero_LeavesTimeUnchanged ()
    {
        DateTimeOffset initial = new(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);
        IPrimeTestSystemClock clock = new PrimeTestSystemClock(initial);
        clock.Advance(TimeSpan.Zero);
        clock.UtcNow.Should().Be(initial);
    }

    #endregion SetTime and Advance

    #region RunFor

    /// <summary>
    ///   Verifies that <see cref="IPrimeTestSystemClock.RunFor"/> advances virtual time by the given duration.
    /// </summary>
    [Fact]
    public void RunFor_WithDuration_AdvancesVirtualTimeByDuration ()
    {
        DateTimeOffset initial = new(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);
        IPrimeTestSystemClock clock = new PrimeTestSystemClock(initial);
        clock.RunFor(TimeSpan.FromMinutes(30));
        clock.UtcNow.Should().Be(initial + TimeSpan.FromMinutes(30));
    }

    #endregion RunFor

    #region Start and Stop

    /// <summary>
    ///   Verifies that <see cref="IPrimeTestSystemClock.IsRunning"/> is false when not started.
    /// </summary>
    [Fact]
    public void IsRunning_WhenNotStarted_ReturnsFalse ()
    {
        IPrimeTestSystemClock clock = new PrimeTestSystemClock();
        clock.IsRunning.Should().BeFalse();
    }

    /// <summary>
    ///   Verifies that <see cref="IPrimeTestSystemClock.Stop"/> returns false when not running.
    /// </summary>
    [Fact]
    public void Stop_WhenNotRunning_ReturnsFalse ()
    {
        IPrimeTestSystemClock clock = new PrimeTestSystemClock();
        clock.Stop().Should().BeFalse();
    }

    /// <summary>
    ///   Verifies that <see cref="IPrimeTestSystemClock.Start"/> and <see cref="IPrimeTestSystemClock.Stop"/> set
    ///   IsRunning and that Stop returns true when was running.
    /// </summary>
    [Fact]
    public void Start_ThenStop_SetsIsRunningAndStopReturnsTrue ()
    {
        IPrimeTestSystemClock clock = new PrimeTestSystemClock(new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero));
        clock.Start(TimeSpan.FromSeconds(1));
        clock.IsRunning.Should().BeTrue();
        bool stopped = clock.Stop();
        stopped.Should().BeTrue();
        clock.IsRunning.Should().BeFalse();
    }

    #endregion Start and Stop

    #region ClockEvents

    /// <summary>
    ///   Verifies that <see cref="IPrimeTestSystemClock.ClockEvents"/> is raised when SetTime is called.
    /// </summary>
    [Fact]
    public void SetTime_WhenClockEventsSubscribed_RaisesEventWithNewTime ()
    {
        DateTimeOffset setTime = new(2025, 2, 20, 10, 0, 0, TimeSpan.Zero);
        IPrimeTestSystemClock clock = new PrimeTestSystemClock();
        DateTimeOffset? received = null;
        clock.ClockEvents += (_, e) => received = e.UtcNow;
        clock.SetTime(setTime);
        received.Should().NotBeNull();
        received!.Value.Should().Be(setTime);
    }

    /// <summary>
    ///   Verifies that <see cref="IPrimeTestSystemClock.ClockEvents"/> is raised when Advance is called.
    /// </summary>
    [Fact]
    public void Advance_WhenClockEventsSubscribed_RaisesEventWithNewTime ()
    {
        DateTimeOffset initial = new(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);
        IPrimeTestSystemClock clock = new PrimeTestSystemClock(initial);
        DateTimeOffset? received = null;
        clock.ClockEvents += (_, e) => received = e.UtcNow;
        clock.Advance(TimeSpan.FromHours(1));
        received.Should().NotBeNull();
        received!.Value.Should().Be(initial + TimeSpan.FromHours(1));
    }

    /// <summary>
    ///   Verifies that <see cref="IPrimeTestSystemClock.ClockEvents"/> is raised when RunFor is called.
    /// </summary>
    [Fact]
    public void RunFor_WhenClockEventsSubscribed_RaisesEventWithNewTime ()
    {
        DateTimeOffset initial = new(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);
        IPrimeTestSystemClock clock = new PrimeTestSystemClock(initial);
        DateTimeOffset? received = null;
        clock.ClockEvents += (_, e) => received = e.UtcNow;
        clock.RunFor(TimeSpan.FromMinutes(15));
        received.Should().NotBeNull();
        received!.Value.Should().Be(initial + TimeSpan.FromMinutes(15));
    }

    #endregion ClockEvents

    #region Sleep driven by virtual time

    /// <summary>
    ///   Verifies that Sleep completes when virtual time is advanced by the sleep duration (deterministic).
    /// </summary>
    [Fact]
    public async Task Sleep_WhenAdvanceCoversDuration_CompletesWithoutRealDelay ()
    {
        DateTimeOffset initial = new(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);
        IPrimeTestSystemClock clock = new PrimeTestSystemClock(initial);
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

    /// <summary>
    ///   Verifies that Sleep(0) completes immediately on a test clock.
    /// </summary>
    [Fact]
    public void Sleep_WithTimeSpanZero_CompletesWithoutThrowing ()
    {
        IPrimeTestSystemClock clock = new PrimeTestSystemClock();
        Action act = () => clock.Sleep(TimeSpan.Zero);
        act.Should().NotThrow();
    }

    #endregion Sleep driven by virtual time

    #region DelayAsync driven by virtual time

    /// <summary>
    ///   Verifies that DelayAsync completes when virtual time is advanced by the delay duration.
    /// </summary>
    [Fact]
    public async Task DelayAsync_WhenAdvanceCoversDuration_CompletesWithoutRealDelay ()
    {
        DateTimeOffset initial = new(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);
        IPrimeTestSystemClock clock = new PrimeTestSystemClock(initial);
        Task delayTask = clock.DelayAsync(TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken);

        delayTask.IsCompleted.Should().BeFalse();
        clock.Advance(TimeSpan.FromSeconds(3));
        await delayTask;
        delayTask.Status.Should().Be(TaskStatus.RanToCompletion);
    }

    /// <summary>
    ///   Verifies that DelayAsync(TimeSpan, CancellationToken) throws
    ///   <see cref="OperationCanceledException"/> when the cancellation token is triggered during the delay.
    /// </summary>
    [Fact]
    public async Task DelayAsync_WhenTokenCancelledDuringDelay_ThrowsOperationCanceledException ()
    {
        DateTimeOffset initial = new(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);
        IPrimeTestSystemClock clock = new PrimeTestSystemClock(initial);
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

    #endregion DelayAsync driven by virtual time

    #region Time cancellation driven by virtual time

    /// <summary>
    ///   Verifies that GetTimeCancellationToken expires when virtual time reaches the cancel time.
    /// </summary>
    [Fact]
    public void GetTimeCancellationToken_WhenAdvanceReachesCancelTime_TokenIsCancelled ()
    {
        DateTimeOffset initial = new(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);
        IPrimeTestSystemClock clock = new PrimeTestSystemClock(initial);
        using TimeCancellationTokenSource tcs = clock.GetTimeCancellationToken(TimeSpan.FromSeconds(2));
        tcs.IsCancellationRequested.Should().BeFalse();
        clock.Advance(TimeSpan.FromSeconds(2));
        tcs.IsCancellationRequested.Should().BeTrue();
    }

    #endregion Time cancellation driven by virtual time

    #region Interval timer driven by virtual time

    /// <summary>
    ///   Verifies that a one-shot interval timer fires when virtual time is advanced past the callback time.
    /// </summary>
    [Fact]
    public void RegisterTimer_OneShot_WhenAdvanceReachesCallbackTime_FiresOnce ()
    {
        DateTimeOffset initial = new(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);
        IPrimeTestSystemClock clock = new PrimeTestSystemClock(initial);
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

    /// <summary>
    ///   Verifies that a repeating interval timer fires multiple times as virtual time advances.
    /// </summary>
    [Fact]
    public void RegisterTimer_Repeating_WhenAdvanceCoversMultipleIntervals_FiresMultipleTimes ()
    {
        DateTimeOffset initial = new(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);
        IPrimeTestSystemClock clock = new PrimeTestSystemClock(initial);
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
        IPrimeTestSystemClock clock = new PrimeTestSystemClock(initial);
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

    #endregion Day-time timer driven by virtual time
#endif
}
