// Copyright (c) Kevin Zehrer
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

#if NET

using System.Diagnostics.CodeAnalysis;

using AwesomeAssertions;
using KZDev.PrimeTime.Tests;

namespace KZDev.SystemClock.PrimeTime.UnitTests;

/// <summary>
///   Unit tests for <see cref="IPrimeClock"/> RegisterTimeOfDay and RegisterAsyncTimeOfDay
///   (day-time timers) and <see cref="IClockDayTimeTimer"/> (local/UTC, options, Change).
/// </summary>
/// <remarks>
///   Uses wall-clock delays for end-to-end wiring; timing precision is asserted only as a lower bound
///   (callback not before the scheduled delay). Upper-bound timing and schedule math are covered by
///   <see cref="PrimeTestClock"/> and <c>FakeTimeProvider</c> tests.
/// </remarks>
[ExcludeFromCodeCoverage]
public class UsingIPrimeClockDayTimeTimers : UnitTestBase
{
    /// <summary>
    ///   Lead time when computing a time-of-day target for wall-clock tests. Must exceed worst-case
    ///   registration delay on CI so the next occurrence stays on today's calendar date.
    /// </summary>
    private static readonly TimeSpan ScheduleLeadTime = TimeSpan.FromSeconds(1);
    /// <summary>
    ///   Extra wait time beyond expected delay to avoid flaky failures.
    /// </summary>
    private static readonly TimeSpan WaitMargin = TimeSpan.FromMilliseconds(450);
    /// <summary>
    ///   Wall-clock wait budget for a callback that is expected to fire after <see cref="ScheduleLeadTime"/>.
    /// </summary>
    private static readonly TimeSpan CallbackWaitTimeout = GetFirstCallbackWaitTimeout(ScheduleLeadTime, WaitMargin);
    /// <summary>
    ///   Allowed shortfall when asserting callback timing (wall-clock lower bound only).
    /// </summary>
    private static readonly TimeSpan TimingTolerance = TimeSpan.FromMilliseconds(200);
    /// <summary>
    ///   Brief wait after callback to let state settle before assertions.
    /// </summary>
    private static readonly TimeSpan CallbackSettle = TimeSpan.FromMilliseconds(50);

    /// <summary>
    ///   How far ahead the cancel-before-fire test schedules its target time-of-day.
    /// </summary>
    private static readonly TimeSpan CancelBeforeFireDelay = TimeSpan.FromSeconds(5);

    /// <summary>
    ///   Minimum remaining time on the current calendar day required before placing a soon target via
    ///   <see cref="GetSoonTimeOfDayFromOffset"/>.
    /// </summary>
    private static readonly TimeSpan DayEndSafetyMargin = TimeSpan.FromSeconds(2);

    /// <summary>
    ///   Returns a <see cref="TimeOnly"/> on the current calendar day that is about <paramref name="leadTime"/>
    ///   after <paramref name="now"/>, clamped before local/UTC day rollover so schedulers do not arm tomorrow.
    /// </summary>
    /// <param name="now">Current instant in the schedule basis (local or UTC).</param>
    /// <param name="leadTime">Desired lead before the next fire on today's calendar date.</param>
    /// <param name="useUtcDateTime">
    ///   <c>true</c> to interpret <paramref name="now"/> and the result in UTC; otherwise local.
    /// </param>
    /// <returns>A time-of-day on today's calendar date strictly after <paramref name="now"/>.</returns>
    private static TimeOnly GetSoonTimeOfDayFromOffset (DateTimeOffset now, TimeSpan leadTime, bool useUtcDateTime)
    {
        DateTimeOffset fireAt = now + leadTime;
        DateTime nowDate = useUtcDateTime ? now.UtcDateTime : now.DateTime;
        if (fireAt.Date == now.Date)
        {
            return TimeOnly.FromDateTime(useUtcDateTime ? fireAt.UtcDateTime : fireAt.DateTime);
        }

        TimeSpan remainingToday = TimeSpan.FromDays(1) - nowDate.TimeOfDay;
        TimeSpan clampedLead = remainingToday - DayEndSafetyMargin;
        if (clampedLead <= TimeSpan.Zero)
        {
            clampedLead = TimeSpan.FromTicks(remainingToday.Ticks / 2);
            if (clampedLead <= TimeSpan.Zero)
                clampedLead = TimeSpan.FromMilliseconds(100);
        }

        DateTimeOffset clampedFireAt = now + clampedLead;
        return TimeOnly.FromDateTime(useUtcDateTime ? clampedFireAt.UtcDateTime : clampedFireAt.DateTime);
    }

    /// <summary>
    ///   Returns a local <see cref="TimeOnly"/> that is <paramref name="leadTime"/> after the clock's current local time.
    /// </summary>
    /// <param name="clock">Clock used to read the current local time.</param>
    /// <param name="leadTime">How far ahead to place the target on today's local calendar.</param>
    /// <returns>The local time-of-day for the next timer registration.</returns>
    private static TimeOnly GetSoonLocalTimeOfDay (IPrimeClock clock, TimeSpan leadTime) =>
        GetSoonTimeOfDayFromOffset(clock.LocalNowDateTimeOffset, leadTime, useUtcDateTime: false);

    /// <summary>
    ///   Returns a UTC <see cref="TimeOnly"/> that is <paramref name="leadTime"/> after the clock's current UTC time.
    /// </summary>
    /// <param name="clock">Clock used to read the current UTC time.</param>
    /// <param name="leadTime">How far ahead to place the target on today's UTC calendar.</param>
    /// <returns>The UTC time-of-day for the next timer registration.</returns>
    private static TimeOnly GetSoonUtcTimeOfDay (IPrimeClock clock, TimeSpan leadTime) =>
        GetSoonTimeOfDayFromOffset(clock.UtcNowDateTimeOffset, leadTime, useUtcDateTime: true);

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
        TimeOnly targetTime = GetSoonLocalTimeOfDay(clock, ScheduleLeadTime);
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
        AssertWallClockCallbackElapsedNotBefore(firedAt!.Value - start, ScheduleLeadTime, TimingTolerance);
    }

    /// <summary>
    ///   Verifies that a local time-of-day timer returns a registration with
    ///   <see cref="IClockTimer.IsTimeOfDay"/> true and correct options.
    /// </summary>
    [Fact]
    public void RegisterTimeOfDay_LocalTimeOfDay_ReturnsTimerWithCorrectContractProperties ()
    {
        IPrimeClock clock = new PrimeClock();
        TimeOnly target = GetSoonLocalTimeOfDay(clock, ScheduleLeadTime);
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
        TimeOnly targetTime = GetSoonUtcTimeOfDay(clock, ScheduleLeadTime);
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
        AssertWallClockCallbackElapsedNotBefore(firedAt!.Value - start, ScheduleLeadTime, TimingTolerance);
    }

    /// <summary>
    ///   Verifies that a UTC time-of-day timer returns a registration with
    ///   <see cref="IClockTimer.IsLocalTimeRepresentation"/> false.
    /// </summary>
    [Fact]
    public void RegisterTimeOfDay_UtcTimeOfDay_IsLocalTimeRepresentationFalse ()
    {
        IPrimeClock clock = new PrimeClock();
        TimeOnly target = GetSoonUtcTimeOfDay(clock, ScheduleLeadTime);
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
        TimeOnly target = GetSoonLocalTimeOfDay(clock, ScheduleLeadTime);
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
        UtcTimeOfDay timeOfDay = new(GetSoonUtcTimeOfDay(clock, ScheduleLeadTime));
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
        AssertWallClockCallbackElapsedNotBefore(firedAt!.Value - start, ScheduleLeadTime, TimingTolerance);
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
        TimeOnly newTarget = GetSoonLocalTimeOfDay(clock, ScheduleLeadTime);
        timer.Change(new LocalTimeOfDay(newTarget)).Should().BeTrue();
        DateTimeOffset afterChange = clock.LocalNowDateTimeOffset;
        signal.Wait(CallbackWaitTimeout, TestContext.Current.CancellationToken).Should().BeTrue();
        AssertWallClockCallbackElapsedNotBefore(firedAt!.Value - afterChange, ScheduleLeadTime, TimingTolerance);
    }

    /// <summary>
    ///   Verifies that <see cref="IClockDayTimeTimer.Change(UtcTimeOfDay)"/> on a local
    ///   registration returns false (wrong kind).
    /// </summary>
    [Fact]
    public void RegisterTimeOfDay_Local_ChangeUtcTimeOfDay_ReturnsFalse ()
    {
        IPrimeClock clock = new PrimeClock();
        TimeOnly target = GetSoonLocalTimeOfDay(clock, ScheduleLeadTime);
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
        TimeOnly target = GetSoonUtcTimeOfDay(clock, ScheduleLeadTime);
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
        TimeOnly target = TimeOnly.FromDateTime((clock.LocalNowDateTimeOffset + CancelBeforeFireDelay).DateTime);
        LocalTimeOfDay timeOfDay = new(target);
        ManualResetEventSlim signal = new(false);

        using IClockDayTimeTimer timer = clock.RegisterTimeOfDay(timeOfDay, signal.Set,
            cancellationToken: TestContext.Current.CancellationToken);
        timer.Cancel();
        signal.Wait(CancelBeforeFireDelay + WaitMargin, TestContext.Current.CancellationToken).Should().BeFalse();
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
        TimeOnly target = GetSoonUtcTimeOfDay(clock, ScheduleLeadTime);
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

