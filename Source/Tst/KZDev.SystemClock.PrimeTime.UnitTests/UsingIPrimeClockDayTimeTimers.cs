// Copyright (c) Kevin Zehrer
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

#if NET

using System.Diagnostics.CodeAnalysis;

using AwesomeAssertions;
using KZDev.PrimeTime.Testing;
using KZDev.PrimeTime.Tests;

namespace KZDev.SystemClock.PrimeTime.UnitTests;

/// <summary>
///   Unit tests for <see cref="IPrimeClock"/> RegisterTimeOfDay and RegisterAsyncTimeOfDay
///   (day-time timers) and <see cref="IClockDayTimeTimer"/> (local/UTC, options, Change).
/// </summary>
/// <remarks>
///   Uses wall-clock delays; prefer <see cref="PrimeTestClock"/> for fully deterministic day-time timer tests.
/// </remarks>
[ExcludeFromCodeCoverage]
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
    ///   Wall-clock wait budget for a callback that is expected to fire after <see cref="ShortDelay"/>.
    /// </summary>
    private static readonly TimeSpan CallbackWaitTimeout = GetFirstCallbackWaitTimeout(ShortDelay, WaitMargin);
    /// <summary>
    ///   Upper bound when asserting a callback fired soon after registration (not a synchronization wait).
    /// </summary>
    private static readonly TimeSpan CallbackTimingUpperBound = ShortDelay + WaitMargin;
    /// <summary>
    ///   Allowed tolerance when asserting callback timing.
    /// </summary>
    private static readonly TimeSpan TimingTolerance = TimeSpan.FromMilliseconds(200);
    /// <summary>
    ///   Brief wait after callback to let state settle before assertions.
    /// </summary>
    private static readonly TimeSpan CallbackSettle = TimeSpan.FromMilliseconds(50);

    #region Constructors/Finalizers

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

    #endregion Constructors/Finalizers

    #region Local time-of-day — fire and contract

    /// <summary>
    ///   Verifies that a local time-of-day timer invokes the callback once when the
    ///   target time is reached (time set to a short delay from "now").
    /// </summary>
    [Fact]
    public void RegisterTimeOfDay_LocalTimeOfDay_CallbackFiresNearTargetTime ()
    {
        IPrimeClock clock = new PrimeClock();
        DateTimeOffset now = clock.LocalNowDateTimeOffset;
        TimeOnly targetTime = TimeOnly.FromDateTime((now + ShortDelay).DateTime);
        LocalTimeOfDay timeOfDay = new(targetTime);
        ManualResetEventSlim signal = new(false);
        DateTimeOffset? firedAt = null;

        using IClockDayTimeTimer timer = clock.RegisterTimeOfDay(timeOfDay, () =>
        {
            firedAt = clock.LocalNowDateTimeOffset;
            signal.Set();
        }, cancellationToken: TestContext.Current.CancellationToken);
        DateTimeOffset start = clock.LocalNowDateTimeOffset;
        signal.Wait(CallbackWaitTimeout, TestContext.Current.CancellationToken).Should().BeTrue();
        firedAt.Should().NotBeNull();
        TimeSpan elapsed = firedAt!.Value - start;
        elapsed.Should().BeGreaterThanOrEqualTo(ShortDelay - TimingTolerance);
        elapsed.Should().BeLessThanOrEqualTo(CallbackTimingUpperBound);
    }

    /// <summary>
    ///   Verifies that a local time-of-day timer returns a registration with
    ///   <see cref="IClockTimer.IsTimeOfDay"/> true and correct options.
    /// </summary>
    [Fact]
    public void RegisterTimeOfDay_LocalTimeOfDay_ReturnsTimerWithCorrectContractProperties ()
    {
        IPrimeClock clock = new PrimeClock();
        TimeOnly target = TimeOnly.FromDateTime((clock.LocalNowDateTimeOffset + ShortDelay).DateTime);
        LocalTimeOfDay timeOfDay = new(target);
        ManualResetEventSlim signal = new(false);

        using IClockDayTimeTimer timer = clock.RegisterTimeOfDay(timeOfDay, signal.Set,
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
        signal.Wait(CallbackWaitTimeout, TestContext.Current.CancellationToken).Should().BeTrue();
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
        DateTimeOffset now = clock.UtcNowDateTimeOffset;
        TimeOnly targetTime = TimeOnly.FromDateTime((now + ShortDelay).UtcDateTime);
        UtcTimeOfDay timeOfDay = new(targetTime);
        ManualResetEventSlim signal = new(false);
        DateTimeOffset? firedAt = null;

        using IClockDayTimeTimer timer = clock.RegisterTimeOfDay(timeOfDay, () =>
        {
            firedAt = clock.UtcNowDateTimeOffset;
            signal.Set();
        }, cancellationToken: TestContext.Current.CancellationToken);
        DateTimeOffset start = clock.UtcNowDateTimeOffset;
        signal.Wait(CallbackWaitTimeout, TestContext.Current.CancellationToken).Should().BeTrue();
        firedAt.Should().NotBeNull();
        (firedAt!.Value - start).Should().BeCloseTo(ShortDelay, TimingTolerance);
    }

    /// <summary>
    ///   Verifies that a UTC time-of-day timer returns a registration with
    ///   <see cref="IClockTimer.IsLocalTimeRepresentation"/> false.
    /// </summary>
    [Fact]
    public void RegisterTimeOfDay_UtcTimeOfDay_IsLocalTimeRepresentationFalse ()
    {
        IPrimeClock clock = new PrimeClock();
        TimeOnly target = TimeOnly.FromDateTime((clock.UtcNowDateTimeOffset + ShortDelay).UtcDateTime);
        UtcTimeOfDay timeOfDay = new(target);
        ManualResetEventSlim signal = new(false);

        using IClockDayTimeTimer timer = clock.RegisterTimeOfDay(timeOfDay, signal.Set,
            cancellationToken: TestContext.Current.CancellationToken);
        timer.IsLocalTimeRepresentation.Should().BeFalse();
        signal.Wait(CallbackWaitTimeout, TestContext.Current.CancellationToken).Should().BeTrue();
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
        TimeOnly target = TimeOnly.FromDateTime((clock.LocalNowDateTimeOffset + ShortDelay).DateTime);
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
        signal.Wait(CallbackWaitTimeout, TestContext.Current.CancellationToken).Should().BeTrue();
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
        TimeOnly target = TimeOnly.FromDateTime((clock.UtcNowDateTimeOffset + ShortDelay).UtcDateTime);
        UtcTimeOfDay timeOfDay = new(target);
        ManualResetEventSlim signal = new(false);
        DateTimeOffset? firedAt = null;

        using IClockDayTimeTimer timer = clock.RegisterAsyncTimeOfDay(timeOfDay, ct =>
        {
            firedAt = clock.UtcNowDateTimeOffset;
            signal.Set();
            return default;
        }, cancellationToken: TestContext.Current.CancellationToken);
        DateTimeOffset start = clock.UtcNowDateTimeOffset;
        signal.Wait(CallbackWaitTimeout, TestContext.Current.CancellationToken).Should().BeTrue();
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
        TimeOnly farTarget = TimeOnly.FromDateTime((clock.LocalNowDateTimeOffset + TimeSpan.FromSeconds(10)).DateTime);
        LocalTimeOfDay initial = new(farTarget);
        ManualResetEventSlim signal = new(false);
        DateTimeOffset? firedAt = null;

        using IClockDayTimeTimer timer = clock.RegisterTimeOfDay(initial, () =>
        {
            firedAt = clock.LocalNowDateTimeOffset;
            signal.Set();
        }, cancellationToken: TestContext.Current.CancellationToken);
        TimeOnly newTarget = TimeOnly.FromDateTime((clock.LocalNowDateTimeOffset + ShortDelay).DateTime);
        timer.Change(new LocalTimeOfDay(newTarget)).Should().BeTrue();
        DateTimeOffset afterChange = clock.LocalNowDateTimeOffset;
        signal.Wait(CallbackWaitTimeout, TestContext.Current.CancellationToken).Should().BeTrue();
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
        TimeOnly target = TimeOnly.FromDateTime((clock.LocalNowDateTimeOffset + ShortDelay).DateTime);
        LocalTimeOfDay timeOfDay = new(target);
        ManualResetEventSlim signal = new(false);

        using IClockDayTimeTimer timer = clock.RegisterTimeOfDay(timeOfDay, signal.Set,
            cancellationToken: TestContext.Current.CancellationToken);
        timer.Change(new UtcTimeOfDay(new TimeOnly(0, 0, 0))).Should().BeFalse();
        signal.Wait(CallbackWaitTimeout, TestContext.Current.CancellationToken).Should().BeTrue();
    }

    /// <summary>
    ///   Verifies that <see cref="IClockDayTimeTimer.Change(LocalTimeOfDay)"/> on a UTC
    ///   registration returns false (wrong kind).
    /// </summary>
    [Fact]
    public void RegisterTimeOfDay_Utc_ChangeLocalTimeOfDay_ReturnsFalse ()
    {
        IPrimeClock clock = new PrimeClock();
        TimeOnly target = TimeOnly.FromDateTime((clock.UtcNowDateTimeOffset + ShortDelay).UtcDateTime);
        UtcTimeOfDay timeOfDay = new(target);
        ManualResetEventSlim signal = new(false);

        using IClockDayTimeTimer timer = clock.RegisterTimeOfDay(timeOfDay, signal.Set,
            cancellationToken: TestContext.Current.CancellationToken);
        timer.Change(new LocalTimeOfDay(new TimeOnly(0, 0, 0))).Should().BeFalse();
        signal.Wait(CallbackWaitTimeout, TestContext.Current.CancellationToken).Should().BeTrue();
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
        TimeOnly target = TimeOnly.FromDateTime((clock.LocalNowDateTimeOffset + TimeSpan.FromSeconds(5)).DateTime);
        LocalTimeOfDay timeOfDay = new(target);
        ManualResetEventSlim signal = new(false);

        using IClockDayTimeTimer timer = clock.RegisterTimeOfDay(timeOfDay, signal.Set,
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
        TimeOnly target = TimeOnly.FromDateTime((clock.UtcNowDateTimeOffset + ShortDelay).UtcDateTime);
        UtcTimeOfDay timeOfDay = new(target);
        ManualResetEventSlim signal = new(false);

        using IClockDayTimeTimer timer = clock.RegisterTimeOfDay(timeOfDay, signal.Set,
            TestContext.Current.CancellationToken, timerOptions: null);
        timer.ConcurrentTriggerProcessing.Should().Be(ConcurrentTriggerProcessing.Skip);
        timer.SkippedTimeBehavior.Should().Be(SkippedTimeBehavior.RunAfter);
        timer.DuplicateTimeBehavior.Should().Be(DuplicateTimeBehavior.RunLast);
        signal.Wait(CallbackWaitTimeout, TestContext.Current.CancellationToken).Should().BeTrue();
    }

    #endregion Cancel and options
}
#endif

