// Copyright (c) Kevin Zehrer
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

#if NET

using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Threading;

using AwesomeAssertions;
using KZDev.PrimeTime.Testing;
using KZDev.PrimeTime.Tests;

using Microsoft.Extensions.Time.Testing;
// ReSharper disable AccessToDisposedClosure

namespace KZDev.SystemClock.PrimeTime.UnitTests;

//################################################################################
/// <summary>
///   Unit tests for <see cref="ClockDayTimeTimerRegistration"/> callback dispatch, sequential
///   overlap handling, lifecycle, and <see cref="IClockDayTimeTimer"/> surface members that are not
///   otherwise exercised through <see cref="PrimeTestClock"/> virtual day-time timers.
/// </summary>
[ExcludeFromCodeCoverage]
public class UsingClockDayTimeTimerRegistration : UnitTestBase
{
    /// <summary>
    ///   Short delay from &quot;now&quot; used to compute a UTC time-of-day that fires soon (milliseconds).
    /// </summary>
    private static readonly TimeSpan ShortDelay = TimeSpan.FromMilliseconds(200);

    /// <summary>
    ///   Extra wait time beyond expected delay to avoid flaky failures.
    /// </summary>
    private static readonly TimeSpan WaitMargin = TimeSpan.FromSeconds(5);

    #region Constructors/Finalizers

    /// <summary>
    ///   Initializes a new instance of the <see cref="UsingClockDayTimeTimerRegistration"/> class.
    /// </summary>
    /// <param name="xUnitTestOutputHelper">The xUnit test output helper.</param>
    public UsingClockDayTimeTimerRegistration (ITestOutputHelper xUnitTestOutputHelper)
        : base(xUnitTestOutputHelper)
    {
    }

    #endregion Constructors/Finalizers

    /// <summary>
    ///   Regression: when a UTC day-time tick runs slightly before today's nominal <see cref="TimeOnly"/>
    ///   instant, the next reschedule must advance to the next UTC calendar day instead of arming a
    ///   short follow-up for the same slot (which caused duplicate callbacks under load).
    /// </summary>
    [Fact]
    public void ClockDayTimeTimerRegistration_UtcEarlyTimerTick_ReschedulesNextDayNotSameSlot ()
    {
        DateTimeOffset startUtc = new(2025, 6, 15, 12, 0, 0, 490, TimeSpan.Zero);
        TimeOnly target = new(12, 0, 0, 640);
        FakeTimeProvider fake = new(startUtc);
        IPrimeClock clock = new PrimeClock(fake);
        using ManualResetEventSlim entered = new(false);
        using IClockDayTimeTimer registration = new ClockDayTimeTimerRegistration(clock, new UtcTimeOfDay(target),
            TimerCallbackKind.SimpleAction, entered.Set, null, null, TestContext.Current.CancellationToken);
        MethodInfo onTimerTick = ClockTimerRegistrationTestReflection.GetDayTimeOnTimerTickMethod(
            typeof(ClockDayTimeTimerRegistration));
        onTimerTick.Invoke(registration, [null]);
        entered.Wait(WaitMargin, TestContext.Current.CancellationToken).Should().BeTrue();
        registration.TimeUntilNextCallback.Should().BeGreaterThan((long)TimeSpan.FromHours(20).TotalMilliseconds);
    }

    /// <summary>
    ///   Verifies that <see cref="TimerCallbackKind.SimpleAction"/> uses the synchronous simple-action
    ///   dispatch path on <see cref="ClockDayTimeTimerRegistration"/>.
    /// </summary>
    /// <remarks>
    ///   A wall-clock <see cref="PrimeClock"/> can deliver the first <see cref="Timer"/> tick slightly
    ///   before the nominal <see cref="TimeOnly"/> instant; the next-delay calculation may then arm a
    ///   short follow-up tick. Cancelling the registration token on the first callback stops further
    ///   scheduling so the assertion stays stable under parallel test load.
    /// </remarks>
    [Fact]
    public void ClockDayTimeTimerRegistration_SimpleActionCallback_InvokesUserAction ()
    {
        using CancellationTokenSource stopAfterFirstInvoke =
            CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        IPrimeClock clock = new PrimeClock();
        TimeOnly target = TimeOnly.FromDateTime((clock.UtcNowDateTimeOffset + ShortDelay).UtcDateTime);
        using ManualResetEventSlim done = new(false);
        int fired = 0;
        using IClockDayTimeTimer registration = new ClockDayTimeTimerRegistration(clock, new UtcTimeOfDay(target),
            TimerCallbackKind.SimpleAction, (Action)(() =>
            {
                stopAfterFirstInvoke.Cancel();
                // ReSharper disable once AccessToModifiedClosure
                Interlocked.Increment(ref fired);
                done.Set();
            }), null, null, stopAfterFirstInvoke.Token);
        done.Wait(WaitMargin, TestContext.Current.CancellationToken).Should().BeTrue();
        Volatile.Read(ref fired).Should().Be(1);
    }

    /// <summary>
    ///   Verifies that <see cref="TimerCallbackKind.ContextActionWithToken"/> passes the registration
    ///   cancellation token into the user callback.
    /// </summary>
    [Fact]
    public void ClockDayTimeTimerRegistration_ContextActionWithTokenCallback_PassesRegistrationToken ()
    {
        IPrimeClock clock = new PrimeClock();
        TimeOnly target = TimeOnly.FromDateTime((clock.UtcNowDateTimeOffset + ShortDelay).UtcDateTime);
        using ManualResetEventSlim done = new(false);
        using CancellationTokenSource cts = new();
        CancellationToken? receivedToken = null;
        using IClockDayTimeTimer registration = new ClockDayTimeTimerRegistration(clock, new UtcTimeOfDay(target),
            TimerCallbackKind.ContextActionWithToken,
            (Action<ClockTimerCallbackContext, CancellationToken>)((_, ct) =>
            {
                receivedToken = ct;
                done.Set();
            }), null, null, cts.Token);
        done.Wait(WaitMargin, TestContext.Current.CancellationToken).Should().BeTrue();
        receivedToken.Should().NotBeNull();
        receivedToken!.Value.Should().Be(cts.Token);
    }

    /// <summary>
    ///   Verifies that with <see cref="ConcurrentTriggerProcessing.RunSequentially"/>, a second timer tick
    ///   while the first callback is still running defers work and <see cref="ClockDayTimeTimerRegistration"/>
    ///   eventually runs the deferred callback (exercising <c>_pendingRunSequential</c>, <c>ProcessCallback</c>,
    ///   and synchronous completion paths).
    /// </summary>
    [Fact]
    public void ClockDayTimeTimerRegistration_RunSequentially_OverlappingTick_DeferredCallbackRuns ()
    {
        IPrimeClock clock = new PrimeClock();
        TimeOnly target = TimeOnly.FromDateTime((clock.UtcNowDateTimeOffset + ShortDelay).UtcDateTime);
        DayTimeTimerOptions options = new()
        {
            ConcurrentTriggerProcessing = ConcurrentTriggerProcessing.RunSequentially
        };
        using ManualResetEventSlim enteredFirstCallback = new(false);
        using ManualResetEventSlim releaseFirstCallback = new(false);
        int invokeCount = 0;
        Action userCallback = () =>
        {
            if (Interlocked.Increment(ref invokeCount) != 1)
            {
                return;
            }

            enteredFirstCallback.Set();
            releaseFirstCallback.Wait(WaitMargin, TestContext.Current.CancellationToken).Should().BeTrue();
        };
        using IClockDayTimeTimer registration = new ClockDayTimeTimerRegistration(clock, new UtcTimeOfDay(target),
            TimerCallbackKind.SimpleAction, userCallback, null, options, TestContext.Current.CancellationToken);
        enteredFirstCallback.Wait(WaitMargin, TestContext.Current.CancellationToken).Should().BeTrue();
        MethodInfo onTimerTick = ClockTimerRegistrationTestReflection.GetDayTimeOnTimerTickMethod(
            typeof(ClockDayTimeTimerRegistration));
        onTimerTick.Invoke(registration, [null]);
        releaseFirstCallback.Set();
        SpinWait.SpinUntil(() => Volatile.Read(ref invokeCount) >= 2, WaitMargin).Should().BeTrue();
    }

    /// <summary>
    ///   Verifies that <see cref="TimerCallbackKind.SimpleAsync"/> with a synchronously completed
    ///   <see cref="ValueTask"/> runs through <c>RunAsync</c> and, when a sequential run is pending,
    ///   invokes the user delegate twice in one loop (the <c>continue</c> path after clearing
    ///   <c>_pendingRunSequential</c>).
    /// </summary>
    [Fact]
    public void ClockDayTimeTimerRegistration_SimpleAsync_SyncCompleteWithPendingSequential_InvokesTwiceInOneRunAsync ()
    {
        IPrimeClock clock = new PrimeClock();
        TimeOnly target = TimeOnly.FromDateTime((clock.UtcNowDateTimeOffset + ShortDelay).UtcDateTime);
        DayTimeTimerOptions options = new()
        {
            ConcurrentTriggerProcessing = ConcurrentTriggerProcessing.RunSequentially
        };
        using ManualResetEventSlim innerStarted = new(false);
        using ManualResetEventSlim allowSecondTick = new(false);
        int callCount = 0;
        Func<CancellationToken, ValueTask> run = _ =>
        {
            int n = Interlocked.Increment(ref callCount);
            if (n != 1)
            {
                return default;
            }

            innerStarted.Set();
            allowSecondTick.Wait(WaitMargin, TestContext.Current.CancellationToken).Should().BeTrue();

            return default;
        };
        using IClockDayTimeTimer registration = new ClockDayTimeTimerRegistration(clock, new UtcTimeOfDay(target),
            TimerCallbackKind.SimpleAsync, run, null, options, TestContext.Current.CancellationToken);
        innerStarted.Wait(WaitMargin, TestContext.Current.CancellationToken).Should().BeTrue();
        MethodInfo onTimerTick = ClockTimerRegistrationTestReflection.GetDayTimeOnTimerTickMethod(
            typeof(ClockDayTimeTimerRegistration));
        onTimerTick.Invoke(registration, [null]);
        allowSecondTick.Set();
        SpinWait.SpinUntil(() => Volatile.Read(ref callCount) >= 2, WaitMargin).Should().BeTrue();
    }

    /// <summary>
    ///   Verifies that an asynchronous context callback with <see cref="ConcurrentTriggerProcessing.RunSequentially"/>,
    ///   when a second tick arrives while the first callback is still incomplete, schedules the deferred
    ///   continuation path (<c>OnAsyncComplete</c> with <c>Task.Run</c> / continuation wiring).
    /// </summary>
    [Fact]
    public void ClockDayTimeTimerRegistration_ContextAsync_OverlappingTickWhileIncomplete_RunsDeferredCallback ()
    {
        IPrimeClock clock = new PrimeClock();
        TimeOnly target = TimeOnly.FromDateTime((clock.UtcNowDateTimeOffset + ShortDelay).UtcDateTime);
        DayTimeTimerOptions options = new()
        {
            ConcurrentTriggerProcessing = ConcurrentTriggerProcessing.RunSequentially
        };
        using ManualResetEventSlim enteredAsyncBody = new(false);
        using ManualResetEventSlim allowSecondTick = new(false);
        int enteredCount = 0;
        Func<ClockTimerCallbackContext, CancellationToken, ValueTask> callback = async (_, _) =>
        {
            if (Interlocked.Increment(ref enteredCount) != 1)
            {
                return;
            }

            enteredAsyncBody.Set();
            allowSecondTick.Wait(WaitMargin, TestContext.Current.CancellationToken).Should().BeTrue();
            await Task.Delay(50, TestContext.Current.CancellationToken).ConfigureAwait(false);
        };
        using IClockDayTimeTimer registration = new ClockDayTimeTimerRegistration(clock, new UtcTimeOfDay(target),
            TimerCallbackKind.ContextAsync, callback, null, options, TestContext.Current.CancellationToken);
        enteredAsyncBody.Wait(WaitMargin, TestContext.Current.CancellationToken).Should().BeTrue();
        MethodInfo onTimerTick = ClockTimerRegistrationTestReflection.GetDayTimeOnTimerTickMethod(
            typeof(ClockDayTimeTimerRegistration));
        onTimerTick.Invoke(registration, [null]);
        allowSecondTick.Set();
        SpinWait.SpinUntil(() => Volatile.Read(ref enteredCount) >= 2, WaitMargin).Should().BeTrue();
    }

    /// <summary>
    ///   Verifies that <see cref="TimerCallbackKind.SimpleAsync"/> uses <c>RunAsync</c> with a synchronously
    ///   completed <see cref="ValueTask"/> (the fast path in <see cref="ClockDayTimeTimerRegistration"/>).
    /// </summary>
    [Fact]
    public void ClockDayTimeTimerRegistration_SimpleAsync_DefaultValueTask_CompletesCallback ()
    {
        IPrimeClock clock = new PrimeClock();
        TimeOnly target = TimeOnly.FromDateTime((clock.UtcNowDateTimeOffset + ShortDelay).UtcDateTime);
        using ManualResetEventSlim done = new(false);
        Func<CancellationToken, ValueTask> run = _ =>
        {
            done.Set();
            return default;
        };
        using IClockDayTimeTimer registration = new ClockDayTimeTimerRegistration(clock, new UtcTimeOfDay(target),
            TimerCallbackKind.SimpleAsync, run, null, null, TestContext.Current.CancellationToken);
        done.Wait(WaitMargin, TestContext.Current.CancellationToken).Should().BeTrue();
    }

    /// <summary>
    ///   Verifies that <see cref="IClockDayTimeTimer.Start"/> after <see cref="IClockTimer.Stop"/> returns
    ///   <c>true</c> and restores an active registration.
    /// </summary>
    [Fact]
    public void RegisterTimeOfDay_Utc_StopThenStart_ReturnsTrueAndRestoresActive ()
    {
        IPrimeClock clock = new PrimeClock();
        TimeOnly farTarget = TimeOnly.FromDateTime((clock.UtcNowDateTimeOffset + TimeSpan.FromHours(6)).UtcDateTime);
        using IClockDayTimeTimer timer = clock.RegisterTimeOfDay(new UtcTimeOfDay(farTarget), () => { },
            TestContext.Current.CancellationToken);
        timer.State.Should().Be(TimerState.Active);
        timer.Stop().Should().BeTrue();
        timer.State.Should().Be(TimerState.Disabled);
        timer.Start().Should().BeTrue();
        timer.State.Should().Be(TimerState.Active);
    }

    /// <summary>
    ///   Verifies that <see cref="IClockDayTimeTimer.Change(LocalTimeOfDay)"/> on a local registration updates the
    ///   schedule so the callback fires near the new local time of day.
    /// </summary>
    [Fact]
    public void RegisterTimeOfDay_Local_ChangeLocalTimeOfDay_ReschedulesAndFiresAtNewTime ()
    {
        IPrimeClock clock = new PrimeClock();
        TimeOnly farTarget = TimeOnly.FromDateTime((clock.LocalNowDateTimeOffset + TimeSpan.FromSeconds(8)).DateTime);
        LocalTimeOfDay initial = new(farTarget);
        using ManualResetEventSlim signal = new(false);
        DateTimeOffset? firedAt = null;
        using IClockDayTimeTimer timer = clock.RegisterTimeOfDay(initial, () =>
        {
            firedAt = clock.LocalNowDateTimeOffset;
            signal.Set();
        }, TestContext.Current.CancellationToken);
        TimeOnly newTarget = TimeOnly.FromDateTime((clock.LocalNowDateTimeOffset + ShortDelay).DateTime);
        timer.Change(new LocalTimeOfDay(newTarget)).Should().BeTrue();
        DateTimeOffset afterChange = clock.LocalNowDateTimeOffset;
        signal.Wait(WaitMargin, TestContext.Current.CancellationToken).Should().BeTrue();
        firedAt.Should().NotBeNull();
        (firedAt!.Value - afterChange).Should().BeCloseTo(ShortDelay, TimeSpan.FromMilliseconds(400));
    }

    /// <summary>
    ///   Verifies that <see cref="IClockDayTimeTimer.Change(UtcTimeOfDay)"/> on a UTC registration updates the
    ///   schedule so the callback fires near the new UTC time of day.
    /// </summary>
    [Fact]
    public void RegisterTimeOfDay_Utc_ChangeUtcTimeOfDay_ReschedulesAndFiresAtNewTime ()
    {
        IPrimeClock clock = new PrimeClock();
        TimeOnly farTarget = TimeOnly.FromDateTime((clock.UtcNowDateTimeOffset + TimeSpan.FromSeconds(8)).UtcDateTime);
        UtcTimeOfDay initial = new(farTarget);
        using ManualResetEventSlim signal = new(false);
        DateTimeOffset? firedAt = null;
        using IClockDayTimeTimer timer = clock.RegisterTimeOfDay(initial, () =>
        {
            firedAt = clock.UtcNowDateTimeOffset;
            signal.Set();
        }, TestContext.Current.CancellationToken);
        TimeOnly newTarget = TimeOnly.FromDateTime((clock.UtcNowDateTimeOffset + ShortDelay).UtcDateTime);
        timer.Change(new UtcTimeOfDay(newTarget)).Should().BeTrue();
        DateTimeOffset afterChange = clock.UtcNowDateTimeOffset;
        signal.Wait(WaitMargin, TestContext.Current.CancellationToken).Should().BeTrue();
        firedAt.Should().NotBeNull();
        (firedAt!.Value - afterChange).Should().BeCloseTo(ShortDelay, TimeSpan.FromMilliseconds(400));
    }

    /// <summary>
    ///   Verifies that <see cref="IClockDayTimeTimer.ElapsedTime"/> reports <c>-1</c> before any callback and
    ///   a non-negative value after a callback has completed.
    /// </summary>
    [Fact]
    public void RegisterTimeOfDay_Utc_ElapsedTime_BeforeFireIsNegativeOne_AfterFireIsNonNegative ()
    {
        IPrimeClock clock = new PrimeClock();
        TimeOnly farTarget = TimeOnly.FromDateTime((clock.UtcNowDateTimeOffset + TimeSpan.FromSeconds(6)).UtcDateTime);
        using ManualResetEventSlim signal = new(false);
        using IClockDayTimeTimer timer = clock.RegisterTimeOfDay(new UtcTimeOfDay(farTarget), signal.Set,
            TestContext.Current.CancellationToken);
        timer.ElapsedTime.Should().Be(-1);
        TimeOnly nearTarget = TimeOnly.FromDateTime((clock.UtcNowDateTimeOffset + ShortDelay).UtcDateTime);
        timer.Change(new UtcTimeOfDay(nearTarget)).Should().BeTrue();
        signal.Wait(WaitMargin, TestContext.Current.CancellationToken).Should().BeTrue();
        Thread.Sleep(30);
        timer.ElapsedTime.Should().BeGreaterThanOrEqualTo(0);
    }
}
//################################################################################

#endif
