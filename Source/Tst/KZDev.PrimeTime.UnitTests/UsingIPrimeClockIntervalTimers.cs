// Copyright (c) Kevin Zehrer
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

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
        timer.State.Should().BeOneOf(TimerState.RepeatCycle, TimerState.ProcessingCallback);
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies that a repeating timer with default <see cref="IntervalTimerOptions.ResetIntervalBeforeCallback"/>
    ///   (<c>false</c>) invokes callbacks with spacing close to the repeat interval (countdown starts after each callback completes).
    /// </summary>
    [Fact]
    public void RegisterTimer_Repeating_CountdownAfterCallback_CallbackCalledWithCorrectSpacing ()
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
        }, TestContext.Current.CancellationToken);
        signal.Wait((WaitMargin + ShortDelay + RepeatInterval + WaitMargin).ToTimeSpan(),
            TestContext.Current.CancellationToken).Should().BeTrue();
        times.Count.Should().BeGreaterThan(1);
        (times[1] - times[0]).Should().BeGreaterThanOrEqualTo(RepeatInterval.Minus(TimingTolerance));
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies that when <see cref="IntervalTimerOptions.ResetIntervalBeforeCallback"/> is <c>true</c>,
    ///   the next repeat interval is armed before the current callback completes, allowing overlapping
    ///   callbacks and reporting <see cref="TimerState.RepeatProcessingCallback"/> during execution.
    /// </summary>
    [Fact]
    public void RegisterTimer_Repeating_WithResetIntervalBeforeCallbackTrue_AllowsOverlapAndReportsRepeatProcessingCallback ()
    {
        IPrimeClock clock = new PrimeClock();
        IntervalTimerOptions timerOptions = new() { ResetIntervalBeforeCallback = true };
        using ManualResetEventSlim firstCallbackStarted = new(false);
        using ManualResetEventSlim overlapObserved = new(false);
        using ManualResetEventSlim releaseCallbacks = new(false);
        using ManualResetEventSlim callbacksCompleted = new(false);
        int callbacksStarted = 0;
        int callbacksCompletedCount = 0;
        int callbacksRunning = 0;
        TimerState[] observedStates = new TimerState[2];

        IClockIntervalTimer? timer = null;
        timer = clock.RegisterTimer(ShortDelay,
            RepeatInterval,
            _ =>
            {
                int callbackIndex = Interlocked.Increment(ref callbacksStarted) - 1;
                int runningCount = Interlocked.Increment(ref callbacksRunning);
                if (callbackIndex < observedStates.Length)
                    observedStates[callbackIndex] = timer!.State;
                if (callbackIndex == 0)
                    firstCallbackStarted.Set();
                if (runningCount > 1)
                    overlapObserved.Set();

                try
                {
                    releaseCallbacks.Wait((WaitMargin + RepeatInterval + WaitMargin).ToTimeSpan(),
                        TestContext.Current.CancellationToken).Should().BeTrue();
                }
                finally
                {
                    Interlocked.Decrement(ref callbacksRunning);
                    if (Interlocked.Increment(ref callbacksCompletedCount) >= 2)
                        callbacksCompleted.Set();
                }
            },
            TestContext.Current.CancellationToken,
            timerOptions: timerOptions);
        using (timer)
        {
            firstCallbackStarted.Wait((ShortDelay + WaitMargin).ToTimeSpan(), TestContext.Current.CancellationToken).Should().BeTrue();
            overlapObserved.Wait((RepeatInterval + WaitMargin).ToTimeSpan(), TestContext.Current.CancellationToken).Should().BeTrue();
            timer.State.Should().Be(TimerState.RepeatProcessingCallback);
            observedStates[0].Should().Be(TimerState.RepeatProcessingCallback);
            observedStates[1].Should().Be(TimerState.RepeatProcessingCallback);
            timer.CallbacksProcessing.Should().BeTrue();

            releaseCallbacks.Set();
            callbacksCompleted.Wait((WaitMargin + RepeatInterval + WaitMargin).ToTimeSpan(),
                TestContext.Current.CancellationToken).Should().BeTrue();
        }
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies that when overlapping callbacks are enabled by
    ///   <see cref="IntervalTimerOptions.ResetIntervalBeforeCallback"/> being <c>true</c>, completing one
    ///   callback while another is still running transitions the registration to
    ///   <see cref="TimerState.ProcessingCallback"/> until the final overlapping callback completes.
    /// </summary>
    [Fact]
    public void RegisterTimer_Repeating_WithResetIntervalBeforeCallbackTrue_WhenOneOverlapCompletes_StateBecomesProcessingCallback ()
    {
        IPrimeClock clock = new PrimeClock();
        IntervalTimerOptions timerOptions = new() { ResetIntervalBeforeCallback = true };
        using ManualResetEventSlim firstCallbackStarted = new(false);
        using ManualResetEventSlim secondCallbackStarted = new(false);
        using ManualResetEventSlim firstCallbackMayExit = new(false);
        using ManualResetEventSlim secondCallbackMayExit = new(false);
        using ManualResetEventSlim firstCallbackCompleted = new(false);
        int callbacksStarted = 0;

        IClockIntervalTimer? timer = null;
        timer = clock.RegisterTimer(ShortDelay,
            RepeatInterval,
            _ =>
            {
                int callbackIndex = Interlocked.Increment(ref callbacksStarted);
                if (callbackIndex == 1)
                {
                    firstCallbackStarted.Set();
                    firstCallbackMayExit.Wait((WaitMargin + RepeatInterval + WaitMargin).ToTimeSpan(),
                        TestContext.Current.CancellationToken).Should().BeTrue();
                    firstCallbackCompleted.Set();
                    return;
                }

                if (callbackIndex == 2)
                {
                    secondCallbackStarted.Set();
                    secondCallbackMayExit.Wait((WaitMargin + RepeatInterval + WaitMargin).ToTimeSpan(),
                        TestContext.Current.CancellationToken).Should().BeTrue();
                }
            },
            TestContext.Current.CancellationToken,
            timerOptions: timerOptions);
        using (timer)
        {
            firstCallbackStarted.Wait((ShortDelay + WaitMargin).ToTimeSpan(), TestContext.Current.CancellationToken).Should().BeTrue();
            secondCallbackStarted.Wait((RepeatInterval + WaitMargin).ToTimeSpan(), TestContext.Current.CancellationToken).Should().BeTrue();

            firstCallbackMayExit.Set();

            firstCallbackCompleted.Wait((WaitMargin + RepeatInterval).ToTimeSpan(), TestContext.Current.CancellationToken).Should().BeTrue();
            SpinWait.SpinUntil(() => timer.State == TimerState.ProcessingCallback,
                WaitMargin.ToTimeSpan()).Should().BeTrue();
            timer.CallbacksProcessing.Should().BeTrue();

            secondCallbackMayExit.Set();
            SpinWait.SpinUntil(() => timer.State == TimerState.RepeatCycle,
                WaitMargin.ToTimeSpan()).Should().BeTrue();
        }
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
    ///   Verifies that <see cref="ClockIntervalTimerRegistration"/> supports
    ///   <see cref="TimerCallbackKind.SimpleAction"/> and completes after invoking the callback.
    /// </summary>
    [Fact]
    public void ClockIntervalTimerRegistration_WithSimpleActionCallback_InvokesCallbackAndCompletes ()
    {
        IPrimeClock clock = new PrimeClock();
        using ManualResetEventSlim callbackInvoked = new(false);
        using IClockIntervalTimer timer = new ClockIntervalTimerRegistration(clock,
            ShortDelay.ToTimeSpan(),
            Timeout.InfiniteTimeSpan,
            TimerCallbackKind.SimpleAction,
            () => callbackInvoked.Set(),
            null,
            null,
            TestContext.Current.CancellationToken);

        callbackInvoked.Wait((ShortDelay + WaitMargin).ToTimeSpan(), TestContext.Current.CancellationToken).Should().BeTrue();
        clock.Sleep(CallbackSettle);
        timer.State.Should().Be(TimerState.Completed);
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies that <see cref="ClockIntervalTimerRegistration"/> supports
    ///   <see cref="TimerCallbackKind.ContextActionWithToken"/> and passes both callback
    ///   state and registration cancellation token to the callback.
    /// </summary>
    [Fact]
    public void ClockIntervalTimerRegistration_WithContextActionWithTokenCallback_PassesStateAndCancellationToken ()
    {
        IPrimeClock clock = new PrimeClock();
        object state = new();
        using CancellationTokenSource cancellationTokenSource = new();
        using ManualResetEventSlim callbackInvoked = new(false);
        object? receivedState = null;
        CancellationToken? receivedToken = null;

        using IClockIntervalTimer timer = new ClockIntervalTimerRegistration(clock,
            ShortDelay.ToTimeSpan(),
            Timeout.InfiniteTimeSpan,
            TimerCallbackKind.ContextActionWithToken,
            (Action<ClockTimerCallbackContext, CancellationToken>)((ClockTimerCallbackContext callbackContext, CancellationToken cancellationToken) =>
            {
                receivedState = callbackContext.CallbackState;
                receivedToken = cancellationToken;
                callbackInvoked.Set();
            }),
            state,
            null,
            cancellationTokenSource.Token);

        callbackInvoked.Wait((ShortDelay + WaitMargin).ToTimeSpan(), TestContext.Current.CancellationToken).Should().BeTrue();
        receivedState.Should().BeSameAs(state);
        receivedToken.Should().NotBeNull();
        receivedToken!.Value.Should().Be(cancellationTokenSource.Token);
        clock.Sleep(CallbackSettle);
        timer.State.Should().Be(TimerState.Completed);
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
    ///   Verifies that <see cref="ClockIntervalTimerRegistration"/> supports
    ///   <see cref="TimerCallbackKind.SimpleAsync"/> and completes after the async callback
    ///   returns a completed <see cref="ValueTask"/>.
    /// </summary>
    [Fact]
    public void ClockIntervalTimerRegistration_WithSimpleAsyncCallback_InvokesCallbackAndCompletes ()
    {
        IPrimeClock clock = new PrimeClock();
        using ManualResetEventSlim callbackInvoked = new(false);

        using IClockIntervalTimer timer = new ClockIntervalTimerRegistration(clock,
            ShortDelay.ToTimeSpan(),
            Timeout.InfiniteTimeSpan,
            TimerCallbackKind.SimpleAsync,
            (Func<CancellationToken, ValueTask>)(_ =>
            {
                callbackInvoked.Set();
                return default;
            }),
            null,
            null,
            TestContext.Current.CancellationToken);

        callbackInvoked.Wait((ShortDelay + WaitMargin).ToTimeSpan(), TestContext.Current.CancellationToken).Should().BeTrue();
        clock.Sleep(CallbackSettle);
        timer.State.Should().Be(TimerState.Completed);
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies that a repeating async timer with default
    ///   <see cref="IntervalTimerOptions.ResetIntervalBeforeCallback"/> (<c>false</c>) schedules the next tick
    ///   only after the async callback completes (not from callback start).
    /// </summary>
    [Fact]
    public void RegisterAsyncTimer_Repeating_CountdownAfterCallback_NextTickAfterAsyncCallbackCompletes ()
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
            TestContext.Current.CancellationToken);
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
    ///   Verifies that callbacks from two independent timers can run concurrently on real time.
    /// </summary>
    [Fact]
    public void RegisterTimer_TwoIndependentTimers_CanRunCallbacksConcurrently ()
    {
        IPrimeClock clock = new PrimeClock();
        using ManualResetEventSlim firstEntered = new(false);
        using ManualResetEventSlim secondEntered = new(false);
        using ManualResetEventSlim overlapObserved = new(false);
        using SemaphoreSlim allowExit = new(0, 2);
        int inFlight = 0;
        int maxInFlight = 0;

        ValueTask Callback (ManualResetEventSlim enteredSignal, CancellationToken ct)
        {
            enteredSignal.Set();
            int current = Interlocked.Increment(ref inFlight);
            int recordedMaxInFlight = Volatile.Read(ref maxInFlight);
            while (current > recordedMaxInFlight)
            {
                int observedMaxInFlight = Interlocked.CompareExchange(ref maxInFlight, current, recordedMaxInFlight);
                if (observedMaxInFlight == recordedMaxInFlight)
                {
                    break;
                }

                recordedMaxInFlight = observedMaxInFlight;
            }

            if (current > 1)
            {
                overlapObserved.Set();
            }

            return RunAsync();
            async ValueTask RunAsync ()
            {
                try
                {
                    await allowExit.WaitAsync(GetAsyncTimerCallbackHoldTimeoutMilliseconds(Duration.FromSeconds(2).ToTimeSpan()), ct);
                }
                finally
                {
                    Interlocked.Decrement(ref inFlight);
                }
            }
        }

        using IClockIntervalTimer timer1 = clock.RegisterAsyncTimer(Duration.FromMilliseconds(30),
            ct => Callback(firstEntered, ct),
            TestContext.Current.CancellationToken);
        using IClockIntervalTimer timer2 = clock.RegisterAsyncTimer(Duration.FromMilliseconds(30),
            ct => Callback(secondEntered, ct),
            TestContext.Current.CancellationToken);

        TimeSpan waitTimeout = (WaitMargin + Duration.FromMilliseconds(200)).ToTimeSpan();
        firstEntered.Wait(waitTimeout, TestContext.Current.CancellationToken)
            .Should().BeTrue();
        secondEntered.Wait(waitTimeout, TestContext.Current.CancellationToken)
            .Should().BeTrue();
        overlapObserved.Wait(Duration.FromSeconds(1).ToTimeSpan(), TestContext.Current.CancellationToken).Should().BeTrue();
        maxInFlight.Should().BeGreaterThan(1);

        allowExit.Release(2);
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies that disposing a timer while an async callback is active does not throw and transitions the timer
    ///   out of active processing state.
    /// </summary>
    [Fact]
    public void RegisterAsyncTimer_CallbackRunning_WhenDisposed_StopsActiveProcessing ()
    {
        IPrimeClock clock = new PrimeClock();
        using CancellationTokenSource cts = new();
        using ManualResetEventSlim callbackStarted = new(false);
        using SemaphoreSlim allowExit = new(0, 1);
        IClockIntervalTimer timer = clock.RegisterAsyncTimer(Duration.FromMilliseconds(30),
            async ct =>
            {
                callbackStarted.Set();
                await allowExit.WaitAsync(GetAsyncTimerCallbackHoldTimeoutMilliseconds(WaitMargin.ToTimeSpan()), ct);
            },
            cts.Token);
        using (timer)
        {
            callbackStarted.Wait((WaitMargin + ShortDelay).ToTimeSpan(), TestContext.Current.CancellationToken).Should().BeTrue();

            Action act = () => timer.Dispose();
            act.Should().NotThrow();
            SpinWait.SpinUntil(() => !timer.CallbacksProcessing, WaitMargin.ToTimeSpan()).Should().BeTrue();
            timer.State.Should().Be(TimerState.Disposed);
            allowExit.Release();
        }
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
    ///   Verifies that registering with <see cref="Duration.MaxValue"/> can overflow scheduling math and throws
    ///   <see cref="OverflowException"/>.
    /// </summary>
    [Fact]
    public void RegisterTimer_OneShotDurationMaxValue_ThrowsOverflowException ()
    {
        IPrimeClock clock = new PrimeClock();
        Action act = () => clock.RegisterTimer(Duration.MaxValue, () => { }, TestContext.Current.CancellationToken);
        act.Should().Throw<OverflowException>();
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies that <see cref="IClockIntervalTimer.Change(Duration)"/> throws
    ///   <see cref="ObjectDisposedException"/> after the timer registration has been disposed.
    /// </summary>
    [Fact]
    public void RegisterTimer_AfterDispose_Change_ThrowsObjectDisposedException ()
    {
        IPrimeClock clock = new PrimeClock();
        IClockIntervalTimer timer = clock.RegisterTimer(ShortDelay + Duration.FromSeconds(2),
            () => { },
            cancellationToken: TestContext.Current.CancellationToken);
        timer.Dispose();
        Action act = () => timer.Change(Duration.FromMilliseconds(50));
        act.Should().Throw<ObjectDisposedException>();
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies that <see cref="IRegisteredTimer.Start"/> throws
    ///   <see cref="ObjectDisposedException"/> after the timer registration has been disposed.
    /// </summary>
    [Fact]
    public void RegisterTimer_AfterDispose_Start_ThrowsObjectDisposedException ()
    {
        IPrimeClock clock = new PrimeClock();
        IClockIntervalTimer timer = clock.RegisterTimer(ShortDelay + Duration.FromSeconds(2),
            () => { },
            cancellationToken: TestContext.Current.CancellationToken);
        timer.Dispose();
        Action act = () => timer.Start();
        act.Should().Throw<ObjectDisposedException>();
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies that setting <see cref="IRegisteredTimer.Enabled"/> throws
    ///   <see cref="ObjectDisposedException"/> after the timer registration has been disposed.
    /// </summary>
    [Fact]
    public void RegisterTimer_AfterDispose_SetEnabled_ThrowsObjectDisposedException ()
    {
        IPrimeClock clock = new PrimeClock();
        IClockIntervalTimer timer = clock.RegisterTimer(ShortDelay + Duration.FromSeconds(2),
            () => { },
            cancellationToken: TestContext.Current.CancellationToken);
        timer.Dispose();
        Action act = () => timer.Enabled = true;
        act.Should().Throw<ObjectDisposedException>();
    }
    //----------------------------------------------------------------------------

    #region Regression (repeat spacing after first virtual tick)

    /// <summary>
    ///   Regression (virtual clock): after the first interval callback, <see cref="IClockIntervalTimer.TimeUntilNextCallback"/>
    ///   must reflect the configured repeat interval so repeat spacing cannot silently regress.
    /// </summary>
    [Fact]
    public void RegisterTimer_Repeating_AfterFirstVirtualTick_TimeUntilNextReflectsRepeat ()
    {
        IPrimeTestClock clock = new PrimeTestClock(Instant.FromUtc(2025, 6, 15, 10, 0, 0));
        TimeSpan repeat = TimeSpan.FromHours(2);
        using IClockIntervalTimer timer = clock.RegisterTimer(TimeSpan.FromHours(24), repeat, () => { },
            CancellationToken.None);
        clock.Advance(Duration.FromHours(24));
        long msUntilNext = timer.TimeUntilNextCallback;
        // Align with the SystemClock repeat-spacing regression (±5 minutes around the configured repeat):
        // the virtual path arms the next due instant as clock-now + repeat, so the value should stay
        // near the full repeat interval rather than tolerating an arbitrary ±1 hour window.
        long minExpected = (long)TimeSpan.FromHours(2).Subtract(TimeSpan.FromMinutes(5)).TotalMilliseconds;
        long maxExpected = (long)TimeSpan.FromHours(2).Add(TimeSpan.FromMinutes(5)).TotalMilliseconds;
        msUntilNext.Should().BeInRange(minExpected, maxExpected);
    }

    #endregion Regression (repeat spacing after first virtual tick)

    #endregion Properties
}
//################################################################################

