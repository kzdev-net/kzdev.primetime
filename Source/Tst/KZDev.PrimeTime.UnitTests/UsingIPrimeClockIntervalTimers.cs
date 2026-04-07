// Copyright (c) Kevin Zehrer
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
//
// Unit tests for IPrimeClock interval timers. Uses real time; use PrimeTestClock for fully
// deterministic interval timer tests.

using System.Diagnostics.CodeAnalysis;

using AwesomeAssertions;
using KZDev.PrimeTime.Tests;
using NodaTime;

namespace KZDev.PrimeTime.UnitTests;

//################################################################################
/// <summary>
///   Unit tests for <see cref="IPrimeClock"/> RegisterTimer and RegisterAsyncTimer
///   (interval timers) and <see cref="IClockIntervalTimer"/> (one-shot, repeating,
///   Change(Duration), Change(Duration, Duration), Unsafe).
/// </summary>
[ExcludeFromCodeCoverage]
public class UsingIPrimeClockIntervalTimers : UnitTestBase
{
    //----------------------------------------------------------------------------
    /// <summary>
    ///   Short initial delay used by many tests.
    /// </summary>
    private static readonly Duration ShortDelay = Duration.FromMilliseconds(80);

    /// <summary>
    ///   Repeat interval for repeating-timer tests.
    /// </summary>
    private static readonly Duration RepeatInterval = Duration.FromMilliseconds(60);

    /// <summary>
    ///   Extra wait time beyond expected delay to avoid flaky failures.
    /// </summary>
    private static readonly Duration WaitMargin = Duration.FromMilliseconds(400);

    /// <summary>
    ///   Allowed tolerance when asserting callback timing (milliseconds).
    /// </summary>
    private static readonly Duration TimingTolerance = Duration.FromMilliseconds(150);

    /// <summary>
    ///   Brief wait after callback to let state settle before assertions.
    /// </summary>
    private static readonly Duration CallbackSettle = Duration.FromMilliseconds(50);
    //----------------------------------------------------------------------------

    #region Constructors/Finalizers

    //----------------------------------------------------------------------------
    /// <summary>
    ///   Initializes a new instance of the <see cref="UsingIPrimeClockIntervalTimers"/> class.
    /// </summary>
    /// <param name="xUnitTestOutputHelper">
    ///   The xUnit test output helper for diagnostic output.
    /// </param>
    public UsingIPrimeClockIntervalTimers (ITestOutputHelper xUnitTestOutputHelper)
        : base(xUnitTestOutputHelper)
    {
    }
    //----------------------------------------------------------------------------

    #endregion Constructors/Finalizers

    //----------------------------------------------------------------------------
    /// <summary>
    ///   Asserts that <see cref="IClockIntervalTimer.Change(Duration, Duration)"/> on a one-shot registration
    ///   with a positive repeat interval throws <see cref="InvalidOperationException"/>.
    /// </summary>
    /// <param name="registration">The timer registration to exercise.</param>
    private static void AssertChangeToRepeatingThrows (IClockIntervalTimer registration)
    {
        Action act = () => registration.Change(Duration.FromMilliseconds(50), Duration.FromMilliseconds(50));
        act.Should().Throw<InvalidOperationException>();
    }
    //----------------------------------------------------------------------------

    #region One-shot

    /// <summary>
    ///   Verifies that a one-shot timer invokes the callback once and reaches
    ///   <see cref="TimerState.Completed"/> with timing close to the initial delay.
    /// </summary>
    [Fact]
    public void RegisterTimer_OneShot_CallbackCalledOnceAndStateCompleted ()
    {
        IPrimeClock clock = new PrimeClock();
        ManualResetEventSlim signal = new(false);
        Instant? firedAt = null;

        using IClockIntervalTimer timer = clock.RegisterTimer(ShortDelay, () =>
        {
            firedAt = clock.NowInstant;
            signal.Set();
        }, cancellationToken: TestContext.Current.CancellationToken);
        Instant start = clock.NowInstant;
        signal.Wait(WaitMargin.ToTimeSpan(), TestContext.Current.CancellationToken).Should().BeTrue();
        clock.Sleep(CallbackSettle);
        timer.State.Should().Be(TimerState.Completed);
        firedAt.Should().NotBeNull();
        Duration elapsed = firedAt!.Value - start;
        elapsed.Should().BeGreaterThanOrEqualTo(ShortDelay.Minus(TimingTolerance));
        elapsed.Should().BeLessThanOrEqualTo(ShortDelay.Plus(TimingTolerance));
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies that a one-shot timer with state passes the state and registration
    ///   (<see cref="IClockIntervalTimer"/>) to the callback via
    ///   <see cref="ClockTimerCallbackContext"/>.
    /// </summary>
    [Fact]
    public void RegisterTimer_OneShotWithContext_CallbackReceivesStateAndRegistration ()
    {
        IPrimeClock clock = new PrimeClock();
        object state = new();
        ManualResetEventSlim signal = new(false);
        object? receivedState = null;
        IClockIntervalTimer? receivedReg = null;

        using IClockIntervalTimer timer = clock.RegisterTimer(ShortDelay, callbackContext =>
        {
            receivedState = callbackContext.CallbackState;
            receivedReg = (IClockIntervalTimer)callbackContext.Registration;
            signal.Set();
        }, TestContext.Current.CancellationToken, state);
        signal.Wait(WaitMargin.ToTimeSpan(), TestContext.Current.CancellationToken).Should().BeTrue();
        receivedState.Should().BeSameAs(state);
        receivedReg.Should().BeSameAs(timer);
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies the <see cref="PrimeClockNodaTimerExtensions.RegisterTimer(IPrimeClock, Duration, Action{ClockTimerCallbackContext, CancellationToken}, CancellationToken, object?, bool, IntervalTimerOptions?)"/>
    ///   overload passes the registration <see cref="CancellationToken"/> through to the callback.
    /// </summary>
    [Fact]
    public void RegisterTimer_OneShot_ExtensionWithContextAndToken_CallbackReceivesRegistrationCancellationToken ()
    {
        IPrimeClock clock = new PrimeClock();
        ManualResetEventSlim signal = new(false);
        using CancellationTokenSource cts = new();
        CancellationToken? receivedToken = null;
        using IClockIntervalTimer timer = clock.RegisterTimer(ShortDelay,
            (ClockTimerCallbackContext _, CancellationToken ct) =>
            {
                receivedToken = ct;
                signal.Set();
            },
            cts.Token);
        signal.Wait(WaitMargin.ToTimeSpan(), TestContext.Current.CancellationToken).Should().BeTrue();
        receivedToken.Should().NotBeNull();
        receivedToken!.Value.Should().Be(cts.Token);
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies that when a one-shot timer is registered with an already cancelled
    ///   <see cref="CancellationToken"/>, the timer never fires and state is
    ///   <see cref="TimerState.Cancelled"/>.
    /// </summary>
    [Fact]
    public void RegisterTimer_OneShotWithCancelledToken_StateCancelled ()
    {
        IPrimeClock clock = new PrimeClock();
        ManualResetEventSlim signal = new(false);
        using CancellationTokenSource cts = new();
        cts.Cancel();

        using IClockIntervalTimer timer = clock.RegisterTimer(ShortDelay, () => signal.Set(),
            cts.Token, timerOptions: null);
        signal.Wait((ShortDelay + CallbackSettle).ToTimeSpan(), TestContext.Current.CancellationToken).Should().BeFalse();
        timer.State.Should().Be(TimerState.Cancelled);
        timer.IsCancelled.Should().BeTrue();
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies that cancelling a one-shot timer before it fires prevents the callback
    ///   from running and sets state to <see cref="TimerState.Cancelled"/>.
    /// </summary>
    [Fact]
    public void RegisterTimer_OneShot_CancelBeforeFire_StateCancelled ()
    {
        IPrimeClock clock = new PrimeClock();
        ManualResetEventSlim signal = new(false);
        using IClockIntervalTimer timer = clock.RegisterTimer(ShortDelay + Duration.FromMilliseconds(200),
            () => signal.Set(),
            cancellationToken: TestContext.Current.CancellationToken);
        timer.Cancel();
        signal.Wait((ShortDelay + WaitMargin).ToTimeSpan(), TestContext.Current.CancellationToken).Should().BeFalse();
        timer.State.Should().Be(TimerState.Cancelled);
    }
    //----------------------------------------------------------------------------

    #endregion One-shot

    #region Repeating and reset behavior

    /// <summary>
    ///   Verifies that a repeating timer invokes the callback multiple times with spacing
    ///   close to the repeat interval and ends in a repeat-related state.
    /// </summary>
    [Fact]
    public void RegisterTimer_Repeating_CallbackCalledMultipleTimes ()
    {
        IPrimeClock clock = new PrimeClock();
        int count = 0;
        ManualResetEventSlim signal = new(false);
        Instant firstCallbackTime = default;
        Instant secondCallbackTime = default;

        using IClockIntervalTimer timer = clock.RegisterTimer(ShortDelay, RepeatInterval, () =>
        {
            count++;
            switch (count)
            {
                case 1:
                    firstCallbackTime = clock.NowInstant;
                    break;

                case 2:
                    secondCallbackTime = clock.NowInstant;
                    signal.Set();
                    break;
            }
        }, cancellationToken: TestContext.Current.CancellationToken);
        Instant start = clock.NowInstant;
        signal.Wait((WaitMargin + ShortDelay + RepeatInterval + WaitMargin).ToTimeSpan(),
            TestContext.Current.CancellationToken).Should().BeTrue();
        count.Should().BeGreaterThan(1);
        (firstCallbackTime - start).Should().BeGreaterThanOrEqualTo(ShortDelay.Minus(TimingTolerance));
        (secondCallbackTime - firstCallbackTime).Should().BeGreaterThanOrEqualTo(RepeatInterval.Minus(TimingTolerance));
        timer.State.Should().BeOneOf(TimerState.RepeatCycle, TimerState.RepeatProcessingCallback);
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies that a repeating timer with <see cref="IntervalTimerOptions.ResetIntervalAfterCallback"/>
    ///   invokes callbacks with spacing close to the repeat interval (reset-after-callback semantics).
    /// </summary>
    [Fact]
    public void RegisterTimer_RepeatingResetAfterCallback_CallbackCalledWithCorrectSpacing ()
    {
        IPrimeClock clock = new PrimeClock();
        int count = 0;
        ManualResetEventSlim signal = new(false);
        List<Instant> times = [];

        using IClockIntervalTimer timer = clock.RegisterTimer(ShortDelay, RepeatInterval, callbackContext =>
        {
            times.Add(clock.NowInstant);
            count++;
            if (count >= 2)
                signal.Set();
        }, TestContext.Current.CancellationToken,
            timerOptions: new IntervalTimerOptions { ResetIntervalAfterCallback = true });
        signal.Wait((WaitMargin + ShortDelay + RepeatInterval + WaitMargin).ToTimeSpan(),
            TestContext.Current.CancellationToken).Should().BeTrue();
        times.Count.Should().BeGreaterThan(1);
        (times[1] - times[0]).Should().BeGreaterThanOrEqualTo(RepeatInterval.Minus(TimingTolerance));
    }
    //----------------------------------------------------------------------------

    #endregion Repeating and reset behavior

    #region Change

    /// <summary>
    ///   Verifies that <see cref="IClockIntervalTimer.Change(Duration)"/> on an active one-shot
    ///   timer reschedules the next fire to the new interval.
    /// </summary>
    [Fact]
    public void RegisterTimer_OneShot_ChangeBeforeFire_NextFireAtNewInterval ()
    {
        IPrimeClock clock = new PrimeClock();
        Duration newInterval = Duration.FromMilliseconds(100);
        ManualResetEventSlim signal = new(false);
        Instant? firedAt = null;

        using IClockIntervalTimer timer = clock.RegisterTimer(ShortDelay + Duration.FromSeconds(2),
            () =>
            {
                firedAt = clock.NowInstant;
                signal.Set();
            },
            cancellationToken: TestContext.Current.CancellationToken);
        clock.Sleep(ShortDelay);
        timer.Change(newInterval).Should().BeTrue();
        timer.State.Should().Be(TimerState.Active);
        Instant afterChange = clock.NowInstant;
        signal.Wait((WaitMargin + newInterval).ToTimeSpan(), TestContext.Current.CancellationToken).Should().BeTrue();
        (firedAt!.Value - afterChange).Should().BeGreaterThanOrEqualTo(newInterval.Minus(TimingTolerance));
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies that calling <see cref="IClockIntervalTimer.Change(Duration)"/> on a completed
    ///   one-shot timer reschedules and fires the callback again, then completes.
    /// </summary>
    [Fact]
    public void RegisterTimer_OneShot_ChangeAfterFire_ReschedulesAndFiresAgain ()
    {
        IPrimeClock clock = new PrimeClock();
        Duration secondInterval = Duration.FromMilliseconds(90);
        ManualResetEventSlim signal = new(false);
        int count = 0;

        using IClockIntervalTimer timer = clock.RegisterTimer(ShortDelay, () =>
        {
            count++;
            if (count == 1 || count == 2)
                signal.Set();
        }, cancellationToken: TestContext.Current.CancellationToken);
        signal.Wait(WaitMargin.ToTimeSpan(), TestContext.Current.CancellationToken).Should().BeTrue();
        clock.Sleep(CallbackSettle);
        timer.State.Should().Be(TimerState.Completed);
        signal.Reset();
        timer.Change(secondInterval).Should().BeTrue();
        timer.State.Should().Be(TimerState.Active);
        Instant start = clock.NowInstant;
        bool secondFired = signal.Wait(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
        secondFired.Should().BeTrue("second callback should fire after Change(interval) on completed one-shot");
        count.Should().Be(2);
        clock.Sleep(CallbackSettle);
        timer.State.Should().Be(TimerState.Completed);
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies that <see cref="IClockIntervalTimer.Change(Duration, Duration)"/> on a repeating
    ///   timer updates both the next due time and the repeat interval for subsequent callbacks.
    /// </summary>
    [Fact]
    public void RegisterTimer_Repeating_ChangeNextAndRepeatInterval_NextFiresAtNewIntervals ()
    {
        IPrimeClock clock = new PrimeClock();
        Duration newFirst = Duration.FromMilliseconds(70);
        Duration newRepeat = Duration.FromMilliseconds(55);
        ManualResetEventSlim signal = new(false);
        List<Instant> times = [];
        int targetCount = 3;

        using IClockIntervalTimer timer = clock.RegisterTimer(ShortDelay + Duration.FromSeconds(1),
            RepeatInterval,
            () =>
            {
                times.Add(clock.NowInstant);
                if (times.Count >= targetCount)
                    signal.Set();
            },
            cancellationToken: TestContext.Current.CancellationToken);
        clock.Sleep(ShortDelay);
        timer.Change(newFirst, newRepeat).Should().BeTrue();
        Instant start = clock.NowInstant;
        signal.Wait((WaitMargin + newFirst + newRepeat + newRepeat + WaitMargin).ToTimeSpan(),
            TestContext.Current.CancellationToken).Should().BeTrue();
        times.Count.Should().BeGreaterThan(targetCount - 1);
        (times[0] - start).Should().BeGreaterThanOrEqualTo(newFirst.Minus(TimingTolerance));
        if (times.Count >= 2)
            (times[1] - times[0]).Should().BeGreaterThanOrEqualTo(newRepeat.Minus(TimingTolerance));
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies that calling <see cref="IClockIntervalTimer.Change(Duration, Duration)"/> on a
    ///   one-shot timer with a positive repeat interval throws <see cref="InvalidOperationException"/>.
    /// </summary>
    [Fact]
    public void RegisterTimer_OneShot_ChangeToRepeating_ThrowsInvalidOperationException ()
    {
        IPrimeClock clock = new PrimeClock();
        using IClockIntervalTimer timer = clock.RegisterTimer(ShortDelay + Duration.FromSeconds(1),
            () => { },
            cancellationToken: TestContext.Current.CancellationToken);
        AssertChangeToRepeatingThrows(timer);
    }
    //----------------------------------------------------------------------------

    #endregion Change

    #region Stop / Start

    /// <summary>
    ///   Verifies that <see cref="IRegisteredTimer.Stop"/> sets state to
    ///   <see cref="TimerState.Disabled"/>, and <see cref="IRegisteredTimer.Start"/> after
    ///   <see cref="IClockIntervalTimer.Change(Duration)"/> reschedules and allows the callback to fire.
    /// </summary>
    [Fact]
    public void RegisterTimer_Stop_StateDisabled_Start_Reschedules ()
    {
        IPrimeClock clock = new PrimeClock();
        ManualResetEventSlim signal = new(false);
        using IClockIntervalTimer timer = clock.RegisterTimer(ShortDelay + Duration.FromSeconds(2),
            () => signal.Set(),
            cancellationToken: TestContext.Current.CancellationToken);
        clock.Sleep(ShortDelay);
        timer.Stop().Should().BeTrue();
        timer.State.Should().Be(TimerState.Disabled);
        timer.IsActive.Should().BeTrue("disabled timers are still active until cancelled, disposed, or completed");
        timer.Enabled.Should().BeFalse();
        signal.Wait(ShortDelay.ToTimeSpan(), TestContext.Current.CancellationToken).Should().BeFalse();
        timer.Change(ShortDelay).Should().BeTrue();
        timer.Start().Should().BeTrue();
        timer.State.Should().Be(TimerState.Active);
        signal.Wait((WaitMargin + ShortDelay).ToTimeSpan(), TestContext.Current.CancellationToken).Should().BeTrue();
    }
    //----------------------------------------------------------------------------

    #endregion Stop / Start

    #region Async and Unsafe

    /// <summary>
    ///   Verifies that a one-shot async timer invokes the callback, the returned
    ///   <see cref="ValueTask"/> completes, and state becomes <see cref="TimerState.Completed"/>.
    /// </summary>
    [Fact]
    public void RegisterAsyncTimer_OneShot_ValueTaskCompletesAndStateCompleted ()
    {
        IPrimeClock clock = new PrimeClock();
        ManualResetEventSlim signal = new(false);
        Instant? firedAt = null;

        using IClockIntervalTimer timer = clock.RegisterAsyncTimer(ShortDelay, ct =>
        {
            firedAt = clock.NowInstant;
            signal.Set();
            return default;
        }, cancellationToken: TestContext.Current.CancellationToken);
        Instant start = clock.NowInstant;
        signal.Wait(WaitMargin.ToTimeSpan(), TestContext.Current.CancellationToken).Should().BeTrue();
        clock.Sleep(CallbackSettle);
        timer.State.Should().Be(TimerState.Completed);
        (firedAt!.Value - start).Should().BeGreaterThanOrEqualTo(ShortDelay.Minus(TimingTolerance));
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies that a one-shot async timer with state passes the state to the callback
    ///   via <see cref="ClockTimerCallbackContext"/> (and receives a cancellation token).
    /// </summary>
    [Fact]
    public void RegisterAsyncTimer_OneShotWithContext_ReceivesStateAndToken ()
    {
        IPrimeClock clock = new PrimeClock();
        object state = new();
        ManualResetEventSlim signal = new(false);
        object? receivedState = null;

        using IClockIntervalTimer timer = clock.RegisterAsyncTimer(ShortDelay, (callbackContext, ct) =>
        {
            receivedState = callbackContext.CallbackState;
            signal.Set();
            return default;
        }, TestContext.Current.CancellationToken, state);
        signal.Wait(WaitMargin.ToTimeSpan(), TestContext.Current.CancellationToken).Should().BeTrue();
        receivedState.Should().BeSameAs(state);
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies that a repeating async timer with
    ///   <see cref="IntervalTimerOptions.ResetIntervalAfterCallback"/> schedules the next tick
    ///   only after the async callback completes (not from callback start).
    /// </summary>
    [Fact]
    public void RegisterAsyncTimer_RepeatingResetAfterCallback_NextTickAfterAsyncCallbackCompletes ()
    {
        IPrimeClock clock = new PrimeClock();
        Duration asyncWork = Duration.FromMilliseconds(80);
        List<Instant> callbackStarts = [];
        ManualResetEventSlim signal = new(false);

        using IClockIntervalTimer timer = clock.RegisterAsyncTimer(ShortDelay,
            RepeatInterval,
            async (ct) =>
            {
                callbackStarts.Add(clock.NowInstant);
                await Task.Delay(asyncWork.ToTimeSpan(), ct);
                if (callbackStarts.Count >= 2)
                    signal.Set();
            },
            TestContext.Current.CancellationToken,
            timerOptions: new IntervalTimerOptions { ResetIntervalAfterCallback = true });
        signal.Wait((ShortDelay + asyncWork + RepeatInterval + WaitMargin).ToTimeSpan(),
            TestContext.Current.CancellationToken).Should().BeTrue();
        callbackStarts.Count.Should().BeGreaterThan(1);
        Duration betweenFirstAndSecond = callbackStarts[1] - callbackStarts[0];
        betweenFirstAndSecond.Should().BeGreaterThan(RepeatInterval,
            "next tick must be scheduled after async callback completes, so gap includes repeat interval plus async work");
        betweenFirstAndSecond.Should().BeLessThanOrEqualTo(RepeatInterval.Plus(asyncWork).Plus(TimingTolerance));
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies that <see cref="IClockTimer.CallbacksProcessing"/> returns <c>false</c> once the
    ///   registration is cancelled, even if an async callback is still in flight.
    /// </summary>
    [Fact]
    public void RegisterAsyncTimer_CallbackRunning_WhenCancelled_CallbacksProcessingReturnsFalse ()
    {
        IPrimeClock clock = new PrimeClock();
        using CancellationTokenSource cts = new();
        using ManualResetEventSlim callbackStarted = new(false);
        using SemaphoreSlim allowCallbackToExit = new(0, 1);
        using IClockIntervalTimer timer = clock.RegisterAsyncTimer(ShortDelay,
            async ct =>
            {
                callbackStarted.Set();
                await allowCallbackToExit.WaitAsync(GetAsyncTimerCallbackHoldTimeoutMilliseconds(WaitMargin.ToTimeSpan()), ct);
            },
            cts.Token);

        callbackStarted.Wait((WaitMargin + ShortDelay).ToTimeSpan(), TestContext.Current.CancellationToken).Should().BeTrue();
        cts.Cancel();
        SpinWait.SpinUntil(() => timer.IsCancelled, WaitMargin.ToTimeSpan()).Should().BeTrue();
        timer.CallbacksProcessing.Should().BeFalse();
        timer.State.Should().Be(TimerState.Cancelled);
        allowCallbackToExit.Release();
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies that with Unsafe option the timer callback is invoked (execution context is not
    ///   captured/restored).
    /// </summary>
    [Fact]
    public void RegisterTimer_UnsafeOption_CallbackInvoked ()
    {
        IPrimeClock clock = new PrimeClock();
        ManualResetEventSlim signal = new(false);

        using IClockIntervalTimer timer = clock.RegisterTimer(ShortDelay, () => signal.Set(),
            TestContext.Current.CancellationToken,
            timerOptions: new IntervalTimerOptions { CallbackExecutionContext = TimerCallbackExecutionContext.Unsafe });
        bool fired = signal.Wait((WaitMargin + ShortDelay + Duration.FromSeconds(1)).ToTimeSpan(),
            TestContext.Current.CancellationToken);
        fired.Should().BeTrue("callback should fire within timeout when using Unsafe option");
        clock.Sleep(CallbackSettle);
        timer.State.Should().Be(TimerState.Completed);
    }
    //----------------------------------------------------------------------------

    #endregion Async and Unsafe

    #region Properties

    /// <summary>
    ///   Verifies that the timer returned by RegisterTimer has the expected
    ///   <see cref="IClockIntervalTimer"/> contract properties (ID, IsTimeOfDay, IsRepeating,
    ///   RegisteredInstant, ElapsedTime, TimeUntilNextCallback).
    /// </summary>
    [Fact]
    public void RegisterTimer_ReturnsTimerWithCorrectContractProperties ()
    {
        IPrimeClock clock = new PrimeClock();
        ManualResetEventSlim signal = new(false);
        using IClockIntervalTimer timer = clock.RegisterTimer(ShortDelay, () => signal.Set(),
            cancellationToken: TestContext.Current.CancellationToken);
        timer.Id.Should().BeGreaterThan(0);
        timer.IsTimeOfDay.Should().BeFalse();
        timer.IsRepeating.Should().BeFalse();
        timer.IsCancelled.Should().BeFalse();
        (clock.NowInstant - timer.RegisteredInstant).Should().BeLessThan(Duration.FromSeconds(5));
        timer.ElapsedTime.Should().Be(-1);
        timer.TimeUntilNextCallback.Should().BeInRange(0L, (long)(ShortDelay + WaitMargin).TotalMilliseconds);
        signal.Wait(WaitMargin.ToTimeSpan(), TestContext.Current.CancellationToken).Should().BeTrue();
        clock.Sleep(CallbackSettle);
        timer.ElapsedTime.Should().BeGreaterThanOrEqualTo(0);
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies that <see cref="IClockIntervalTimer.Change(Duration)"/> returns <c>false</c>
    ///   after the timer registration has been disposed.
    /// </summary>
    [Fact]
    public void RegisterTimer_AfterDispose_ChangeReturnsFalse ()
    {
        IPrimeClock clock = new PrimeClock();
        IClockIntervalTimer timer = clock.RegisterTimer(ShortDelay + Duration.FromSeconds(2),
            () => { },
            cancellationToken: TestContext.Current.CancellationToken);
        timer.Dispose();
        timer.Change(Duration.FromMilliseconds(50)).Should().BeFalse();
    }
    //----------------------------------------------------------------------------

    #endregion Properties
}
//################################################################################

