// Copyright (c) Kevin Zehrer
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
//
// Unit tests for IPrimeClock RegisterTimeOfDay and RegisterAsyncTimeOfDay.
// Uses real time; use PrimeTestClock for fully deterministic day-time timer tests.

using System.Diagnostics.CodeAnalysis;

using AwesomeAssertions;
using KZDev.PrimeTime.Tests;
using NodaTime;

namespace KZDev.PrimeTime.UnitTests;

//################################################################################
/// <summary>
///   Unit tests for <see cref="IPrimeClock"/> RegisterTimeOfDay and RegisterAsyncTimeOfDay
///   (day-time timers) and <see cref="IClockDayTimeTimer"/> (Change(LocalTime), options).
/// </summary>
[ExcludeFromCodeCoverage]
public class UsingIPrimeClockDayTimeTimers : UnitTestBase
{
    //----------------------------------------------------------------------------
    /// <summary>
    ///   Short delay from "now" used to compute a time-of-day that fires soon.
    /// </summary>
    private static readonly Duration ShortDelay = Duration.FromMilliseconds(120);

    /// <summary>
    ///   Extra wait time beyond expected delay to avoid flaky failures.
    /// </summary>
    private static readonly Duration WaitMargin = Duration.FromMilliseconds(450);

    /// <summary>
    ///   Allowed tolerance when asserting callback timing.
    /// </summary>
    private static readonly Duration TimingTolerance = Duration.FromMilliseconds(180);

    /// <summary>
    ///   Brief wait after callback to let state settle before assertions.
    /// </summary>
    private static readonly Duration CallbackSettle = Duration.FromMilliseconds(50);
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
        LocalTime targetTime = (clock.NowInstant + ShortDelay).InZone(clock.LocalZonedNow.Zone).LocalDateTime.TimeOfDay;
        ManualResetEventSlim signal = new(false);
        Instant? firedAt = null;

        using IClockDayTimeTimer timer = clock.RegisterTimeOfDay(targetTime, () =>
        {
            firedAt = clock.NowInstant;
            signal.Set();
        }, cancellationToken: TestContext.Current.CancellationToken);
        Instant start = clock.NowInstant;
        signal.Wait(WaitMargin.ToTimeSpan(), TestContext.Current.CancellationToken).Should().BeTrue();
        firedAt.Should().NotBeNull();
        Duration elapsed = firedAt!.Value - start;
        elapsed.Should().BeGreaterThanOrEqualTo(ShortDelay.Minus(TimingTolerance));
        elapsed.Should().BeLessThanOrEqualTo(ShortDelay.Plus(TimingTolerance));
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
        LocalTime target = (clock.NowInstant + ShortDelay).InZone(clock.LocalZonedNow.Zone).LocalDateTime.TimeOfDay;
        ManualResetEventSlim signal = new(false);

        using IClockDayTimeTimer timer = clock.RegisterTimeOfDay(target, () => signal.Set(),
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
        signal.Wait(WaitMargin.ToTimeSpan(), TestContext.Current.CancellationToken).Should().BeTrue();
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
        LocalTime target = (clock.NowInstant + ShortDelay).InZone(clock.LocalZonedNow.Zone).LocalDateTime.TimeOfDay;
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
        signal.Wait(WaitMargin.ToTimeSpan(), TestContext.Current.CancellationToken).Should().BeTrue();
        receivedState.Should().BeSameAs(state);
        receivedReg.Should().BeSameAs(timer);
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies that RegisterAsyncTimeOfDay invokes the async callback and completes.
    /// </summary>
    [Fact]
    public void RegisterAsyncTimeOfDay_LocalTime_CallbackFiresNearTargetTime ()
    {
        IPrimeClock clock = new PrimeClock();
        LocalTime target = (clock.NowInstant + ShortDelay).InZone(clock.LocalZonedNow.Zone).LocalDateTime.TimeOfDay;
        ManualResetEventSlim signal = new(false);
        Instant? firedAt = null;

        using IClockDayTimeTimer timer = clock.RegisterAsyncTimeOfDay(target, ct =>
        {
            firedAt = clock.NowInstant;
            signal.Set();
            return default; // completed ValueTask; use default for .NET Standard 2.0 compatibility where ValueTask.CompletedTask is unavailable

        }, cancellationToken: TestContext.Current.CancellationToken);
        Instant start = clock.NowInstant;
        signal.Wait(WaitMargin.ToTimeSpan(), TestContext.Current.CancellationToken).Should().BeTrue();
        Duration elapsed = firedAt!.Value - start;
        elapsed.Should().BeGreaterThanOrEqualTo(ShortDelay.Minus(TimingTolerance));
        elapsed.Should().BeLessThanOrEqualTo(ShortDelay.Plus(TimingTolerance));
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
        LocalTime farTarget = (clock.NowInstant + Duration.FromSeconds(10)).InZone(clock.LocalZonedNow.Zone)
            .LocalDateTime.TimeOfDay;
        ManualResetEventSlim signal = new(false);
        Instant? firedAt = null;

        using IClockDayTimeTimer timer = clock.RegisterTimeOfDay(farTarget, () =>
        {
            firedAt = clock.NowInstant;
            signal.Set();
        }, cancellationToken: TestContext.Current.CancellationToken);
        LocalTime newTarget = (clock.NowInstant + ShortDelay).InZone(clock.LocalZonedNow.Zone).LocalDateTime.TimeOfDay;
        timer.Change(newTarget).Should().BeTrue();
        Instant afterChange = clock.NowInstant;
        signal.Wait(WaitMargin.ToTimeSpan(), TestContext.Current.CancellationToken).Should().BeTrue();
        Duration elapsed = firedAt!.Value - afterChange;
        elapsed.Should().BeGreaterThanOrEqualTo(ShortDelay.Minus(TimingTolerance));
        elapsed.Should().BeLessThanOrEqualTo(ShortDelay.Plus(TimingTolerance));
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies that <see cref="IClockDayTimeTimer.Change(Duration)"/> on a
    ///   time-of-day registration returns false (not applicable).
    /// </summary>
    [Fact]
    public void RegisterTimeOfDay_ChangeDuration_ReturnsFalse ()
    {
        IPrimeClock clock = new PrimeClock();
        LocalTime target = (clock.NowInstant + ShortDelay).InZone(clock.LocalZonedNow.Zone).LocalDateTime.TimeOfDay;
        ManualResetEventSlim signal = new(false);

        using IClockDayTimeTimer timer = clock.RegisterTimeOfDay(target, () => signal.Set(),
            cancellationToken: TestContext.Current.CancellationToken);
        timer.Change(Duration.FromSeconds(1)).Should().BeFalse();
        signal.Wait(WaitMargin.ToTimeSpan(), TestContext.Current.CancellationToken).Should().BeTrue();
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
        LocalTime target = (clock.NowInstant + Duration.FromSeconds(5)).InZone(clock.LocalZonedNow.Zone)
            .LocalDateTime.TimeOfDay;
        ManualResetEventSlim signal = new(false);

        using IClockDayTimeTimer timer = clock.RegisterTimeOfDay(target, () => signal.Set(),
            cancellationToken: TestContext.Current.CancellationToken);
        timer.Cancel();
        signal.Wait((ShortDelay + CallbackSettle).ToTimeSpan(), TestContext.Current.CancellationToken)
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
        LocalTime target = (clock.NowInstant + ShortDelay).InZone(clock.LocalZonedNow.Zone).LocalDateTime.TimeOfDay;
        ManualResetEventSlim signal = new(false);

        using IClockDayTimeTimer timer = clock.RegisterTimeOfDay(target, () => signal.Set(),
            TestContext.Current.CancellationToken, timerOptions: null);
        IDayTimeTimer dayTimer = (IDayTimeTimer)timer;
        dayTimer.ConcurrentTriggerProcessing.Should().Be(ConcurrentTriggerProcessing.Skip);
        dayTimer.SkippedTimeBehavior.Should().Be(SkippedTimeBehavior.RunAfter);
        dayTimer.DuplicateTimeBehavior.Should().Be(DuplicateTimeBehavior.RunLast);
        signal.Wait(WaitMargin.ToTimeSpan(), TestContext.Current.CancellationToken).Should().BeTrue();
    }
    //----------------------------------------------------------------------------

    #endregion Cancel and options
}
//################################################################################

