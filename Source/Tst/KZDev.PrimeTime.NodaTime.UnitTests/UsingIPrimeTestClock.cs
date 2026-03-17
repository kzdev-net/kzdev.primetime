// Copyright (c) Kevin Zehrer
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

// Unit tests for IPrimeTestClock and PrimeTestClock (Phase 13).
// Verifies SetInstant, SetTime, SetLocalTime, Advance, RunFor, Start/Stop, ClockEvents,
// and that Sleep, DelayAsync, time cancellation, and timers are driven by virtual time.

using AwesomeAssertions;
using KZDev.PrimeTime.Tests;
using NodaTime;

namespace KZDev.PrimeTime.NodaTime.UnitTests;

/// <summary>
///   Unit tests for <see cref="IPrimeTestClock"/> and <see cref="PrimeTestClock"/>.
/// </summary>
public class UsingIPrimeTestClock : UnitTestBase
{
    #region Constructors/Finalizers

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

    #endregion Constructors/Finalizers

    private static readonly Duration SleepTestRealTimeTimeout = Duration.FromSeconds(5);

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

    /// <summary>
    ///   Verifies that <see cref="PrimeTestClock"/> implements <see cref="IPrimeTestClock"/>.
    /// </summary>
    [Fact]
    public void PrimeTestClock_ImplementsIPrimeTestClock ()
    {
        IPrimeTestClock clock = new PrimeTestClock();
        clock.Should().NotBeNull();
    }

    /// <summary>
    ///   Verifies that <see cref="PrimeTestClock"/> with initial instant returns that instant from Instant.
    /// </summary>
    [Fact]
    public void PrimeTestClock_WithInitialInstant_ReturnsThatInstantFromInstant ()
    {
        Instant initial = Instant.FromUtc(2020, 6, 15, 12, 0, 0);
        IPrimeTestClock clock = new PrimeTestClock(initial);
        clock.Instant.Should().Be(initial);
    }

    /// <summary>
    ///   Verifies that <see cref="PrimeTestClock"/> with initial instant and zone returns correct UtcNow and LocalZonedNow.
    /// </summary>
    [Fact]
    public void PrimeTestClock_WithInitialInstantAndZone_ReturnsCorrectZonedNow ()
    {
        Instant initial = Instant.FromUtc(2020, 6, 15, 12, 0, 0);
        DateTimeZone utc = DateTimeZone.Utc;
        IPrimeTestClock clock = new PrimeTestClock(initial, utc);
        clock.Instant.Should().Be(initial);
        clock.UtcNow.ToInstant().Should().Be(initial);
        clock.LocalZonedNow.Zone.Should().Be(utc);
    }

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
        clock.Instant.Should().Be(setInstant);
        clock.UtcNow.ToInstant().Should().Be(setInstant);
    }

    /// <summary>
    ///   Verifies that <see cref="IPrimeTestClock.SetTime"/> (DateTimeOffset) updates Instant.
    /// </summary>
    [Fact]
    public void SetTime_WithDateTimeOffset_UpdatesInstant ()
    {
        DateTimeOffset utcTime = new(2025, 2, 20, 10, 0, 0, TimeSpan.Zero);
        IPrimeTestClock clock = new PrimeTestClock();
        clock.SetTime(utcTime);
        clock.Instant.Should().Be(Instant.FromDateTimeUtc(utcTime.UtcDateTime));
    }

    /// <summary>
    ///   Verifies that <see cref="IPrimeTestClock.SetLocalTime"/> sets the instant from local date/time in the clock's zone.
    /// </summary>
    [Fact]
    public void SetLocalTime_WithLocalDateTime_UpdatesInstantInZone ()
    {
        IPrimeTestClock clock = new PrimeTestClock(Instant.FromUtc(2020, 1, 1, 0, 0, 0), DateTimeZone.Utc);
        LocalDateTime local = new LocalDate(2025, 3, 15).At(new LocalTime(9, 30));
        clock.SetLocalTime(local);
        clock.LocalNow.Should().Be(local);
        clock.UtcNow.LocalDateTime.Should().Be(local);
    }

    /// <summary>
    ///   Verifies that <see cref="IPrimeTestClock.Advance"/> adds duration to virtual time.
    /// </summary>
    [Fact]
    public void Advance_WithPositiveDuration_AddsToVirtualTime ()
    {
        Instant initial = Instant.FromUtc(2025, 1, 1, 0, 0, 0);
        IPrimeTestClock clock = new PrimeTestClock(initial);
        clock.Advance(Duration.FromHours(2));
        clock.Instant.Should().Be(initial + Duration.FromHours(2));
    }

    /// <summary>
    ///   Verifies that <see cref="IPrimeTestClock.Advance"/> with zero does not change time.
    /// </summary>
    [Fact]
    public void Advance_WithZero_LeavesTimeUnchanged ()
    {
        Instant initial = Instant.FromUtc(2025, 1, 1, 0, 0, 0);
        IPrimeTestClock clock = new PrimeTestClock(initial);
        clock.Advance(Duration.Zero);
        clock.Instant.Should().Be(initial);
    }

    /// <summary>
    ///   Verifies that <see cref="IPrimeTestClock.Advance"/> with negative duration does not move
    ///   time backward (implementation treats it as zero).
    /// </summary>
    [Fact]
    public void Advance_WithNegativeDuration_LeavesTimeUnchanged ()
    {
        Instant initial = Instant.FromUtc(2025, 1, 1, 0, 0, 0);
        IPrimeTestClock clock = new PrimeTestClock(initial);
        clock.Advance(Duration.FromHours(-1));
        clock.Instant.Should().Be(initial);
    }

    #endregion SetInstant, SetTime, SetLocalTime and Advance

    #region RunFor

    /// <summary>
    ///   Verifies that <see cref="IPrimeTestClock.RunFor"/> advances virtual time by the given duration.
    /// </summary>
    [Fact]
    public void RunFor_WithDuration_AdvancesVirtualTimeByDuration ()
    {
        Instant initial = Instant.FromUtc(2025, 1, 1, 0, 0, 0);
        IPrimeTestClock clock = new PrimeTestClock(initial);
        clock.RunFor(Duration.FromMinutes(30));
        clock.Instant.Should().Be(initial + Duration.FromMinutes(30));
    }

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

    /// <summary>
    ///   Verifies that <see cref="IPrimeTestClock.Stop"/> returns false when not running.
    /// </summary>
    [Fact]
    public void Stop_WhenNotRunning_ReturnsFalse ()
    {
        IPrimeTestClock clock = new PrimeTestClock();
        clock.Stop().Should().BeFalse();
    }

    /// <summary>
    ///   Verifies that <see cref="IPrimeTestClock.Start"/> and <see cref="IPrimeTestClock.Stop"/> set
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

    #endregion Start and Stop

    #region ClockEvents

    /// <summary>
    ///   Verifies that <see cref="IPrimeTestClock.ClockEvents"/> is raised when SetInstant is called.
    /// </summary>
    [Fact]
    public void SetInstant_WhenClockEventsSubscribed_RaisesEventWithNewInstant ()
    {
        Instant setInstant = Instant.FromUtc(2025, 2, 20, 10, 0, 0);
        IPrimeTestClock clock = new PrimeTestClock();
        Instant? received = null;
        clock.ClockEvents += (_, e) => received = e.Instant;
        clock.SetInstant(setInstant);
        received.Should().NotBeNull();
        received!.Value.Should().Be(setInstant);
    }

    /// <summary>
    ///   Verifies that <see cref="IPrimeTestClock.ClockEvents"/> is raised when Advance is called.
    /// </summary>
    [Fact]
    public void Advance_WhenClockEventsSubscribed_RaisesEventWithNewInstant ()
    {
        Instant initial = Instant.FromUtc(2025, 1, 1, 0, 0, 0);
        IPrimeTestClock clock = new PrimeTestClock(initial);
        Instant? received = null;
        clock.ClockEvents += (_, e) => received = e.Instant;
        clock.Advance(Duration.FromHours(1));
        received.Should().NotBeNull();
        received!.Value.Should().Be(initial + Duration.FromHours(1));
    }

    /// <summary>
    ///   Verifies that <see cref="IPrimeTestClock.ClockEvents"/> is raised when RunFor is called.
    /// </summary>
    [Fact]
    public void RunFor_WhenClockEventsSubscribed_RaisesEventWithNewInstant ()
    {
        Instant initial = Instant.FromUtc(2025, 1, 1, 0, 0, 0);
        IPrimeTestClock clock = new PrimeTestClock(initial);
        Instant? received = null;
        clock.ClockEvents += (_, e) => received = e.Instant;
        clock.RunFor(Duration.FromMinutes(15));
        received.Should().NotBeNull();
        received!.Value.Should().Be(initial + Duration.FromMinutes(15));
    }

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

            sleepRegistered.Wait(SleepTestRealTimeTimeout.ToTimeSpan(), TestContext.Current.CancellationToken).Should().BeTrue(
                "sleep task should register before advance");
            System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();
            while (!sleepCompleted && sw.Elapsed < SleepTestRealTimeTimeout.ToTimeSpan())
            {
                clock.Advance(Duration.FromSeconds(5));
                Thread.Sleep(0);
            }
        sleepCompleted.Should().BeTrue(
            "sleep should have completed within the real-time timeout so that virtual advance could complete the delay");
        await sleepTask;
        }
        finally
        {
            sleepRegistered.Dispose();
        }
    }

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
        using CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
            cts.Token, TestContext.Current.CancellationToken);
        CancellationToken linked = linkedCts.Token;
#pragma warning disable xUnit1051 // Linked token includes TestContext.Current.CancellationToken for test cancellation
        Task delayTask = clock.DelayAsync(Duration.FromSeconds(10), linked);
#pragma warning restore xUnit1051
        delayTask.IsCompleted.Should().BeFalse();
        cts.Cancel();
        Func<Task> act = async () => await delayTask;
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

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
        using (TimeCancellationTokenSource tcs = clock.GetTimeCancellationToken(Duration.FromSeconds(2)))
        {
            tcs.IsCancellationRequested.Should().BeFalse();
            clock.Advance(Duration.FromSeconds(2));
            tcs.IsCancellationRequested.Should().BeTrue();
        }
    }

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
        using (IPrimeClockTimerRegistration registration = clock.RegisterTimer(
            Duration.FromSeconds(2),
            () => fireCount++,
            false,
            cancellationToken: TestContext.Current.CancellationToken))
        {
            fireCount.Should().Be(0);
            clock.Advance(Duration.FromSeconds(1));
            fireCount.Should().Be(0);
            clock.Advance(Duration.FromSeconds(1));
            fireCount.Should().Be(1);
            clock.Advance(Duration.FromSeconds(10));
            fireCount.Should().Be(1);
        }
    }

    /// <summary>
    ///   Verifies that a repeating interval timer fires multiple times as virtual time advances.
    /// </summary>
    [Fact]
    public void RegisterTimer_Repeating_WhenAdvanceCoversMultipleIntervals_FiresMultipleTimes ()
    {
        Instant initial = Instant.FromUtc(2025, 1, 1, 0, 0, 0);
        IPrimeTestClock clock = new PrimeTestClock(initial);
        int fireCount = 0;
        using (IPrimeClockTimerRegistration registration = clock.RegisterTimer(
            Duration.FromSeconds(1),
            () => fireCount++,
            repeat: true,
            cancellationToken: TestContext.Current.CancellationToken))
        {
            clock.Advance(Duration.FromSeconds(1));
            fireCount.Should().Be(1);
            clock.Advance(Duration.FromSeconds(1));
            fireCount.Should().Be(2);
            clock.Advance(Duration.FromSeconds(2));
            fireCount.Should().Be(4);
        }
    }

    #endregion Interval timer driven by virtual time

    #region Day-time timer driven by virtual time

    /// <summary>
    ///   Verifies that a local time-of-day timer fires when virtual time reaches the target time of day.
    /// </summary>
    [Fact]
    public void RegisterTimeOfDay_Local_WhenAdvanceReachesTargetTimeOfDay_Fires ()
    {
        // Use UTC zone so "local" and "UTC" coincide and we can set instant to midnight then advance to 02:00
        Instant initial = Instant.FromUtc(2025, 1, 1, 0, 0, 0);
        IPrimeTestClock clock = new PrimeTestClock(initial, DateTimeZone.Utc);
        int fireCount = 0;
        LocalTime twoAm = new LocalTime(2, 0, 0);
        using (IPrimeClockTimerRegistration registration = clock.RegisterTimeOfDay(
            twoAm,
            () => fireCount++,
            cancellationToken: TestContext.Current.CancellationToken))
        {
            clock.Advance(Duration.FromHours(1));
            fireCount.Should().Be(0);
            clock.Advance(Duration.FromHours(1));
            fireCount.Should().Be(1);
        }
    }

    #endregion Day-time timer driven by virtual time
}
