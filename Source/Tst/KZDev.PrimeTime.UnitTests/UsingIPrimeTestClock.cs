// Copyright (c) Kevin Zehrer
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;

using AwesomeAssertions;

using KZDev.PrimeTime.Tests;

using NodaTime;
// ReSharper disable AccessToDisposedClosure

namespace KZDev.PrimeTime.UnitTests;

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
    ///   <see cref="OverflowException"/> when the resulting instant exceeds Noda instant bounds.
    /// </summary>
    [Fact]
    public void Advance_WithDurationMaxValue_ThrowsOverflowException ()
    {
        Instant initial = Instant.FromUtc(2025, 1, 1, 0, 0, 0);
        IPrimeTestClock clock = new PrimeTestClock(initial);

        Action act = () => clock.Advance(Duration.MaxValue);
        act.Should().Throw<OverflowException>();
    }
    //----------------------------------------------------------------------------

    #endregion SetInstant, SetTime, SetLocalTime and Advance

    #region RunFor

    /// <summary>
    ///   Verifies that <see cref="IPrimeTestClock.RunFor(Duration)"/> advances virtual time by the given duration.
    /// </summary>
    [Fact]
    public void RunFor_WithDuration_AdvancesVirtualTimeByDuration ()
    {
        Instant initial = Instant.FromUtc(2025, 1, 1, 0, 0, 0);
        IPrimeTestClock clock = new PrimeTestClock(initial);
        clock.RunFor(Duration.FromMinutes(30));
        clock.NowInstant.Should().Be(initial + Duration.FromMinutes(30));
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
    ///   Verifies that <see cref="IPrimeTestClock.Start(Duration)"/> and <see cref="IPrimeTestClock.Stop"/> set
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
        clock.ClockEvents += (_, e) => received = e is NodaClockTimeChangedEventArgs nodaArgs ? nodaArgs.Instant : null;
        clock.SetInstant(setInstant);
        received.Should().NotBeNull();
        received!.Value.Should().Be(setInstant);
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies that <see cref="IPrimeTestClock.ClockEvents"/> is raised when Advance is called.
    /// </summary>
    [Fact]
    public void Advance_WhenClockEventsSubscribed_RaisesEventWithNewInstant ()
    {
        Instant initial = Instant.FromUtc(2025, 1, 1, 0, 0, 0);
        IPrimeTestClock clock = new PrimeTestClock(initial);
        Instant? received = null;
        clock.ClockEvents += (_, e) => received = e is NodaClockTimeChangedEventArgs nodaArgs ? nodaArgs.Instant : null;
        clock.Advance(Duration.FromHours(1));
        received.Should().NotBeNull();
        received!.Value.Should().Be(initial + Duration.FromHours(1));
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies that <see cref="IPrimeTestClock.ClockEvents"/> is raised when RunFor is called.
    /// </summary>
    [Fact]
    public void RunFor_WhenClockEventsSubscribed_RaisesEventWithNewInstant ()
    {
        Instant initial = Instant.FromUtc(2025, 1, 1, 0, 0, 0);
        IPrimeTestClock clock = new PrimeTestClock(initial);
        Instant? received = null;
        clock.ClockEvents += (_, e) => received = e is NodaClockTimeChangedEventArgs nodaArgs ? nodaArgs.Instant : null;
        clock.RunFor(Duration.FromMinutes(15));
        received.Should().NotBeNull();
        received!.Value.Should().Be(initial + Duration.FromMinutes(15));
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
    ///   Verifies that a repeating interval timer fires multiple times as virtual time advances.
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
        fireCount.Should().Be(3,
            "with countdown-after-callback (default), the next tick is scheduled from the virtual instant when the callback completes, so one Advance cannot fire twice at the same coarse time");
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

