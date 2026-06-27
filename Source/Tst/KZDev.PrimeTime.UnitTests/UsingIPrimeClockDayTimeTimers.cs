// Copyright (c) Kevin Zehrer
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using System.Threading;

using AwesomeAssertions;

using KZDev.PrimeTime.Testing;
using KZDev.PrimeTime.Tests;

using NodaTime;

namespace KZDev.PrimeTime.UnitTests;

//################################################################################
/// <summary>
///   Unit tests for <see cref="IPrimeClock"/> RegisterTimeOfDay and RegisterAsyncTimeOfDay
///   (day-time timers) and <see cref="IClockDayTimeTimer"/> (Change(LocalTime), options).
/// </summary>
/// <remarks>
///   Wall-clock tests assert callback timing with a lower bound only; schedule math and upper-bound timing
///   are covered by <see cref="PrimeTestClock"/> tests in the Testing assembly.
/// </remarks>
[ExcludeFromCodeCoverage]
public class UsingIPrimeClockDayTimeTimers : UnitTestBase
{
    //----------------------------------------------------------------------------
    /// <summary>
    ///   Lead time when computing a local time-of-day target for wall-clock tests. Must exceed worst-case
    ///   registration delay on CI so the next occurrence stays on today's calendar date.
    /// </summary>
    private static readonly Duration ScheduleLeadTime = Duration.FromSeconds(1);

    /// <summary>
    ///   Extra wait time beyond expected delay to avoid flaky failures.
    /// </summary>
    private static readonly Duration WaitMargin = Duration.FromMilliseconds(450);

    /// <summary>
    ///   Wall-clock wait budget for a callback that is expected to fire after <see cref="ScheduleLeadTime"/>.
    /// </summary>
    private static readonly TimeSpan CallbackWaitTimeout =
        GetFirstCallbackWaitTimeout(ScheduleLeadTime.ToTimeSpan(), WaitMargin.ToTimeSpan());

    /// <summary>
    ///   Allowed shortfall when asserting callback timing (wall-clock lower bound only).
    /// </summary>
    private static readonly Duration TimingTolerance = Duration.FromMilliseconds(180);

    /// <summary>
    ///   Brief wait after callback to let state settle before assertions.
    /// </summary>
    private static readonly Duration CallbackSettle = Duration.FromMilliseconds(50);

    /// <summary>
    ///   How far ahead the cancel-before-fire test schedules its target time-of-day.
    /// </summary>
    private static readonly Duration CancelBeforeFireDelay = Duration.FromSeconds(5);

    /// <summary>
    ///   Returns a local time-of-day that is <paramref name="leadTime"/> after <paramref name="clock"/>'s current instant.
    /// </summary>
    /// <param name="clock">Clock used to read the current instant and local zone.</param>
    /// <param name="leadTime">How far ahead to place the target on today's local calendar.</param>
    /// <returns>The local time-of-day for the next timer registration.</returns>
    private static LocalTime GetSoonLocalTimeOfDay (IPrimeClock clock, Duration leadTime) =>
        (clock.NowInstant + leadTime).InZone(clock.LocalZonedNowInstant.Zone).LocalDateTime.TimeOfDay;
    //----------------------------------------------------------------------------

    #region Constructors/Finalizers

    //----------------------------------------------------------------------------
    /// <summary>
    ///   Initializes a new instance of the <see cref="UsingIPrimeClockDayTimeTimers"/> class.
    /// </summary>
    /// <param name="xUnitTestOutputHelper">
    ///   The xUnit test output helper for diagnostic output.
    /// </param>
    public UsingIPrimeClockDayTimeTimers (ITestOutputHelper xUnitTestOutputHelper)
        : base(xUnitTestOutputHelper)
    {
    }
    //----------------------------------------------------------------------------

    #endregion Constructors/Finalizers

    #region Local time-of-day — fire and contract

    /// <summary>
    ///   Verifies that a local time-of-day timer invokes the callback once when the
    ///   target time is reached (time set to a short delay from "now").
    /// </summary>
    [Fact]
    public void RegisterTimeOfDay_LocalTime_CallbackFiresNearTargetTime ()
    {
        IPrimeClock clock = new PrimeClock();
        LocalTime targetTime = GetSoonLocalTimeOfDay(clock, ScheduleLeadTime);
        ManualResetEventSlim signal = new(false);
        Instant? firedAt = null;

        using IClockDayTimeTimer timer = clock.RegisterTimeOfDay(targetTime, () =>
        {
            firedAt = clock.NowInstant;
            signal.Set();
        }, cancellationToken: TestContext.Current.CancellationToken);
        Instant start = clock.NowInstant;
        signal.Wait(CallbackWaitTimeout, TestContext.Current.CancellationToken).Should().BeTrue();
        firedAt.Should().NotBeNull();
        AssertWallClockCallbackElapsedNotBefore(
            (firedAt!.Value - start).ToTimeSpan(), ScheduleLeadTime.ToTimeSpan(), TimingTolerance.ToTimeSpan());
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies that a local time-of-day timer returns a registration with
    ///   <see cref="IRegisteredTimer.IsTimeOfDay"/> true and correct options when cast to
    ///   <see cref="IDayTimeTimer"/>.
    /// </summary>
    [Fact]
    public void RegisterTimeOfDay_LocalTime_ReturnsTimerWithCorrectContractProperties ()
    {
        IPrimeClock clock = new PrimeClock();
        LocalTime target = GetSoonLocalTimeOfDay(clock, ScheduleLeadTime);
        ManualResetEventSlim signal = new(false);

        using IClockDayTimeTimer timer = clock.RegisterTimeOfDay(target, signal.Set,
            TestContext.Current.CancellationToken,
            new DayTimeTimerOptions
            {
                ConcurrentTriggerProcessing = ConcurrentTriggerProcessing.RunConcurrently,
                SkippedTimeBehavior = SkippedTimeBehavior.RunAfter,
                DuplicateTimeBehavior = DuplicateTimeBehavior.RunFirst
            });
        timer.Id.Should().BeGreaterThan(0);
        timer.IsTimeOfDay.Should().BeTrue();
        timer.IsRepeating.Should().BeTrue();
        timer.IsLocalTimeRepresentation.Should().BeTrue();
        IDayTimeTimer dayTimer = (IDayTimeTimer)timer;
        dayTimer.ConcurrentTriggerProcessing.Should().Be(ConcurrentTriggerProcessing.RunConcurrently);
        dayTimer.SkippedTimeBehavior.Should().Be(SkippedTimeBehavior.RunAfter);
        dayTimer.DuplicateTimeBehavior.Should().Be(DuplicateTimeBehavior.RunFirst);
        signal.Wait(CallbackWaitTimeout, TestContext.Current.CancellationToken).Should().BeTrue();
    }
    //----------------------------------------------------------------------------

    #endregion Local time-of-day — fire and contract

    #region Callback overloads (context, async)

    /// <summary>
    ///   Verifies that a time-of-day timer with context callback receives state and
    ///   registration via <see cref="ClockTimerCallbackContext"/>.
    /// </summary>
    [Fact]
    public void RegisterTimeOfDay_WithContext_CallbackReceivesStateAndRegistration ()
    {
        IPrimeClock clock = new PrimeClock();
        LocalTime target = GetSoonLocalTimeOfDay(clock, ScheduleLeadTime);
        object state = new();
        ManualResetEventSlim signal = new(false);
        object? receivedState = null;
        IClockDayTimeTimer? receivedReg = null;

        using IClockDayTimeTimer timer = clock.RegisterTimeOfDay(target, callbackContext =>
        {
            receivedState = callbackContext.CallbackState;
            receivedReg = (IClockDayTimeTimer)callbackContext.Registration;
            signal.Set();
        }, TestContext.Current.CancellationToken, state);
        signal.Wait(CallbackWaitTimeout, TestContext.Current.CancellationToken).Should().BeTrue();
        receivedState.Should().BeSameAs(state);
        receivedReg.Should().BeSameAs(timer);
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies that <see cref="IPrimeClock.RegisterAsyncTimeOfDay"/> invokes the async callback when virtual
    ///   time reaches the target local time of day, and does not fire again on further advance within the same day.
    /// </summary>
    [Fact]
    public void RegisterAsyncTimeOfDay_LocalTime_WhenAdvanceReachesTarget_CallbackFires ()
    {
        Instant initial = Instant.FromUtc(2025, 6, 15, 10, 0, 0);
        IPrimeTestClock clock = new PrimeTestClock(initial, DateTimeZone.Utc);
        Duration lead = Duration.FromMilliseconds(120);
        LocalTime target = (initial + lead).InZone(DateTimeZone.Utc).LocalDateTime.TimeOfDay;
        int fireCount = 0;

        using IClockDayTimeTimer timer = clock.RegisterAsyncTimeOfDay(target, ct =>
        {
            Interlocked.Increment(ref fireCount);
            return default;
        }, cancellationToken: TestContext.Current.CancellationToken);

        clock.Advance(lead - Duration.FromMilliseconds(1));
        fireCount.Should().Be(0);
        clock.Advance(Duration.FromMilliseconds(1));
        fireCount.Should().Be(1);
        clock.Advance(Duration.FromMinutes(5));
        fireCount.Should().Be(1);
        timer.IsLocalTimeRepresentation.Should().BeTrue();
    }
    //----------------------------------------------------------------------------

    #endregion Callback overloads (context, async)

    #region Change(LocalTime)

    /// <summary>
    ///   Verifies that <see cref="IClockDayTimeTimer.Change(LocalTime)"/> on a
    ///   time-of-day registration updates the target time and reschedules; the callback
    ///   fires near the new time.
    /// </summary>
    [Fact]
    public void RegisterTimeOfDay_ChangeLocalTime_ReschedulesAndFiresAtNewTime ()
    {
        IPrimeClock clock = new PrimeClock();
        LocalTime farTarget = (clock.NowInstant + Duration.FromSeconds(10)).InZone(clock.LocalZonedNowInstant.Zone)
            .LocalDateTime.TimeOfDay;
        ManualResetEventSlim signal = new(false);
        Instant? firedAt = null;

        using IClockDayTimeTimer timer = clock.RegisterTimeOfDay(farTarget, () =>
        {
            firedAt = clock.NowInstant;
            signal.Set();
        }, cancellationToken: TestContext.Current.CancellationToken);
        LocalTime newTarget = GetSoonLocalTimeOfDay(clock, ScheduleLeadTime);
        timer.Change(newTarget).Should().BeTrue();
        Instant afterChange = clock.NowInstant;
        signal.Wait(CallbackWaitTimeout, TestContext.Current.CancellationToken).Should().BeTrue();
        Duration elapsed = firedAt!.Value - afterChange;
        AssertWallClockCallbackElapsedNotBefore(
            elapsed.ToTimeSpan(), ScheduleLeadTime.ToTimeSpan(), TimingTolerance.ToTimeSpan());
    }
    //----------------------------------------------------------------------------

    #endregion Change(LocalTime)

    #region Cancel and options

    /// <summary>
    ///   Verifies that cancelling a time-of-day timer before it fires prevents the callback
    ///   from running and sets state to <see cref="TimerState.Cancelled"/>.
    /// </summary>
    [Fact]
    public void RegisterTimeOfDay_CancelBeforeFire_StateCancelled ()
    {
        IPrimeClock clock = new PrimeClock();
        LocalTime target = (clock.NowInstant + CancelBeforeFireDelay).InZone(clock.LocalZonedNowInstant.Zone)
            .LocalDateTime.TimeOfDay;
        ManualResetEventSlim signal = new(false);

        using IClockDayTimeTimer timer = clock.RegisterTimeOfDay(target, signal.Set,
            cancellationToken: TestContext.Current.CancellationToken);
        timer.Cancel();
        signal.Wait((CancelBeforeFireDelay + WaitMargin).ToTimeSpan(), TestContext.Current.CancellationToken)
            .Should().BeFalse();
        timer.State.Should().Be(TimerState.Cancelled);
        timer.IsCancelled.Should().BeTrue();
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies that registering with default <see cref="DayTimeTimerOptions"/> (null)
    ///   yields the expected default enum values on the registration when cast to
    ///   <see cref="IDayTimeTimer"/>.
    /// </summary>
    [Fact]
    public void RegisterTimeOfDay_WithDefaultOptions_RegistrationExposesDefaultBehaviors ()
    {
        IPrimeClock clock = new PrimeClock();
        LocalTime target = GetSoonLocalTimeOfDay(clock, ScheduleLeadTime);
        ManualResetEventSlim signal = new(false);

        using IClockDayTimeTimer timer = clock.RegisterTimeOfDay(target, signal.Set,
            TestContext.Current.CancellationToken, timerOptions: null);
        IDayTimeTimer dayTimer = (IDayTimeTimer)timer;
        dayTimer.ConcurrentTriggerProcessing.Should().Be(ConcurrentTriggerProcessing.Skip);
        dayTimer.SkippedTimeBehavior.Should().Be(SkippedTimeBehavior.RunAfter);
        dayTimer.DuplicateTimeBehavior.Should().Be(DuplicateTimeBehavior.RunLast);
        signal.Wait(CallbackWaitTimeout, TestContext.Current.CancellationToken).Should().BeTrue();
    }
    //----------------------------------------------------------------------------

    #endregion Cancel and options
}
//################################################################################

