// Copyright (c) Kevin Zehrer
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
//
// Unit tests for IPrimeSystemClock interval timers (Phase 8). Uses real time; full determinism
// comes with PrimeTestSystemClock (Phase 12).

using AwesomeAssertions;
using KZDev.PrimeTime.Tests;

namespace KZDev.PrimeTime.SystemClock.UnitTests;

/// <summary>
///   Unit tests for <see cref="IPrimeSystemClock"/> RegisterTimer and RegisterAsyncTimer
///   (interval timers) and <see cref="IClockIntervalTimer"/> (one-shot, repeating, Change, Unsafe).
/// </summary>
public class UsingIPrimeSystemClockIntervalTimers : UnitTestBase
{
    /// <summary>
    ///   Short initial delay used by many tests (milliseconds).
    /// </summary>
    private static readonly TimeSpan ShortDelay = TimeSpan.FromMilliseconds(80);
    /// <summary>
    ///   Repeat interval for repeating-timer tests (milliseconds).
    /// </summary>
    private static readonly TimeSpan RepeatInterval = TimeSpan.FromMilliseconds(60);
    /// <summary>
    ///   Extra wait time beyond expected delay to avoid flaky failures.
    /// </summary>
    private static readonly TimeSpan WaitMargin = TimeSpan.FromMilliseconds(400);
    /// <summary>
    ///   Allowed tolerance when asserting callback timing.
    /// </summary>
    private static readonly TimeSpan TimingTolerance = TimeSpan.FromMilliseconds(150);
    /// <summary>
    ///   Brief wait after callback to let state settle before assertions.
    /// </summary>
    private static readonly TimeSpan CallbackSettle = TimeSpan.FromMilliseconds(50);

    /// <summary>
    ///   Initializes a new instance of the <see cref="UsingIPrimeSystemClockIntervalTimers"/> class.
    /// </summary>
    /// <param name="xUnitTestOutputHelper">
    ///   The xUnit test output helper for diagnostic output.
    /// </param>
    public UsingIPrimeSystemClockIntervalTimers (ITestOutputHelper xUnitTestOutputHelper)
        : base(xUnitTestOutputHelper)
    {
    }

    #region One-shot

    /// <summary>
    ///   Verifies that a one-shot timer invokes the callback once and reaches
    ///   <see cref="TimerState.Completed"/> with timing close to the initial delay.
    /// </summary>
    [Fact]
    public void RegisterTimer_OneShot_CallbackCalledOnceAndStateCompleted ()
    {
        IPrimeSystemClock clock = new PrimeSystemClock();
        ManualResetEventSlim signal = new(false);
        DateTimeOffset? firedAt = null;

        using IClockIntervalTimer timer = clock.RegisterTimer(ShortDelay, () =>
        {
            firedAt = clock.UtcNow;
            signal.Set();
        }, cancellationToken: TestContext.Current.CancellationToken);
        DateTimeOffset start = clock.UtcNow;
        signal.Wait(WaitMargin, TestContext.Current.CancellationToken).Should().BeTrue();
        timer.State.Should().Be(TimerState.Completed);
        firedAt.Should().NotBeNull();
        (firedAt!.Value - start).Should().BeCloseTo(ShortDelay, TimingTolerance);
    }

    /// <summary>
    ///   Verifies that a one-shot timer with state passes the state and registration
    ///   (<see cref="IClockTimer"/>) to the callback via <see cref="ClockTimerCallbackContext"/>.
    /// </summary>
    [Fact]
    public void RegisterTimer_OneShotWithContext_CallbackReceivesStateAndRegistration ()
    {
        IPrimeSystemClock clock = new PrimeSystemClock();
        object state = new();
        ManualResetEventSlim signal = new(false);
        object? receivedState = null;
        IClockTimer? receivedReg = null;

        using IClockIntervalTimer timer = clock.RegisterTimer(ShortDelay, ctx =>
        {
            receivedState = ctx.CallbackState;
            receivedReg = ctx.Registration;
            signal.Set();
        }, state, cancellationToken: TestContext.Current.CancellationToken);
        signal.Wait(WaitMargin, TestContext.Current.CancellationToken).Should().BeTrue();
        receivedState.Should().BeSameAs(state);
        receivedReg.Should().BeSameAs(timer);
    }

    /// <summary>
    ///   Verifies that when a one-shot timer is registered with an already cancelled
    ///   <see cref="CancellationToken"/>, the timer never fires and state is
    ///   <see cref="TimerState.Cancelled"/>.
    /// </summary>
    [Fact]
    public void RegisterTimer_OneShotWithCancelledToken_StateCancelled ()
    {
        IPrimeSystemClock clock = new PrimeSystemClock();
        ManualResetEventSlim signal = new(false);
        using CancellationTokenSource cts = new();
        cts.Cancel();

        using IClockIntervalTimer timer = clock.RegisterTimer(ShortDelay, () => signal.Set(),
            timerOptions: null, cancellationToken: cts.Token);
        signal.Wait(ShortDelay + CallbackSettle, TestContext.Current.CancellationToken).Should().BeFalse();
        timer.State.Should().Be(TimerState.Cancelled);
        timer.IsCancelled.Should().BeTrue();
    }

    /// <summary>
    ///   Verifies that cancelling a one-shot timer before it fires prevents the callback
    ///   from running and sets state to <see cref="TimerState.Cancelled"/>.
    /// </summary>
    [Fact]
    public void RegisterTimer_OneShot_CancelBeforeFire_StateCancelled ()
    {
        IPrimeSystemClock clock = new PrimeSystemClock();
        ManualResetEventSlim signal = new(false);
        using IClockIntervalTimer timer = clock.RegisterTimer(ShortDelay + TimeSpan.FromMilliseconds(200),
            () => signal.Set(), cancellationToken: TestContext.Current.CancellationToken);
        timer.Cancel();
        signal.Wait(ShortDelay + WaitMargin, TestContext.Current.CancellationToken).Should().BeFalse();
        timer.State.Should().Be(TimerState.Cancelled);
    }

    #endregion One-shot

    #region Repeating and reset behavior

    /// <summary>
    ///   Verifies that a repeating timer invokes the callback multiple times with spacing
    ///   close to the repeat interval and ends in a repeat-related state.
    /// </summary>
    [Fact]
    public void RegisterTimer_Repeating_CallbackCalledMultipleTimes ()
    {
        IPrimeSystemClock clock = new PrimeSystemClock();
        int count = 0;
        ManualResetEventSlim signal = new(false);
        DateTimeOffset firstCallbackTime = default;
        DateTimeOffset secondCallbackTime = default;

        using IClockIntervalTimer timer = clock.RegisterTimer(ShortDelay, RepeatInterval, () =>
        {
            count++;
            if (count == 1)
                firstCallbackTime = clock.UtcNow;
            else if (count == 2)
            {
                secondCallbackTime = clock.UtcNow;
                signal.Set();
            }
        }, cancellationToken: TestContext.Current.CancellationToken);
        DateTimeOffset start = clock.UtcNow;
        signal.Wait(WaitMargin + ShortDelay + RepeatInterval + WaitMargin, TestContext.Current.CancellationToken).Should().BeTrue();
        count.Should().BeGreaterThan(1);
        (firstCallbackTime - start).Should().BeCloseTo(ShortDelay, TimingTolerance);
        (secondCallbackTime - firstCallbackTime).Should().BeCloseTo(RepeatInterval, TimingTolerance);
        timer.State.Should().BeOneOf(TimerState.RepeatCycle, TimerState.RepeatProcessingCallback);
    }

    /// <summary>
    ///   Verifies that a repeating timer with <see cref="IntervalTimerOptions.ResetIntervalAfterCallback"/>
    ///   invokes callbacks with spacing close to the repeat interval (reset-after-callback semantics).
    /// </summary>
    [Fact]
    public void RegisterTimer_RepeatingResetAfterCallback_CallbackCalledWithCorrectSpacing ()
    {
        IPrimeSystemClock clock = new PrimeSystemClock();
        int count = 0;
        ManualResetEventSlim signal = new(false);
        List<DateTimeOffset> times = new();

        using IClockIntervalTimer timer = clock.RegisterTimer(ShortDelay, RepeatInterval, ctx =>
        {
            times.Add(clock.UtcNow);
            count++;
            if (count >= 2)
                signal.Set();
        }, timerOptions: new IntervalTimerOptions { ResetIntervalAfterCallback = true },
            cancellationToken: TestContext.Current.CancellationToken);
        signal.Wait(WaitMargin + ShortDelay + RepeatInterval + WaitMargin, TestContext.Current.CancellationToken).Should().BeTrue();
        times.Count.Should().BeGreaterThan(1);
        (times[1] - times[0]).Should().BeCloseTo(RepeatInterval, TimingTolerance);
    }

    #endregion Repeating and reset behavior

    #region Change

    /// <summary>
    ///   Verifies that <see cref="IClockIntervalTimer.Change(TimeSpan)"/> on an active one-shot
    ///   timer reschedules the next fire to the new interval.
    /// </summary>
    [Fact]
    public void RegisterTimer_OneShot_ChangeBeforeFire_NextFireAtNewInterval ()
    {
        IPrimeSystemClock clock = new PrimeSystemClock();
        TimeSpan newInterval = TimeSpan.FromMilliseconds(100);
        ManualResetEventSlim signal = new(false);
        DateTimeOffset? firedAt = null;

        using IClockIntervalTimer timer = clock.RegisterTimer(ShortDelay + TimeSpan.FromSeconds(2), () =>
        {
            firedAt = clock.UtcNow;
            signal.Set();
        }, cancellationToken: TestContext.Current.CancellationToken);
        clock.Sleep(ShortDelay);
        timer.Change(newInterval).Should().BeTrue();
        timer.State.Should().Be(TimerState.Active);
        DateTimeOffset afterChange = clock.UtcNow;
        signal.Wait(WaitMargin + newInterval, TestContext.Current.CancellationToken).Should().BeTrue();
        (firedAt!.Value - afterChange).Should().BeCloseTo(newInterval, TimingTolerance);
    }

    /// <summary>
    ///   Verifies that calling <see cref="IClockIntervalTimer.Change(TimeSpan)"/> on a completed
    ///   one-shot timer reschedules and fires the callback again, then completes.
    /// </summary>
    [Fact]
    public void RegisterTimer_OneShot_ChangeAfterFire_ReschedulesAndFiresAgain ()
    {
        IPrimeSystemClock clock = new PrimeSystemClock();
        TimeSpan secondInterval = TimeSpan.FromMilliseconds(90);
        ManualResetEventSlim signal = new(false);
        int count = 0;

        using IClockIntervalTimer timer = clock.RegisterTimer(ShortDelay, () =>
        {
            count++;
            if (count == 1)
                signal.Set();
            else if (count == 2)
                signal.Set();
        }, cancellationToken: TestContext.Current.CancellationToken);
        signal.Wait(WaitMargin, TestContext.Current.CancellationToken).Should().BeTrue();
        clock.Sleep(CallbackSettle);
        timer.State.Should().Be(TimerState.Completed);
        signal.Reset();
        timer.Change(secondInterval).Should().BeTrue();
        timer.State.Should().Be(TimerState.Active);
        DateTimeOffset start = clock.UtcNow;
        bool secondFired = signal.Wait(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
        secondFired.Should().BeTrue("second callback should fire after Change(interval) on completed one-shot");
        count.Should().Be(2);
        clock.Sleep(CallbackSettle);
        timer.State.Should().Be(TimerState.Completed);
    }

    /// <summary>
    ///   Verifies that <see cref="IClockIntervalTimer.Change(TimeSpan, TimeSpan)"/> on a repeating
    ///   timer updates both the next due time and the repeat interval for subsequent callbacks.
    /// </summary>
    [Fact]
    public void RegisterTimer_Repeating_ChangeNextAndRepeatInterval_NextFiresAtNewIntervals ()
    {
        IPrimeSystemClock clock = new PrimeSystemClock();
        TimeSpan newFirst = TimeSpan.FromMilliseconds(70);
        TimeSpan newRepeat = TimeSpan.FromMilliseconds(55);
        ManualResetEventSlim signal = new(false);
        List<DateTimeOffset> times = new();
        int targetCount = 3;

        using IClockIntervalTimer timer = clock.RegisterTimer(ShortDelay + TimeSpan.FromSeconds(1), RepeatInterval, () =>
        {
            times.Add(clock.UtcNow);
            if (times.Count >= targetCount)
                signal.Set();
        }, cancellationToken: TestContext.Current.CancellationToken);
        clock.Sleep(ShortDelay);
        timer.Change(newFirst, newRepeat).Should().BeTrue();
        DateTimeOffset start = clock.UtcNow;
        signal.Wait(WaitMargin + newFirst + newRepeat + newRepeat + WaitMargin, TestContext.Current.CancellationToken).Should().BeTrue();
        times.Count.Should().BeGreaterThan(targetCount - 1);
        (times[0] - start).Should().BeCloseTo(newFirst, TimingTolerance);
        if (times.Count >= 2)
            (times[1] - times[0]).Should().BeCloseTo(newRepeat, TimingTolerance);
    }

    /// <summary>
    ///   Verifies that calling <see cref="IClockIntervalTimer.Change(TimeSpan, TimeSpan)"/> on a
    ///   one-shot timer throws <see cref="InvalidOperationException"/>.
    /// </summary>
    [Fact]
    public void RegisterTimer_OneShot_ChangeToRepeating_ThrowsInvalidOperationException ()
    {
        IPrimeSystemClock clock = new PrimeSystemClock();
        using IClockIntervalTimer timer = clock.RegisterTimer(ShortDelay + TimeSpan.FromSeconds(1), () => { },
            cancellationToken: TestContext.Current.CancellationToken);
        Action act = () => timer.Change(TimeSpan.FromMilliseconds(50), TimeSpan.FromMilliseconds(50));
        act.Should().Throw<InvalidOperationException>();
    }

    #endregion Change

    #region Stop / Start

    /// <summary>
    ///   Verifies that <see cref="IRegisteredTimer.Stop"/> sets state to
    ///   <see cref="TimerState.Disabled"/>, and <see cref="IRegisteredTimer.Start"/> after
    ///   <see cref="IClockIntervalTimer.Change(TimeSpan)"/> reschedules and allows the callback to fire.
    /// </summary>
    [Fact]
    public void RegisterTimer_Stop_StateDisabled_Start_Reschedules ()
    {
        IPrimeSystemClock clock = new PrimeSystemClock();
        ManualResetEventSlim signal = new(false);
        using IClockIntervalTimer timer = clock.RegisterTimer(ShortDelay + TimeSpan.FromSeconds(2), () => signal.Set(),
            cancellationToken: TestContext.Current.CancellationToken);
        clock.Sleep(ShortDelay);
        timer.Stop().Should().BeTrue();
        timer.State.Should().Be(TimerState.Disabled);
        signal.Wait(ShortDelay, TestContext.Current.CancellationToken).Should().BeFalse();
        timer.Change(ShortDelay).Should().BeTrue();
        timer.Start().Should().BeTrue();
        timer.State.Should().Be(TimerState.Active);
        signal.Wait(WaitMargin + ShortDelay, TestContext.Current.CancellationToken).Should().BeTrue();
    }

    #endregion Stop / Start

    #region Async and Unsafe

    /// <summary>
    ///   Verifies that a one-shot async timer invokes the callback, the returned
    ///   <see cref="ValueTask"/> completes, and state becomes <see cref="TimerState.Completed"/>.
    /// </summary>
    [Fact]
    public void RegisterAsyncTimer_OneShot_ValueTaskCompletesAndStateCompleted ()
    {
        IPrimeSystemClock clock = new PrimeSystemClock();
        ManualResetEventSlim signal = new(false);
        DateTimeOffset? firedAt = null;

        using IClockIntervalTimer timer = clock.RegisterAsyncTimer(ShortDelay, ct =>
        {
            firedAt = clock.UtcNow;
            signal.Set();
            return default;
        }, cancellationToken: TestContext.Current.CancellationToken);
        DateTimeOffset start = clock.UtcNow;
        signal.Wait(WaitMargin, TestContext.Current.CancellationToken).Should().BeTrue();
        clock.Sleep(CallbackSettle);
        timer.State.Should().Be(TimerState.Completed);
        (firedAt!.Value - start).Should().BeCloseTo(ShortDelay, TimingTolerance);
    }

    /// <summary>
    ///   Verifies that a one-shot async timer with state passes the state to the callback
    ///   via <see cref="ClockTimerCallbackContext"/> (and receives a cancellation token).
    /// </summary>
    [Fact]
    public void RegisterAsyncTimer_OneShotWithContext_ReceivesStateAndToken ()
    {
        IPrimeSystemClock clock = new PrimeSystemClock();
        object state = new();
        ManualResetEventSlim signal = new(false);
        object? receivedState = null;

        using IClockIntervalTimer timer = clock.RegisterAsyncTimer(ShortDelay, (ctx, ct) =>
        {
            receivedState = ctx.CallbackState;
            signal.Set();
            return default;
        }, state, cancellationToken: TestContext.Current.CancellationToken);
        signal.Wait(WaitMargin, TestContext.Current.CancellationToken).Should().BeTrue();
        receivedState.Should().BeSameAs(state);
    }

    /// <summary>
    ///   Verifies that a repeating async timer with
    ///   <see cref="IntervalTimerOptions.ResetIntervalAfterCallback"/> schedules the next tick
    ///   only after the async callback completes (not from callback start).
    /// </summary>
    [Fact]
    public void RegisterAsyncTimer_RepeatingResetAfterCallback_NextTickAfterAsyncCallbackCompletes ()
    {
        IPrimeSystemClock clock = new PrimeSystemClock();
        TimeSpan asyncWork = TimeSpan.FromMilliseconds(80);
        List<DateTimeOffset> callbackStarts = new();
        ManualResetEventSlim signal = new(false);

        using IClockIntervalTimer timer = clock.RegisterAsyncTimer(
            ShortDelay,
            RepeatInterval,
            async (ct) =>
            {
                callbackStarts.Add(clock.UtcNow);
                await Task.Delay(asyncWork, ct);
                if (callbackStarts.Count >= 2)
                    signal.Set();
            },
            timerOptions: new IntervalTimerOptions { ResetIntervalAfterCallback = true },
            cancellationToken: TestContext.Current.CancellationToken);
        // Time until second callback: ShortDelay + first callback (asyncWork) + RepeatInterval; WaitMargin for timing tolerance.
        signal.Wait(ShortDelay + asyncWork + RepeatInterval + WaitMargin, TestContext.Current.CancellationToken).Should().BeTrue();
        callbackStarts.Count.Should().BeGreaterThan(1);
        TimeSpan betweenFirstAndSecond = callbackStarts[1] - callbackStarts[0];
        betweenFirstAndSecond.Should().BeGreaterThan(RepeatInterval,
            "next tick must be scheduled after async callback completes, so gap includes repeat interval plus async work");
        betweenFirstAndSecond.Should().BeCloseTo(RepeatInterval + asyncWork, TimingTolerance);
    }

    /// <summary>
    ///   Verifies that with Unsafe option the timer callback is invoked (execution context is not
    ///   captured/restored). Full isolation from the calling thread's AsyncLocal may depend on the
    ///   BCL timer and thread pool behavior.
    /// </summary>
    [Fact]
    public void RegisterTimer_UnsafeOption_CallbackInvoked ()
    {
        IPrimeSystemClock clock = new PrimeSystemClock();
        ManualResetEventSlim signal = new(false);

        using IClockIntervalTimer timer = clock.RegisterTimer(ShortDelay, () => signal.Set(),
            timerOptions: new IntervalTimerOptions { CallbackExecutionContext = TimerCallbackExecutionContext.Unsafe },
            cancellationToken: TestContext.Current.CancellationToken);
        bool fired = signal.Wait(WaitMargin + ShortDelay + TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken);
        fired.Should().BeTrue("callback should fire within timeout when using Unsafe option");
        timer.State.Should().Be(TimerState.Completed);
    }

    #endregion Async and Unsafe

    #region Properties

    /// <summary>
    ///   Verifies that the timer returned by RegisterTimer has the expected
    ///   <see cref="IClockIntervalTimer"/> contract properties (Id, IsTimeOfDay, IsRepeating,
    ///   RegisteredTime, ElapsedTime, TimeUntilNextCallback).
    /// </summary>
    [Fact]
    public void RegisterTimer_ReturnsTimerWithCorrectContractProperties ()
    {
        IPrimeSystemClock clock = new PrimeSystemClock();
        ManualResetEventSlim signal = new(false);
        using IClockIntervalTimer timer = clock.RegisterTimer(ShortDelay, () => signal.Set(),
            cancellationToken: TestContext.Current.CancellationToken);
        timer.Id.Should().BeGreaterThan(0);
        timer.IsTimeOfDay.Should().BeFalse();
        timer.IsRepeating.Should().BeFalse();
        timer.IsCancelled.Should().BeFalse();
        timer.RegisteredTime.Should().BeCloseTo(clock.UtcNow, TimeSpan.FromSeconds(5));
        timer.ElapsedTime.Should().Be(-1);
        timer.TimeUntilNextCallback.Should().BeInRange(0, (long)(ShortDelay + WaitMargin).TotalMilliseconds);
        signal.Wait(WaitMargin, TestContext.Current.CancellationToken).Should().BeTrue();
        clock.Sleep(CallbackSettle);
        timer.ElapsedTime.Should().BeGreaterThanOrEqualTo(0);
    }

    /// <summary>
    ///   Verifies that <see cref="IClockIntervalTimer.Change(TimeSpan)"/> returns <c>false</c>
    ///   after the timer registration has been disposed.
    /// </summary>
    [Fact]
    public void RegisterTimer_AfterDispose_ChangeReturnsFalse ()
    {
        IPrimeSystemClock clock = new PrimeSystemClock();
        IClockIntervalTimer timer = clock.RegisterTimer(ShortDelay + TimeSpan.FromSeconds(2), () => { },
            cancellationToken: TestContext.Current.CancellationToken);
        timer.Dispose();
        timer.Change(TimeSpan.FromMilliseconds(50)).Should().BeFalse();
    }

    #endregion Properties
}
