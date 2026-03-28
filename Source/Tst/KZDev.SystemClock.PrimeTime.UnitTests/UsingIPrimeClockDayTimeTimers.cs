// Copyright (c) Kevin Zehrer
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
//
// Unit tests for IPrimeClock RegisterTimeOfDay and RegisterAsyncTimeOfDay.
// Uses real time; use PrimeTestClock for fully deterministic day-time timer tests.

#if NET

using AwesomeAssertions;
using KZDev.PrimeTime;
using KZDev.PrimeTime.Tests;

namespace KZDev.SystemClock.PrimeTime.UnitTests;

/// <summary>
///   Unit tests for <see cref="IPrimeClock"/> RegisterTimeOfDay and RegisterAsyncTimeOfDay
///   (day-time timers) and <see cref="IClockDayTimeTimer"/> (local/UTC, options, Change).
/// </summary>
public class UsingIPrimeClockDayTimeTimers : UnitTestBase
{
    /// <summary>
    ///   Short delay from "now" used to compute a time-of-day that fires soon (milliseconds).
    /// </summary>
    private static readonly TimeSpan ShortDelay = TimeSpan.FromMilliseconds(120);
    /// <summary>
    ///   Extra wait time beyond expected delay to avoid flaky failures.
    /// </summary>
    private static readonly TimeSpan WaitMargin = TimeSpan.FromMilliseconds(450);
    /// <summary>
    ///   Allowed tolerance when asserting callback timing.
    /// </summary>
    private static readonly TimeSpan TimingTolerance = TimeSpan.FromMilliseconds(180);
    /// <summary>
    ///   Brief wait after callback to let state settle before assertions.
    /// </summary>
    private static readonly TimeSpan CallbackSettle = TimeSpan.FromMilliseconds(50);

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

    #region Local time-of-day — fire and contract

    /// <summary>
    ///   Verifies that a local time-of-day timer invokes the callback once when the
    ///   target time is reached (time set to a short delay from "now").
    /// </summary>
    [Fact]
    public void RegisterTimeOfDay_LocalTimeOfDay_CallbackFiresNearTargetTime ()
    {
        IPrimeClock clock = new PrimeClock();
        DateTimeOffset now = clock.LocalNowOffset;
        TimeOnly targetTime = TimeOnly.FromDateTime((now + ShortDelay).DateTime);
        LocalTimeOfDay timeOfDay = new(targetTime);
        ManualResetEventSlim signal = new(false);
        DateTimeOffset? firedAt = null;

        using IClockDayTimeTimer timer = clock.RegisterTimeOfDay(timeOfDay, () =>
        {
            firedAt = clock.LocalNowOffset;
            signal.Set();
        }, cancellationToken: TestContext.Current.CancellationToken);
        DateTimeOffset start = clock.LocalNowOffset;
        signal.Wait(WaitMargin, TestContext.Current.CancellationToken).Should().BeTrue();
        firedAt.Should().NotBeNull();
        (firedAt!.Value - start).Should().BeCloseTo(ShortDelay, TimingTolerance);
    }

    /// <summary>
    ///   Verifies that a local time-of-day timer returns a registration with
    ///   <see cref="IRegisteredTimer.IsTimeOfDay"/> true and correct options.
    /// </summary>
    [Fact]
    public void RegisterTimeOfDay_LocalTimeOfDay_ReturnsTimerWithCorrectContractProperties ()
    {
        IPrimeClock clock = new PrimeClock();
        TimeOnly target = TimeOnly.FromDateTime((clock.LocalNowOffset + ShortDelay).DateTime);
        LocalTimeOfDay timeOfDay = new(target);
        ManualResetEventSlim signal = new(false);

        using IClockDayTimeTimer timer = clock.RegisterTimeOfDay(timeOfDay, () => signal.Set(),
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
        timer.ConcurrentTriggerProcessing.Should().Be(ConcurrentTriggerProcessing.RunConcurrently);
        timer.SkippedTimeBehavior.Should().Be(SkippedTimeBehavior.RunAfter);
        timer.DuplicateTimeBehavior.Should().Be(DuplicateTimeBehavior.RunFirst);
        signal.Wait(WaitMargin, TestContext.Current.CancellationToken).Should().BeTrue();
    }

    #endregion Local time-of-day — fire and contract

    #region UTC time-of-day — fire and contract

    /// <summary>
    ///   Verifies that a UTC time-of-day timer invokes the callback once when the
    ///   target UTC time is reached.
    /// </summary>
    [Fact]
    public void RegisterTimeOfDay_UtcTimeOfDay_CallbackFiresNearTargetTime ()
    {
        IPrimeClock clock = new PrimeClock();
        DateTimeOffset now = clock.UtcNowOffset;
        TimeOnly targetTime = TimeOnly.FromDateTime((now + ShortDelay).UtcDateTime);
        UtcTimeOfDay timeOfDay = new(targetTime);
        ManualResetEventSlim signal = new(false);
        DateTimeOffset? firedAt = null;

        using IClockDayTimeTimer timer = clock.RegisterTimeOfDay(timeOfDay, () =>
        {
            firedAt = clock.UtcNowOffset;
            signal.Set();
        }, cancellationToken: TestContext.Current.CancellationToken);
        DateTimeOffset start = clock.UtcNowOffset;
        signal.Wait(WaitMargin, TestContext.Current.CancellationToken).Should().BeTrue();
        firedAt.Should().NotBeNull();
        (firedAt!.Value - start).Should().BeCloseTo(ShortDelay, TimingTolerance);
    }

    /// <summary>
    ///   Verifies that a UTC time-of-day timer returns a registration with
    ///   <see cref="IRegisteredTimer.IsLocalTimeRepresentation"/> false.
    /// </summary>
    [Fact]
    public void RegisterTimeOfDay_UtcTimeOfDay_IsLocalTimeRepresentationFalse ()
    {
        IPrimeClock clock = new PrimeClock();
        TimeOnly target = TimeOnly.FromDateTime((clock.UtcNowOffset + ShortDelay).UtcDateTime);
        UtcTimeOfDay timeOfDay = new(target);
        ManualResetEventSlim signal = new(false);

        using IClockDayTimeTimer timer = clock.RegisterTimeOfDay(timeOfDay, () => signal.Set(),
            cancellationToken: TestContext.Current.CancellationToken);
        timer.IsLocalTimeRepresentation.Should().BeFalse();
        signal.Wait(WaitMargin, TestContext.Current.CancellationToken).Should().BeTrue();
    }

    #endregion UTC time-of-day — fire and contract

    #region Callback overloads (context, async)

    /// <summary>
    ///   Verifies that a local time-of-day timer with context callback receives state and
    ///   registration via <see cref="ClockTimerCallbackContext"/>.
    /// </summary>
    [Fact]
    public void RegisterTimeOfDay_LocalWithContext_CallbackReceivesStateAndRegistration ()
    {
        IPrimeClock clock = new PrimeClock();
        TimeOnly target = TimeOnly.FromDateTime((clock.LocalNowOffset + ShortDelay).DateTime);
        LocalTimeOfDay timeOfDay = new(target);
        object state = new();
        ManualResetEventSlim signal = new(false);
        object? receivedState = null;
        IClockTimer? receivedReg = null;

        using IClockDayTimeTimer timer = clock.RegisterTimeOfDay(timeOfDay, callbackContext =>
        {
            receivedState = callbackContext.CallbackState;
            receivedReg = callbackContext.Registration;
            signal.Set();
        }, TestContext.Current.CancellationToken, state);
        signal.Wait(WaitMargin, TestContext.Current.CancellationToken).Should().BeTrue();
        receivedState.Should().BeSameAs(state);
        receivedReg.Should().BeSameAs(timer);
    }

    /// <summary>
    ///   Verifies that a UTC time-of-day async timer invokes the callback and completes.
    /// </summary>
    [Fact]
    public void RegisterAsyncTimeOfDay_UtcTimeOfDay_ValueTaskCompletesAndCallbackFires ()
    {
        IPrimeClock clock = new PrimeClock();
        TimeOnly target = TimeOnly.FromDateTime((clock.UtcNowOffset + ShortDelay).UtcDateTime);
        UtcTimeOfDay timeOfDay = new(target);
        ManualResetEventSlim signal = new(false);
        DateTimeOffset? firedAt = null;

        using IClockDayTimeTimer timer = clock.RegisterAsyncTimeOfDay(timeOfDay, ct =>
        {
            firedAt = clock.UtcNowOffset;
            signal.Set();
            return default;
        }, cancellationToken: TestContext.Current.CancellationToken);
        DateTimeOffset start = clock.UtcNowOffset;
        signal.Wait(WaitMargin, TestContext.Current.CancellationToken).Should().BeTrue();
        (firedAt!.Value - start).Should().BeCloseTo(ShortDelay, TimingTolerance);
    }

    #endregion Callback overloads (context, async)

    #region Change(LocalTimeOfDay) / Change(UtcTimeOfDay)

    /// <summary>
    ///   Verifies that <see cref="IClockDayTimeTimer.Change(LocalTimeOfDay)"/> on a local
    ///   registration updates the target time and reschedules; the callback fires near the new time.
    /// </summary>
    [Fact]
    public void RegisterTimeOfDay_Local_ChangeLocalTimeOfDay_ReschedulesAndFiresAtNewTime ()
    {
        IPrimeClock clock = new PrimeClock();
        TimeOnly farTarget = TimeOnly.FromDateTime((clock.LocalNowOffset + TimeSpan.FromSeconds(10)).DateTime);
        LocalTimeOfDay initial = new(farTarget);
        ManualResetEventSlim signal = new(false);
        DateTimeOffset? firedAt = null;

        using IClockDayTimeTimer timer = clock.RegisterTimeOfDay(initial, () =>
        {
            firedAt = clock.LocalNowOffset;
            signal.Set();
        }, cancellationToken: TestContext.Current.CancellationToken);
        TimeOnly newTarget = TimeOnly.FromDateTime((clock.LocalNowOffset + ShortDelay).DateTime);
        timer.Change(new LocalTimeOfDay(newTarget)).Should().BeTrue();
        DateTimeOffset afterChange = clock.LocalNowOffset;
        signal.Wait(WaitMargin, TestContext.Current.CancellationToken).Should().BeTrue();
        (firedAt!.Value - afterChange).Should().BeCloseTo(ShortDelay, TimingTolerance);
    }

    /// <summary>
    ///   Verifies that <see cref="IClockDayTimeTimer.Change(UtcTimeOfDay)"/> on a local
    ///   registration returns false (wrong kind).
    /// </summary>
    [Fact]
    public void RegisterTimeOfDay_Local_ChangeUtcTimeOfDay_ReturnsFalse ()
    {
        IPrimeClock clock = new PrimeClock();
        TimeOnly target = TimeOnly.FromDateTime((clock.LocalNowOffset + ShortDelay).DateTime);
        LocalTimeOfDay timeOfDay = new(target);
        ManualResetEventSlim signal = new(false);

        using IClockDayTimeTimer timer = clock.RegisterTimeOfDay(timeOfDay, () => signal.Set(),
            cancellationToken: TestContext.Current.CancellationToken);
        timer.Change(new UtcTimeOfDay(new TimeOnly(0, 0, 0))).Should().BeFalse();
        signal.Wait(WaitMargin, TestContext.Current.CancellationToken).Should().BeTrue();
    }

    /// <summary>
    ///   Verifies that <see cref="IClockDayTimeTimer.Change(LocalTimeOfDay)"/> on a UTC
    ///   registration returns false (wrong kind).
    /// </summary>
    [Fact]
    public void RegisterTimeOfDay_Utc_ChangeLocalTimeOfDay_ReturnsFalse ()
    {
        IPrimeClock clock = new PrimeClock();
        TimeOnly target = TimeOnly.FromDateTime((clock.UtcNowOffset + ShortDelay).UtcDateTime);
        UtcTimeOfDay timeOfDay = new(target);
        ManualResetEventSlim signal = new(false);

        using IClockDayTimeTimer timer = clock.RegisterTimeOfDay(timeOfDay, () => signal.Set(),
            cancellationToken: TestContext.Current.CancellationToken);
        timer.Change(new LocalTimeOfDay(new TimeOnly(0, 0, 0))).Should().BeFalse();
        signal.Wait(WaitMargin, TestContext.Current.CancellationToken).Should().BeTrue();
    }

    #endregion Change(LocalTimeOfDay) / Change(UtcTimeOfDay)

    #region Cancel and options

    /// <summary>
    ///   Verifies that cancelling a time-of-day timer before it fires prevents the callback
    ///   from running and sets state to <see cref="TimerState.Cancelled"/>.
    /// </summary>
    [Fact]
    public void RegisterTimeOfDay_Local_CancelBeforeFire_StateCancelled ()
    {
        IPrimeClock clock = new PrimeClock();
        TimeOnly target = TimeOnly.FromDateTime((clock.LocalNowOffset + TimeSpan.FromSeconds(5)).DateTime);
        LocalTimeOfDay timeOfDay = new(target);
        ManualResetEventSlim signal = new(false);

        using IClockDayTimeTimer timer = clock.RegisterTimeOfDay(timeOfDay, () => signal.Set(),
            cancellationToken: TestContext.Current.CancellationToken);
        timer.Cancel();
        signal.Wait(ShortDelay + CallbackSettle, TestContext.Current.CancellationToken).Should().BeFalse();
        timer.State.Should().Be(TimerState.Cancelled);
        timer.IsCancelled.Should().BeTrue();
    }

    /// <summary>
    ///   Verifies that registering with <see cref="DayTimeTimerOptions"/> default options
    ///   yields the expected default enum values on the registration.
    /// </summary>
    [Fact]
    public void RegisterTimeOfDay_WithDefaultOptions_RegistrationExposesDefaultBehaviors ()
    {
        IPrimeClock clock = new PrimeClock();
        TimeOnly target = TimeOnly.FromDateTime((clock.UtcNowOffset + ShortDelay).UtcDateTime);
        UtcTimeOfDay timeOfDay = new(target);
        ManualResetEventSlim signal = new(false);

        using IClockDayTimeTimer timer = clock.RegisterTimeOfDay(timeOfDay, () => signal.Set(),
            TestContext.Current.CancellationToken, timerOptions: null);
        timer.ConcurrentTriggerProcessing.Should().Be(ConcurrentTriggerProcessing.Skip);
        timer.SkippedTimeBehavior.Should().Be(SkippedTimeBehavior.RunAfter);
        timer.DuplicateTimeBehavior.Should().Be(DuplicateTimeBehavior.RunLast);
        signal.Wait(WaitMargin, TestContext.Current.CancellationToken).Should().BeTrue();
    }

    #endregion Cancel and options
}

#endif

