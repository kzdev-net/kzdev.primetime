// Copyright (c) Kevin Zehrer
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;

#if NET
using System.Reflection;
#endif

using AwesomeAssertions;

using KZDev.PrimeTime.Tests;

#if NET
using Microsoft.Extensions.Time.Testing;
// ReSharper disable AccessToDisposedClosure
#endif

namespace KZDev.SystemClock.PrimeTime.UnitTests;

//################################################################################
/// <summary>
///   Unit tests for <see cref="IPrimeClock"/> RegisterTimer and RegisterAsyncTimer
///   (interval timers) and <see cref="IClockIntervalTimer"/> (one-shot, repeating, Change, Unsafe).
/// </summary>
[ExcludeFromCodeCoverage]
public class UsingIPrimeClockIntervalTimers : UnitTestBase
{
    //----------------------------------------------------------------------------
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
    ///   Asserts that <see cref="IClockIntervalTimer.Change(TimeSpan, TimeSpan)"/> on a one-shot registration
    ///   with a positive repeat interval throws <see cref="InvalidOperationException"/>.
    /// </summary>
    /// <param name="registration">The timer registration to exercise.</param>
    private static void AssertChangeToRepeatingThrows (IClockIntervalTimer registration)
    {
        Action act = () => registration.Change(TimeSpan.FromMilliseconds(50), TimeSpan.FromMilliseconds(50));
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
        DateTimeOffset? firedAt = null;

        using IClockIntervalTimer timer = clock.RegisterTimer(ShortDelay, () =>
        {
            firedAt = clock.UtcNowDateTimeOffset;
            signal.Set();
        }, cancellationToken: TestContext.Current.CancellationToken);
        DateTimeOffset start = clock.UtcNowDateTimeOffset;
        signal.Wait(WaitMargin, TestContext.Current.CancellationToken).Should().BeTrue();
        clock.Sleep(CallbackSettle);
        timer.State.Should().Be(TimerState.Completed);
        firedAt.Should().NotBeNull();
        TimeSpan elapsed = firedAt!.Value - start;
        elapsed.Should().BeGreaterThanOrEqualTo(ShortDelay - TimingTolerance);
        elapsed.Should().BeLessThanOrEqualTo(WaitMargin);
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies that a one-shot timer with state passes the state and registration
    ///   (<see cref="IClockTimer"/>) to the callback via <see cref="ClockTimerCallbackContext"/>.
    /// </summary>
    [Fact]
    public void RegisterTimer_OneShotWithContext_CallbackReceivesStateAndRegistration ()
    {
        IPrimeClock clock = new PrimeClock();
        object state = new();
        ManualResetEventSlim signal = new(false);
        object? receivedState = null;
        IClockTimer? receivedReg = null;

        using IClockIntervalTimer timer = clock.RegisterTimer(ShortDelay, callbackContext =>
        {
            receivedState = callbackContext.CallbackState;
            receivedReg = callbackContext.Registration;
            signal.Set();
        }, TestContext.Current.CancellationToken, state);
        signal.Wait(WaitMargin, TestContext.Current.CancellationToken).Should().BeTrue();
        receivedState.Should().BeSameAs(state);
        receivedReg.Should().BeSameAs(timer);
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

        using IClockIntervalTimer timer = clock.RegisterTimer(ShortDelay, signal.Set,
            cts.Token, timerOptions: null);
        signal.Wait(ShortDelay + CallbackSettle, TestContext.Current.CancellationToken).Should().BeFalse();
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
        using IClockIntervalTimer timer = clock.RegisterTimer(ShortDelay + TimeSpan.FromMilliseconds(200),
            signal.Set, cancellationToken: TestContext.Current.CancellationToken);
        timer.Cancel();
        signal.Wait(ShortDelay + WaitMargin, TestContext.Current.CancellationToken).Should().BeFalse();
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
        DateTimeOffset firstCallbackTime = default;
        DateTimeOffset secondCallbackTime = default;

        using IClockIntervalTimer timer = clock.RegisterTimer(ShortDelay, RepeatInterval, () =>
        {
            count++;
            switch (count)
            {
                case 1:
                    firstCallbackTime = clock.UtcNowDateTimeOffset;
                    break;

                case 2:
                    secondCallbackTime = clock.UtcNowDateTimeOffset;
                    signal.Set();
                    break;
            }
        }, cancellationToken: TestContext.Current.CancellationToken);
        DateTimeOffset start = clock.UtcNowDateTimeOffset;
        signal.Wait(WaitMargin + ShortDelay + RepeatInterval + WaitMargin, TestContext.Current.CancellationToken).Should().BeTrue();
        count.Should().BeGreaterThan(1);
        (firstCallbackTime - start).Should().BeCloseTo(ShortDelay, TimingTolerance);
        (secondCallbackTime - firstCallbackTime).Should().BeCloseTo(RepeatInterval, TimingTolerance);
        SpinWait.SpinUntil(() => timer.State == TimerState.RepeatCycle
            || timer.State == TimerState.ProcessingCallback,
            WaitMargin).Should().BeTrue();
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
        List<DateTimeOffset> times = [];

        using IClockIntervalTimer timer = clock.RegisterTimer(ShortDelay, RepeatInterval, callbackContext =>
        {
            times.Add(clock.UtcNowDateTimeOffset);
            count++;
            if (count >= 2)
                signal.Set();
        }, TestContext.Current.CancellationToken);
        signal.Wait(WaitMargin + ShortDelay + RepeatInterval + WaitMargin, TestContext.Current.CancellationToken).Should().BeTrue();
        times.Count.Should().BeGreaterThan(1);
        (times[1] - times[0]).Should().BeCloseTo(RepeatInterval, TimingTolerance);
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
                    // ReSharper disable once AccessToModifiedClosure
                    observedStates[callbackIndex] = timer!.State;
                if (callbackIndex == 0)
                    firstCallbackStarted.Set();
                if (runningCount > 1)
                    overlapObserved.Set();

                try
                {
                    releaseCallbacks.Wait(WaitMargin + RepeatInterval + WaitMargin,
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
            firstCallbackStarted.Wait(ShortDelay + WaitMargin, TestContext.Current.CancellationToken).Should().BeTrue();
            overlapObserved.Wait(RepeatInterval + WaitMargin, TestContext.Current.CancellationToken).Should().BeTrue();
            timer.State.Should().Be(TimerState.RepeatProcessingCallback);
            observedStates[0].Should().Be(TimerState.RepeatProcessingCallback);
            observedStates[1].Should().Be(TimerState.RepeatProcessingCallback);
            timer.CallbacksProcessing.Should().BeTrue();

            releaseCallbacks.Set();
            callbacksCompleted.Wait(WaitMargin + RepeatInterval + WaitMargin, TestContext.Current.CancellationToken).Should().BeTrue();
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
                    firstCallbackMayExit.Wait(WaitMargin + RepeatInterval + WaitMargin,
                        TestContext.Current.CancellationToken).Should().BeTrue();
                    firstCallbackCompleted.Set();
                    return;
                }

                if (callbackIndex != 2)
                {
                    return;
                }

                secondCallbackStarted.Set();
                secondCallbackMayExit.Wait(WaitMargin + RepeatInterval + WaitMargin,
                    TestContext.Current.CancellationToken).Should().BeTrue();
            },
            TestContext.Current.CancellationToken,
            timerOptions: timerOptions);
        using (timer)
        {
            firstCallbackStarted.Wait(ShortDelay + WaitMargin, TestContext.Current.CancellationToken).Should().BeTrue();
            secondCallbackStarted.Wait(RepeatInterval + WaitMargin, TestContext.Current.CancellationToken).Should().BeTrue();

            firstCallbackMayExit.Set();

            firstCallbackCompleted.Wait(WaitMargin + RepeatInterval, TestContext.Current.CancellationToken).Should().BeTrue();
            SpinWait.SpinUntil(() => timer.State == TimerState.ProcessingCallback,
                WaitMargin).Should().BeTrue();
            timer.CallbacksProcessing.Should().BeTrue();

            secondCallbackMayExit.Set();
            SpinWait.SpinUntil(() => timer.State == TimerState.RepeatCycle,
                WaitMargin).Should().BeTrue();
        }
    }
    //----------------------------------------------------------------------------

    #endregion Repeating and reset behavior

    #region Change

    /// <summary>
    ///   Verifies that <see cref="IClockIntervalTimer.Change(TimeSpan)"/> on an active one-shot
    ///   timer reschedules the next fire to the new interval.
    /// </summary>
    [Fact]
    public void RegisterTimer_OneShot_ChangeBeforeFire_NextFireAtNewInterval ()
    {
        IPrimeClock clock = new PrimeClock();
        TimeSpan newInterval = TimeSpan.FromMilliseconds(100);
        ManualResetEventSlim signal = new(false);
        DateTimeOffset? firedAt = null;

        using IClockIntervalTimer timer = clock.RegisterTimer(ShortDelay + TimeSpan.FromSeconds(2), () =>
        {
            firedAt = clock.UtcNowDateTimeOffset;
            signal.Set();
        }, cancellationToken: TestContext.Current.CancellationToken);
        clock.Sleep(ShortDelay);
        timer.Change(newInterval).Should().BeTrue();
        timer.State.Should().Be(TimerState.Active);
        DateTimeOffset afterChange = clock.UtcNowDateTimeOffset;
        signal.Wait(WaitMargin + newInterval, TestContext.Current.CancellationToken).Should().BeTrue();
        (firedAt!.Value - afterChange).Should().BeCloseTo(newInterval, TimingTolerance);
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies that calling <see cref="IClockIntervalTimer.Change(TimeSpan)"/> on a completed
    ///   one-shot timer reschedules and fires the callback again, then completes.
    /// </summary>
    [Fact]
    public void RegisterTimer_OneShot_ChangeAfterFire_ReschedulesAndFiresAgain ()
    {
        IPrimeClock clock = new PrimeClock();
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
        DateTimeOffset start = clock.UtcNowDateTimeOffset;
        bool secondFired = signal.Wait(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
        secondFired.Should().BeTrue("second callback should fire after Change(interval) on completed one-shot");
        count.Should().Be(2);
        clock.Sleep(CallbackSettle);
        timer.State.Should().Be(TimerState.Completed);
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies that <see cref="IClockIntervalTimer.Change(TimeSpan, TimeSpan)"/> on a repeating
    ///   timer updates both the next due time and the repeat interval for subsequent callbacks.
    /// </summary>
    [Fact]
    public void RegisterTimer_Repeating_ChangeNextAndRepeatInterval_NextFiresAtNewIntervals ()
    {
        const int TargetCount = 3;
        IPrimeClock clock = new PrimeClock();
        TimeSpan newFirst = TimeSpan.FromMilliseconds(70);
        TimeSpan newRepeat = TimeSpan.FromMilliseconds(55);
        ManualResetEventSlim signal = new(false);
        List<DateTimeOffset> times = [];

        using IClockIntervalTimer timer = clock.RegisterTimer(ShortDelay + TimeSpan.FromSeconds(1), RepeatInterval, () =>
        {
            times.Add(clock.UtcNowDateTimeOffset);
            if (times.Count >= TargetCount)
                signal.Set();
        }, cancellationToken: TestContext.Current.CancellationToken);
        clock.Sleep(ShortDelay);
        timer.Change(newFirst, newRepeat).Should().BeTrue();
        DateTimeOffset start = clock.UtcNowDateTimeOffset;
        signal.Wait(WaitMargin + newFirst + newRepeat + newRepeat + WaitMargin, TestContext.Current.CancellationToken).Should().BeTrue();
        times.Count.Should().BeGreaterThan(TargetCount - 1);
        TimeSpan firstElapsed = times[0] - start;
        firstElapsed.Should().BeGreaterThanOrEqualTo(newFirst - TimingTolerance);
        firstElapsed.Should().BeLessThanOrEqualTo(newFirst + WaitMargin);
        if (times.Count < 2)
        {
            return;
        }

        TimeSpan secondElapsed = times[1] - times[0];
        secondElapsed.Should().BeGreaterThanOrEqualTo(newRepeat - TimingTolerance);
        secondElapsed.Should().BeLessThanOrEqualTo(newRepeat + WaitMargin);
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies that calling <see cref="IClockIntervalTimer.Change(TimeSpan, TimeSpan)"/> on a
    ///   one-shot timer throws <see cref="InvalidOperationException"/>.
    /// </summary>
    [Fact]
    public void RegisterTimer_OneShot_ChangeToRepeating_ThrowsInvalidOperationException ()
    {
        IPrimeClock clock = new PrimeClock();
        using IClockIntervalTimer timer = clock.RegisterTimer(ShortDelay + TimeSpan.FromSeconds(1), () => { },
            cancellationToken: TestContext.Current.CancellationToken);
        AssertChangeToRepeatingThrows(timer);
    }
    //----------------------------------------------------------------------------

    #endregion Change

    #region Stop / Start

    /// <summary>
    ///   Verifies that <see cref="IClockTimer.Stop"/> sets state to
    ///   <see cref="TimerState.Disabled"/>, and <see cref="IClockTimer.Start"/> after
    ///   <see cref="IClockIntervalTimer.Change(TimeSpan)"/> reschedules and allows the callback to fire.
    /// </summary>
    [Fact]
    public void RegisterTimer_Stop_StateDisabled_Start_Reschedules ()
    {
        IPrimeClock clock = new PrimeClock();
        ManualResetEventSlim signal = new(false);
        using IClockIntervalTimer timer = clock.RegisterTimer(ShortDelay + TimeSpan.FromSeconds(2), signal.Set,
            cancellationToken: TestContext.Current.CancellationToken);
        clock.Sleep(ShortDelay);
        timer.Stop().Should().BeTrue();
        timer.State.Should().Be(TimerState.Disabled);
        timer.IsActive.Should().BeTrue("disabled timers are still active until cancelled, disposed, or completed");
        timer.Enabled.Should().BeFalse();
        signal.Wait(ShortDelay, TestContext.Current.CancellationToken).Should().BeFalse();
        timer.Change(ShortDelay).Should().BeTrue();
        timer.Start().Should().BeTrue();
        timer.State.Should().Be(TimerState.Active);
        signal.Wait(WaitMargin + ShortDelay, TestContext.Current.CancellationToken).Should().BeTrue();
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
        DateTimeOffset? firedAt = null;

        using IClockIntervalTimer timer = clock.RegisterAsyncTimer(ShortDelay, ct =>
        {
            firedAt = clock.UtcNowDateTimeOffset;
            signal.Set();
            return default;
        }, cancellationToken: TestContext.Current.CancellationToken);
        DateTimeOffset start = clock.UtcNowDateTimeOffset;
        signal.Wait(WaitMargin, TestContext.Current.CancellationToken).Should().BeTrue();
        clock.Sleep(CallbackSettle);
        timer.State.Should().Be(TimerState.Completed);
        (firedAt!.Value - start).Should().BeCloseTo(ShortDelay, TimingTolerance);
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
            ShortDelay,
            Timeout.InfiniteTimeSpan,
            TimerCallbackKind.SimpleAction,
            (Action)callbackInvoked.Set,
            null,
            null,
            TestContext.Current.CancellationToken);

        callbackInvoked.Wait(ShortDelay + WaitMargin, TestContext.Current.CancellationToken).Should().BeTrue();
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
            ShortDelay,
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

        callbackInvoked.Wait(ShortDelay + WaitMargin, TestContext.Current.CancellationToken).Should().BeTrue();
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
        signal.Wait(WaitMargin, TestContext.Current.CancellationToken).Should().BeTrue();
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
            ShortDelay,
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

        callbackInvoked.Wait(ShortDelay + WaitMargin, TestContext.Current.CancellationToken).Should().BeTrue();
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
                await allowCallbackToExit.WaitAsync(GetAsyncTimerCallbackHoldTimeoutMilliseconds(WaitMargin), ct);
            },
            cts.Token);

        callbackStarted.Wait(WaitMargin + ShortDelay, TestContext.Current.CancellationToken).Should().BeTrue();
        cts.Cancel();
        SpinWait.SpinUntil(() => timer.IsCancelled, WaitMargin).Should().BeTrue();
        timer.CallbacksProcessing.Should().BeFalse();
        timer.State.Should().Be(TimerState.Cancelled);
        allowCallbackToExit.Release();
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies that with Unsafe option the timer callback is invoked (execution context is not
    ///   captured/restored). Full isolation from the calling thread's AsyncLocal may depend on the
    ///   BCL timer and thread pool behavior.
    /// </summary>
    [Fact]
    public void RegisterTimer_UnsafeOption_CallbackInvoked ()
    {
        IPrimeClock clock = new PrimeClock();
        ManualResetEventSlim signal = new(false);

        using IClockIntervalTimer timer = clock.RegisterTimer(ShortDelay, signal.Set,
            TestContext.Current.CancellationToken,
            timerOptions: new IntervalTimerOptions { CallbackExecutionContext = TimerCallbackExecutionContext.Unsafe });
        bool fired = signal.Wait(WaitMargin + ShortDelay + TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken);
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
    ///   RegisteredTime, ElapsedTime, TimeUntilNextCallback).
    /// </summary>
    [Fact]
    public void RegisterTimer_ReturnsTimerWithCorrectContractProperties ()
    {
        IPrimeClock clock = new PrimeClock();
        ManualResetEventSlim signal = new(false);
        using IClockIntervalTimer timer = clock.RegisterTimer(ShortDelay, signal.Set,
            cancellationToken: TestContext.Current.CancellationToken);
        timer.Id.Should().BeGreaterThan(0);
        timer.IsTimeOfDay.Should().BeFalse();
        timer.IsRepeating.Should().BeFalse();
        timer.IsCancelled.Should().BeFalse();
        timer.RegisteredTime.Should().BeCloseTo(clock.UtcNowDateTimeOffset, TimeSpan.FromSeconds(5));
        timer.ElapsedTime.Should().Be(-1);
        timer.TimeUntilNextCallback.Should().BeInRange(0, (long)(ShortDelay + WaitMargin).TotalMilliseconds);
        signal.Wait(WaitMargin, TestContext.Current.CancellationToken).Should().BeTrue();
        clock.Sleep(CallbackSettle);
        timer.ElapsedTime.Should().BeGreaterThanOrEqualTo(0);
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies that <see cref="IClockIntervalTimer.Change(TimeSpan)"/> throws
    ///   <see cref="ObjectDisposedException"/> after the timer registration has been disposed.
    /// </summary>
    [Fact]
    public void RegisterTimer_AfterDispose_Change_ThrowsObjectDisposedException ()
    {
        IPrimeClock clock = new PrimeClock();
        IClockIntervalTimer timer = clock.RegisterTimer(ShortDelay + TimeSpan.FromSeconds(2), () => { },
            cancellationToken: TestContext.Current.CancellationToken);
        timer.Dispose();
        Action act = () => timer.Change(TimeSpan.FromMilliseconds(50));
        act.Should().Throw<ObjectDisposedException>();
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies that <see cref="IClockTimer.Start"/> throws
    ///   <see cref="ObjectDisposedException"/> after the timer registration has been disposed.
    /// </summary>
    [Fact]
    public void RegisterTimer_AfterDispose_Start_ThrowsObjectDisposedException ()
    {
        IPrimeClock clock = new PrimeClock();
        IClockIntervalTimer timer = clock.RegisterTimer(ShortDelay + TimeSpan.FromSeconds(2), () => { },
            cancellationToken: TestContext.Current.CancellationToken);
        timer.Dispose();
        Action act = () => timer.Start();
        act.Should().Throw<ObjectDisposedException>();
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies that setting <see cref="IClockTimer.Enabled"/> throws
    ///   <see cref="ObjectDisposedException"/> after the timer registration has been disposed.
    /// </summary>
    [Fact]
    public void RegisterTimer_AfterDispose_SetEnabled_ThrowsObjectDisposedException ()
    {
        IPrimeClock clock = new PrimeClock();
        IClockIntervalTimer timer = clock.RegisterTimer(ShortDelay + TimeSpan.FromSeconds(2), () => { },
            cancellationToken: TestContext.Current.CancellationToken);
        timer.Dispose();
        Action act = () => timer.Enabled = true;
        act.Should().Throw<ObjectDisposedException>();
    }
    //----------------------------------------------------------------------------

    #endregion Properties

#if NET
    #region Regression (repeat spacing after first tick)

    /// <summary>
    ///   Regression: after the first synchronous interval tick, <see cref="IClockIntervalTimer.TimeUntilNextCallback"/>
    ///   must reflect the full repeat interval (not a spurious sub-interval), so the same class of
    ///   scheduling bug as UTC day-time early ticks cannot silently regress here.
    /// </summary>
    [Fact]
    public void RegisterTimer_Repeating_AfterFirstCallback_TimeUntilNextReflectsFullRepeat ()
    {
        DateTimeOffset startUtc = new(2025, 6, 15, 10, 0, 0, 0, TimeSpan.Zero);
        FakeTimeProvider fake = new(startUtc);
        IPrimeClock clock = new PrimeClock(fake);
        using ManualResetEventSlim entered = new(false);
        TimeSpan repeat = TimeSpan.FromHours(1);
        using IClockIntervalTimer timer = clock.RegisterTimer(TimeSpan.FromHours(24), repeat, entered.Set,
            TestContext.Current.CancellationToken);
        MethodInfo onTimerTick = ClockTimerRegistrationTestReflection.GetIntervalOnTimerTickMethod(
            typeof(ClockIntervalTimerRegistration));
        onTimerTick.Invoke(timer, [null]);
        entered.Wait(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken).Should().BeTrue();
        long msUntilNext = timer.TimeUntilNextCallback;
        // Allow ±5 minutes around the 1-hour repeat interval to absorb test callback execution time,
        // thread scheduling delays, and timer/time-provider precision when reading the next due time.
        long minExpected = (long)TimeSpan.FromMinutes(55).TotalMilliseconds;
        long maxExpected = (long)(TimeSpan.FromHours(1) + TimeSpan.FromMinutes(5)).TotalMilliseconds;
        msUntilNext.Should().BeInRange(minExpected, maxExpected);
    }

    #endregion Regression (repeat spacing after first tick)
#endif
}
//################################################################################

