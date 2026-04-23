// Copyright (c) Kevin Zehrer
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using System.Reflection;

using AwesomeAssertions;

using KZDev.PrimeTime.Tests;

using NodaTime;
using NodaTime.Testing;

namespace KZDev.PrimeTime.UnitTests;

/// <summary>
///   Unit tests for Noda <see cref="ClockDayTimeTimerRegistration"/> behavior exposed through
///   <see cref="IClockDayTimeTimer"/> (elapsed / time-until-next metrics, dynamic
///   <see cref="IClockDayTimeTimer.Change(LocalTime)"/>, <see cref="ConcurrentTriggerProcessing"/> overlap
///   handling, async callback completion paths, and <see cref="IClockDayTimeTimer.Start"/>), including UTC
///   calendar-day scheduling constructed without <see cref="System.TimeOnly"/> APIs so the same scenarios run when the
///   library is consumed from <c>netstandard2.0</c> (for example under the unit test
///   <c>net481</c> target).
/// </summary>
[ExcludeFromCodeCoverage]
public class UsingClockDayTimeTimerRegistration : UnitTestBase
{
    /// <summary>
    ///   Initializes a new instance of the <see cref="UsingClockDayTimeTimerRegistration"/> class.
    /// </summary>
    /// <param name="xUnitTestOutputHelper">The xUnit test output helper.</param>
    public UsingClockDayTimeTimerRegistration (ITestOutputHelper xUnitTestOutputHelper)
        : base(xUnitTestOutputHelper)
    {
    }

    /// <summary>
    ///   Verifies <see cref="IDayTimeTimer.ElapsedTime"/> is <c>-1</c> before any callback has started.
    /// </summary>
    [Fact]
    public void RegisterTimeOfDay_Local_BeforeFirstFire_ElapsedTime_IsNegativeOne ()
    {
        Instant utcNow = Instant.FromUtc(2025, 6, 15, 10, 0, 0);
        IPrimeClock clock = new PrimeClock(new FakeClock(utcNow), DateTimeZone.Utc);
        LocalTime target = new(18, 0, 0);
        using IClockDayTimeTimer registration = clock.RegisterTimeOfDay(target,
            static _ => { },
            TestContext.Current.CancellationToken);
        registration.ElapsedTime.Should().Be(-1L);
    }

    /// <summary>
    ///   Verifies <see cref="IDayTimeTimer.ElapsedTime"/> is <c>0</c> while a synchronous callback is executing.
    /// </summary>
    [Fact]
    public void RegisterTimeOfDay_Local_InsideSyncCallback_ElapsedTime_IsZero ()
    {
        Instant utcNow = Instant.FromUtc(2025, 6, 15, 10, 0, 0);
        FakeClock fake = new(utcNow);
        IPrimeClock clock = new PrimeClock(fake, DateTimeZone.Utc);
        LocalTime target = new(18, 0, 0);
        long observedInsideCallback = -2;
        using IClockDayTimeTimer registration = clock.RegisterTimeOfDay(target,
            context =>
            {
                IClockDayTimeTimer dayTimer = (IClockDayTimeTimer)context.Registration;
                observedInsideCallback = dayTimer.ElapsedTime;
            },
            TestContext.Current.CancellationToken);
        MethodInfo onTimerTick = ClockTimerRegistrationTestReflection.GetDayTimeOnTimerTickMethod(
            typeof(ClockDayTimeTimerRegistration));
        fake.Advance(Duration.FromHours(8));
        onTimerTick.Invoke(registration, [null]);
        observedInsideCallback.Should().Be(0L);
    }

    /// <summary>
    ///   Verifies <see cref="IDayTimeTimer.ElapsedTime"/> reflects milliseconds since the last callback start after
    ///   the virtual clock advances.
    /// </summary>
    [Fact]
    public void RegisterTimeOfDay_Local_AfterCallbackAndClockAdvance_ElapsedTime_IsPositive ()
    {
        Instant utcNow = Instant.FromUtc(2025, 6, 15, 10, 0, 0);
        FakeClock fake = new(utcNow);
        IPrimeClock clock = new PrimeClock(fake, DateTimeZone.Utc);
        LocalTime target = new(18, 0, 0);
        using IClockDayTimeTimer registration = clock.RegisterTimeOfDay(target,
            static _ => { },
            TestContext.Current.CancellationToken);
        MethodInfo onTimerTick = ClockTimerRegistrationTestReflection.GetDayTimeOnTimerTickMethod(
            typeof(ClockDayTimeTimerRegistration));
        fake.Advance(Duration.FromHours(8));
        onTimerTick.Invoke(registration, [null]);
        fake.Advance(Duration.FromMilliseconds(150));
        registration.ElapsedTime.Should().BeGreaterThanOrEqualTo(100L).And.BeLessThanOrEqualTo(300L);
    }

    /// <summary>
    ///   Verifies <see cref="IDayTimeTimer.TimeUntilNextCallback"/> is <c>0</c> when the stored next-fire instant is
    ///   not after the current virtual instant (no timer tick required).
    /// </summary>
    [Fact]
    public void RegisterTimeOfDay_Local_NextFireInstantInPast_TimeUntilNext_IsZero ()
    {
        Instant utcNow = Instant.FromUtc(2025, 6, 15, 10, 0, 0);
        FakeClock fake = new(utcNow);
        IPrimeClock clock = new PrimeClock(fake, DateTimeZone.Utc);
        LocalTime target = new(14, 0, 0);
        using IClockDayTimeTimer registration = clock.RegisterTimeOfDay(target,
            static _ => { },
            TestContext.Current.CancellationToken);
        registration.TimeUntilNextCallback.Should().BeGreaterThan(0L);
        fake.Advance(Duration.FromHours(6));
        registration.TimeUntilNextCallback.Should().Be(0L);
    }

    /// <summary>
    ///   Verifies <see cref="IClockDayTimeTimer.Change(LocalTime)"/> while the registration is enabled reschedules so
    ///   <see cref="IDayTimeTimer.TimeUntilNextCallback"/> reflects the new target.
    /// </summary>
    [Fact]
    public void Change_LocalTimeOfDayWhileEnabled_TimeUntilNextReflectsNewTarget ()
    {
        Instant utcNow = Instant.FromUtc(2025, 6, 15, 10, 0, 0);
        IPrimeClock clock = new PrimeClock(new FakeClock(utcNow), DateTimeZone.Utc);
        LocalTime initialTarget = new(18, 0, 0);
        using IClockDayTimeTimer registration = clock.RegisterTimeOfDay(initialTarget,
            static _ => { },
            TestContext.Current.CancellationToken);
        long beforeChange = registration.TimeUntilNextCallback;
        LocalTime newTarget = new(20, 0, 0);
        bool changed = registration.Change(newTarget);
        changed.Should().BeTrue();
        long afterChange = registration.TimeUntilNextCallback;
        afterChange.Should().BeGreaterThan(beforeChange);
    }

    /// <summary>
    ///   Verifies <see cref="IClockDayTimeTimer.Change(LocalTime)"/> returns <c>false</c> after the registration is
    ///   disposed.
    /// </summary>
    [Fact]
    public void Change_AfterDispose_ReturnsFalse ()
    {
        Instant utcNow = Instant.FromUtc(2025, 6, 15, 10, 0, 0);
        IPrimeClock clock = new PrimeClock(new FakeClock(utcNow), DateTimeZone.Utc);
        LocalTime target = new(18, 0, 0);
        IClockDayTimeTimer registration = clock.RegisterTimeOfDay(target,
            static _ => { },
            TestContext.Current.CancellationToken);
        registration.Dispose();
        bool changed = registration.Change(new LocalTime(19, 0, 0));
        changed.Should().BeFalse();
    }

    /// <summary>
    ///   Verifies <see cref="IClockDayTimeTimer.Change(LocalTime)"/> returns <c>false</c> after
    ///   <see cref="IClockTimer.Cancel"/> is called.
    /// </summary>
    [Fact]
    public void Change_AfterCancel_ReturnsFalse ()
    {
        Instant utcNow = Instant.FromUtc(2025, 6, 15, 10, 0, 0);
        IPrimeClock clock = new PrimeClock(new FakeClock(utcNow), DateTimeZone.Utc);
        LocalTime target = new(18, 0, 0);
        IClockDayTimeTimer registration = clock.RegisterTimeOfDay(target,
            static _ => { },
            TestContext.Current.CancellationToken);
        registration.Cancel();
        bool changed = registration.Change(new LocalTime(19, 0, 0));
        changed.Should().BeFalse();
    }

    /// <summary>
    ///   Verifies UTC calendar-day scheduling moves to the next UTC day when the current instant is already past
    ///   today's nominal fire (delay math in the UTC branch of the registration).
    /// </summary>
    [Fact]
    public void UtcSchedule_AfterTodaysTarget_TimeUntilNext_TargetsTomorrowsOccurrence ()
    {
        Instant utcNow = Instant.FromUtc(2025, 6, 15, 14, 0, 0);
        FakeClock fake = new(utcNow);
        IPrimeClock clock = new PrimeClock(fake, DateTimeZone.Utc);
        LocalTime targetUtc = new(12, 0, 0);
        using IClockDayTimeTimer registration = new ClockDayTimeTimerRegistration(clock,
            utcTimeOfDaySchedule: true,
            targetUtc,
            TimerCallbackKind.SimpleAction,
            (Action)(static () => { }),
            null,
            null,
            TestContext.Current.CancellationToken);
        Instant nextExpected = Instant.FromUtc(2025, 6, 16, 12, 0, 0);
        Duration expectedDelay = nextExpected - utcNow;
        long expectedMs = (long)expectedDelay.TotalMilliseconds;
        registration.TimeUntilNextCallback.Should()
            .BeInRange(expectedMs - VirtualClockTimerAssertionToleranceMilliseconds,
                expectedMs + VirtualClockTimerAssertionToleranceMilliseconds);
    }

    /// <summary>
    ///   Verifies a UTC day-time registration that receives a timer tick slightly before today's nominal fire, while
    ///   the callback start is recorded in the same engine pass, rolls the next schedule to the next UTC calendar day
    ///   instead of re-arming the same date with a tiny delay.
    /// </summary>
    [Fact]
    public void UtcSchedule_EarlyTickSameEnginePass_TimeUntilNext_SkipsToNextUtcDay ()
    {
        Instant utcNow = Instant.FromUtc(2025, 6, 15, 11, 59, 59) + Duration.FromMilliseconds(900);
        FakeClock fake = new(utcNow);
        IPrimeClock clock = new PrimeClock(fake, DateTimeZone.Utc);
        LocalTime targetUtc = new(12, 0, 0);
        using IClockDayTimeTimer registration = new ClockDayTimeTimerRegistration(clock,
            utcTimeOfDaySchedule: true,
            targetUtc,
            TimerCallbackKind.SimpleAction,
            (Action)(static () => { }),
            null,
            null,
            TestContext.Current.CancellationToken);
        MethodInfo onTimerTick = ClockTimerRegistrationTestReflection.GetDayTimeOnTimerTickMethod(
            typeof(ClockDayTimeTimerRegistration));
        onTimerTick.Invoke(registration, [null]);
        long msUntilNext = registration.TimeUntilNextCallback;
        long minOneDayLessSkew = (long)Duration.FromHours(23).TotalMilliseconds;
        long maxOneDayPlusSkew = (long)Duration.FromHours(25).TotalMilliseconds;
        msUntilNext.Should().BeInRange(minOneDayLessSkew, maxOneDayPlusSkew);
    }

    /// <summary>
    ///   Verifies <see cref="IDayTimeTimer.ElapsedTime"/> is <c>-1</c> before any callback for a UTC-scheduled
    ///   registration created without <see cref="System.TimeOnly"/> surface APIs.
    /// </summary>
    [Fact]
    public void UtcSchedule_BeforeFirstFire_ElapsedTime_IsNegativeOne ()
    {
        Instant utcNow = Instant.FromUtc(2025, 6, 15, 10, 0, 0);
        IPrimeClock clock = new PrimeClock(new FakeClock(utcNow), DateTimeZone.Utc);
        using IClockDayTimeTimer registration = new ClockDayTimeTimerRegistration(clock,
            utcTimeOfDaySchedule: true,
            new LocalTime(18, 0, 0),
            TimerCallbackKind.SimpleAction,
            (Action)(static () => { }),
            null,
            null,
            TestContext.Current.CancellationToken);
        registration.ElapsedTime.Should().Be(-1L);
    }

    /// <summary>
    ///   Verifies <see cref="IDayTimeTimer.ElapsedTime"/> is <c>0</c> inside a synchronous UTC-scheduled callback tick.
    /// </summary>
    [Fact]
    public void UtcSchedule_InsideSyncCallback_ElapsedTime_IsZero ()
    {
        Instant utcNow = Instant.FromUtc(2025, 6, 15, 10, 0, 0);
        FakeClock fake = new(utcNow);
        IPrimeClock clock = new PrimeClock(fake, DateTimeZone.Utc);
        long observedInsideCallback = -2;
        using IClockDayTimeTimer registration = new ClockDayTimeTimerRegistration(clock,
            utcTimeOfDaySchedule: true,
            new LocalTime(18, 0, 0),
            TimerCallbackKind.ContextAction,
            (Action<ClockTimerCallbackContext>)(context =>
            {
                IClockDayTimeTimer dayTimer = (IClockDayTimeTimer)context.Registration;
                observedInsideCallback = dayTimer.ElapsedTime;
            }),
            null,
            null,
            TestContext.Current.CancellationToken);
        MethodInfo onTimerTick = ClockTimerRegistrationTestReflection.GetDayTimeOnTimerTickMethod(
            typeof(ClockDayTimeTimerRegistration));
        fake.Advance(Duration.FromHours(8));
        onTimerTick.Invoke(registration, [null]);
        observedInsideCallback.Should().Be(0L);
    }

    /// <summary>
    ///   Verifies <see cref="IDayTimeTimer.ElapsedTime"/> increases after a UTC-scheduled tick when the virtual clock
    ///   advances.
    /// </summary>
    [Fact]
    public void UtcSchedule_AfterCallbackAndClockAdvance_ElapsedTime_IsPositive ()
    {
        Instant utcNow = Instant.FromUtc(2025, 6, 15, 10, 0, 0);
        FakeClock fake = new(utcNow);
        IPrimeClock clock = new PrimeClock(fake, DateTimeZone.Utc);
        using IClockDayTimeTimer registration = new ClockDayTimeTimerRegistration(clock,
            utcTimeOfDaySchedule: true,
            new LocalTime(18, 0, 0),
            TimerCallbackKind.SimpleAction,
            (Action)(static () => { }),
            null,
            null,
            TestContext.Current.CancellationToken);
        MethodInfo onTimerTick = ClockTimerRegistrationTestReflection.GetDayTimeOnTimerTickMethod(
            typeof(ClockDayTimeTimerRegistration));
        fake.Advance(Duration.FromHours(8));
        onTimerTick.Invoke(registration, [null]);
        fake.Advance(Duration.FromMilliseconds(150));
        registration.ElapsedTime.Should().BeGreaterThanOrEqualTo(100L).And.BeLessThanOrEqualTo(300L);
    }

    /// <summary>
    ///   Verifies <see cref="IDayTimeTimer.TimeUntilNextCallback"/> becomes <c>0</c> when the stored next-fire instant
    ///   is no longer strictly after <c>now</c> for a UTC-scheduled registration.
    /// </summary>
    [Fact]
    public void UtcSchedule_NextFireInstantInPast_TimeUntilNext_IsZero ()
    {
        Instant utcNow = Instant.FromUtc(2025, 6, 15, 10, 0, 0);
        FakeClock fake = new(utcNow);
        IPrimeClock clock = new PrimeClock(fake, DateTimeZone.Utc);
        using IClockDayTimeTimer registration = new ClockDayTimeTimerRegistration(clock,
            utcTimeOfDaySchedule: true,
            new LocalTime(14, 0, 0),
            TimerCallbackKind.SimpleAction,
            (Action)(static () => { }),
            null,
            null,
            TestContext.Current.CancellationToken);
        registration.TimeUntilNextCallback.Should().BeGreaterThan(0L);
        fake.Advance(Duration.FromHours(6));
        registration.TimeUntilNextCallback.Should().Be(0L);
    }

    /// <summary>
    ///   Verifies <see cref="IClockDayTimeTimer.Change(LocalTime)"/> on a UTC calendar-day registration reschedules from
    ///   tomorrow's prior target to a later wall time that still falls on the current UTC calendar day.
    /// </summary>
    [Fact]
    public void UtcSchedule_Change_LocalTimeWhileEnabled_TimeUntilNextReflectsNewTarget ()
    {
        Instant utcNow = Instant.FromUtc(2025, 6, 15, 12, 30, 0);
        FakeClock fake = new(utcNow);
        IPrimeClock clock = new PrimeClock(fake, DateTimeZone.Utc);
        using IClockDayTimeTimer registration = new ClockDayTimeTimerRegistration(clock,
            utcTimeOfDaySchedule: true,
            new LocalTime(12, 0, 0),
            TimerCallbackKind.SimpleAction,
            (Action)(static () => { }),
            null,
            null,
            TestContext.Current.CancellationToken);
        long beforeChange = registration.TimeUntilNextCallback;
        beforeChange.Should().BeGreaterThan((long)Duration.FromHours(20).TotalMilliseconds);
        bool changed = registration.Change(new LocalTime(13, 0, 0));
        changed.Should().BeTrue();
        long afterChange = registration.TimeUntilNextCallback;
        long expectedThirtyMinutesMs = (long)Duration.FromMinutes(30).TotalMilliseconds;
        afterChange.Should()
            .BeInRange(expectedThirtyMinutesMs - VirtualClockTimerAssertionToleranceMilliseconds,
                expectedThirtyMinutesMs + VirtualClockTimerAssertionToleranceMilliseconds);
    }

    /// <summary>
    ///   Millisecond wall-clock budget for thread synchronization waits in callback-overlap tests.
    /// </summary>
    private static readonly TimeSpan ThreadSyncWaitMargin = TimeSpan.FromSeconds(5);

    /// <summary>
    ///   Verifies that <see cref="TimerCallbackKind.ContextActionWithToken"/> passes the registration
    ///   cancellation token into the user callback when <c>OnTimerTick</c> runs.
    /// </summary>
    [Fact]
    public void UtcSchedule_OnTimerTick_ContextActionWithToken_PassesRegistrationCancellationToken ()
    {
        Instant utcNow = Instant.FromUtc(2025, 6, 15, 10, 0, 0);
        FakeClock fake = new(utcNow);
        IPrimeClock clock = new PrimeClock(fake, DateTimeZone.Utc);
        LocalTime targetUtc = new(18, 0, 0);
        using ManualResetEventSlim done = new(false);
        using CancellationTokenSource cts = new();
        CancellationToken? receivedToken = null;
        using IClockDayTimeTimer registration = new ClockDayTimeTimerRegistration(clock,
            utcTimeOfDaySchedule: true,
            targetUtc,
            TimerCallbackKind.ContextActionWithToken,
            (Action<ClockTimerCallbackContext, CancellationToken>)((_, ct) =>
            {
                receivedToken = ct;
                done.Set();
            }),
            null,
            null,
            cts.Token);
        MethodInfo onTimerTick = ClockTimerRegistrationTestReflection.GetDayTimeOnTimerTickMethod(
            typeof(ClockDayTimeTimerRegistration));
        fake.Advance(Duration.FromHours(8));
        onTimerTick.Invoke(registration, [null]);
        done.Wait(ThreadSyncWaitMargin, TestContext.Current.CancellationToken).Should().BeTrue();
        receivedToken.Should().NotBeNull();
        receivedToken!.Value.Should().Be(cts.Token);
    }

#pragma warning disable xUnit1031 // Overlap tests host OnTimerTick on a worker; parent thread drives the second tick and release.
#pragma warning disable xUnit1051 // Coordination waits use CancellationToken.None so test teardown cannot strand callbacks before release.

    /// <summary>
    ///   Verifies that with <see cref="ConcurrentTriggerProcessing.Skip"/>, a second <c>OnTimerTick</c>
    ///   while the first callback is still running does not run the user callback again for that overlap.
    /// </summary>
    [Fact]
    public void UtcSchedule_ConcurrentSkip_OverlappingOnTimerTick_SecondCallbackSkipped ()
    {
        Instant utcNow = Instant.FromUtc(2025, 6, 15, 10, 0, 0);
        FakeClock fake = new(utcNow);
        IPrimeClock clock = new PrimeClock(fake, DateTimeZone.Utc);
        LocalTime targetUtc = new(18, 0, 0);
        DayTimeTimerOptions options = new()
        {
            ConcurrentTriggerProcessing = ConcurrentTriggerProcessing.Skip,
        };
        using ManualResetEventSlim enteredFirstCallback = new(false);
        using ManualResetEventSlim releaseFirstCallback = new(false);
        using ManualResetEventSlim firstCallbackExited = new(false);
        int invokeCount = 0;
        Action userCallback = () =>
        {
            if (Interlocked.Increment(ref invokeCount) != 1)
            {
                return;
            }

            enteredFirstCallback.Set();
            releaseFirstCallback.Wait(ThreadSyncWaitMargin, CancellationToken.None).Should().BeTrue();
            firstCallbackExited.Set();
        };
        using IClockDayTimeTimer registration = new ClockDayTimeTimerRegistration(clock,
            utcTimeOfDaySchedule: true,
            targetUtc,
            TimerCallbackKind.SimpleAction,
            userCallback,
            null,
            options,
            TestContext.Current.CancellationToken);
        MethodInfo onTimerTick = ClockTimerRegistrationTestReflection.GetDayTimeOnTimerTickMethod(
            typeof(ClockDayTimeTimerRegistration));
        fake.Advance(Duration.FromHours(8));
        Task firstTickWork = Task.Run(() => onTimerTick.Invoke(registration, [null]));
        enteredFirstCallback.Wait(ThreadSyncWaitMargin, CancellationToken.None).Should().BeTrue();
        onTimerTick.Invoke(registration, [null]);
        releaseFirstCallback.Set();
        firstCallbackExited.Wait(ThreadSyncWaitMargin, CancellationToken.None).Should().BeTrue();
        firstTickWork.Wait(ThreadSyncWaitMargin).Should().BeTrue();
        Volatile.Read(ref invokeCount).Should().Be(1);
    }

    /// <summary>
    ///   Verifies that with <see cref="ConcurrentTriggerProcessing.RunSequentially"/>, a second
    ///   <c>OnTimerTick</c> while the first callback is still running defers work and the registration
    ///   eventually runs the deferred callback (exercising <c>ProcessCallback</c>).
    /// </summary>
    [Fact]
    public void UtcSchedule_ConcurrentRunSequentially_OverlappingOnTimerTick_DeferredCallbackRuns ()
    {
        Instant utcNow = Instant.FromUtc(2025, 6, 15, 10, 0, 0);
        FakeClock fake = new(utcNow);
        IPrimeClock clock = new PrimeClock(fake, DateTimeZone.Utc);
        LocalTime targetUtc = new(18, 0, 0);
        DayTimeTimerOptions options = new()
        {
            ConcurrentTriggerProcessing = ConcurrentTriggerProcessing.RunSequentially,
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
            releaseFirstCallback.Wait(ThreadSyncWaitMargin, CancellationToken.None).Should().BeTrue();
        };
        using IClockDayTimeTimer registration = new ClockDayTimeTimerRegistration(clock,
            utcTimeOfDaySchedule: true,
            targetUtc,
            TimerCallbackKind.SimpleAction,
            userCallback,
            null,
            options,
            TestContext.Current.CancellationToken);
        MethodInfo onTimerTick = ClockTimerRegistrationTestReflection.GetDayTimeOnTimerTickMethod(
            typeof(ClockDayTimeTimerRegistration));
        fake.Advance(Duration.FromHours(8));
        Task firstTickWork = Task.Run(() => onTimerTick.Invoke(registration, [null]));
        enteredFirstCallback.Wait(ThreadSyncWaitMargin, CancellationToken.None).Should().BeTrue();
        onTimerTick.Invoke(registration, [null]);
        releaseFirstCallback.Set();
        SpinWait.SpinUntil(() => Volatile.Read(ref invokeCount) >= 2, ThreadSyncWaitMargin).Should().BeTrue();
        firstTickWork.Wait(ThreadSyncWaitMargin).Should().BeTrue();
    }

    /// <summary>
    ///   Verifies that <see cref="TimerCallbackKind.SimpleAsync"/> with a synchronously completed
    ///   <see cref="ValueTask"/> runs the synchronous completion loop in <c>RunAsync</c> twice when a
    ///   sequential run is pending after an overlapping tick.
    /// </summary>
    [Fact]
    public void UtcSchedule_SimpleAsync_SyncCompleteWithPendingSequential_InvokesTwiceInOneRunAsync ()
    {
        Instant utcNow = Instant.FromUtc(2025, 6, 15, 10, 0, 0);
        FakeClock fake = new(utcNow);
        IPrimeClock clock = new PrimeClock(fake, DateTimeZone.Utc);
        LocalTime targetUtc = new(18, 0, 0);
        DayTimeTimerOptions options = new()
        {
            ConcurrentTriggerProcessing = ConcurrentTriggerProcessing.RunSequentially,
        };
        using ManualResetEventSlim innerStarted = new(false);
        using ManualResetEventSlim allowSecondTick = new(false);
        int callCount = 0;
        Func<CancellationToken, ValueTask> run = _ =>
        {
            int n = Interlocked.Increment(ref callCount);
            if (n == 1)
            {
                innerStarted.Set();
                allowSecondTick.Wait(ThreadSyncWaitMargin, CancellationToken.None).Should().BeTrue();
            }

            return default;
        };
        using IClockDayTimeTimer registration = new ClockDayTimeTimerRegistration(clock,
            utcTimeOfDaySchedule: true,
            targetUtc,
            TimerCallbackKind.SimpleAsync,
            run,
            null,
            options,
            TestContext.Current.CancellationToken);
        MethodInfo onTimerTick = ClockTimerRegistrationTestReflection.GetDayTimeOnTimerTickMethod(
            typeof(ClockDayTimeTimerRegistration));
        fake.Advance(Duration.FromHours(8));
        Task firstTickWork = Task.Run(() => onTimerTick.Invoke(registration, [null]));
        innerStarted.Wait(ThreadSyncWaitMargin, CancellationToken.None).Should().BeTrue();
        onTimerTick.Invoke(registration, [null]);
        allowSecondTick.Set();
        SpinWait.SpinUntil(() => Volatile.Read(ref callCount) >= 2, ThreadSyncWaitMargin).Should().BeTrue();
        firstTickWork.Wait(ThreadSyncWaitMargin).Should().BeTrue();
    }

    /// <summary>
    ///   Verifies that <see cref="TimerCallbackKind.ContextAsync"/> with
    ///   <see cref="ConcurrentTriggerProcessing.RunSequentially"/>, when a second tick arrives while the
    ///   first callback is still incomplete, eventually runs the deferred callback (async completion and
    ///   <c>Task.Run</c> continuation path).
    /// </summary>
    [Fact]
    public void UtcSchedule_ContextAsync_OverlappingOnTimerTickWhileIncomplete_RunsDeferredCallback ()
    {
        Instant utcNow = Instant.FromUtc(2025, 6, 15, 10, 0, 0);
        FakeClock fake = new(utcNow);
        IPrimeClock clock = new PrimeClock(fake, DateTimeZone.Utc);
        LocalTime targetUtc = new(18, 0, 0);
        DayTimeTimerOptions options = new()
        {
            ConcurrentTriggerProcessing = ConcurrentTriggerProcessing.RunSequentially,
        };
        using ManualResetEventSlim enteredAsyncBody = new(false);
        using ManualResetEventSlim allowSecondTick = new(false);
        int enteredCount = 0;
        Func<ClockTimerCallbackContext, CancellationToken, ValueTask> callback = async (_, ct) =>
        {
            if (Interlocked.Increment(ref enteredCount) != 1)
            {
                return;
            }

            enteredAsyncBody.Set();
            allowSecondTick.Wait(ThreadSyncWaitMargin, CancellationToken.None).Should().BeTrue();
            await Task.Delay(50, ct).ConfigureAwait(false);
        };
        using IClockDayTimeTimer registration = new ClockDayTimeTimerRegistration(clock,
            utcTimeOfDaySchedule: true,
            targetUtc,
            TimerCallbackKind.ContextAsync,
            callback,
            null,
            options,
            TestContext.Current.CancellationToken);
        MethodInfo onTimerTick = ClockTimerRegistrationTestReflection.GetDayTimeOnTimerTickMethod(
            typeof(ClockDayTimeTimerRegistration));
        fake.Advance(Duration.FromHours(8));
        Task firstTickWork = Task.Run(() => onTimerTick.Invoke(registration, [null]));
        enteredAsyncBody.Wait(ThreadSyncWaitMargin, CancellationToken.None).Should().BeTrue();
        onTimerTick.Invoke(registration, [null]);
        allowSecondTick.Set();
        SpinWait.SpinUntil(() => Volatile.Read(ref enteredCount) >= 2, ThreadSyncWaitMargin).Should().BeTrue();
        firstTickWork.Wait(ThreadSyncWaitMargin).Should().BeTrue();
    }

#pragma warning restore xUnit1051
#pragma warning restore xUnit1031

    /// <summary>
    ///   Verifies that <see cref="IClockDayTimeTimer.Start"/> after <see cref="IClockTimer.Stop"/> returns
    ///   <c>true</c> and restores an active registration.
    /// </summary>
    [Fact]
    public void RegisterTimeOfDay_StopThenStart_ReturnsTrueAndRestoresActive ()
    {
        Instant utcNow = Instant.FromUtc(2025, 6, 15, 10, 0, 0);
        FakeClock fake = new(utcNow);
        IPrimeClock clock = new PrimeClock(fake, DateTimeZone.Utc);
        LocalTime farTarget = new(20, 0, 0);
        using IClockDayTimeTimer timer = clock.RegisterTimeOfDay(farTarget,
            static () => { },
            TestContext.Current.CancellationToken);
        timer.State.Should().Be(TimerState.Active);
        timer.Stop().Should().BeTrue();
        timer.State.Should().Be(TimerState.Disabled);
        timer.Start().Should().BeTrue();
        timer.State.Should().Be(TimerState.Active);
    }

    /// <summary>
    ///   Verifies that <see cref="IClockDayTimeTimer.Start"/> returns <c>false</c> when the registration is
    ///   already active.
    /// </summary>
    [Fact]
    public void RegisterTimeOfDay_StartWhenActive_ReturnsFalse ()
    {
        Instant utcNow = Instant.FromUtc(2025, 6, 15, 10, 0, 0);
        FakeClock fake = new(utcNow);
        IPrimeClock clock = new PrimeClock(fake, DateTimeZone.Utc);
        LocalTime farTarget = new(20, 0, 0);
        using IClockDayTimeTimer timer = clock.RegisterTimeOfDay(farTarget,
            static () => { },
            TestContext.Current.CancellationToken);
        timer.State.Should().Be(TimerState.Active);
        timer.Start().Should().BeFalse();
    }

    /// <summary>
    ///   Verifies that <see cref="IClockDayTimeTimer.Start"/> returns <c>false</c> when the registration is
    ///   cancelled.
    /// </summary>
    [Fact]
    public void RegisterTimeOfDay_StartWhenCancelled_ReturnsFalse ()
    {
        Instant utcNow = Instant.FromUtc(2025, 6, 15, 10, 0, 0);
        FakeClock fake = new(utcNow);
        IPrimeClock clock = new PrimeClock(fake, DateTimeZone.Utc);
        LocalTime farTarget = new(20, 0, 0);
        using (IClockDayTimeTimer timer = clock.RegisterTimeOfDay(farTarget,
                   static () => { },
                   TestContext.Current.CancellationToken))
        {
            timer.Cancel();
            timer.Start().Should().BeFalse();
        }
    }

#if NET
    /// <summary>
    ///   Verifies <see cref="IClockDayTimeTimer.Change(LocalTimeOfDay)"/> on a local registration applies the
    ///   <see cref="TimeOnly"/> tick resolution path and reschedules to a later fire.
    /// </summary>
    [Fact]
    public void Change_LocalTimeOfDayNetSurfaceWhileEnabled_TimeUntilNextReflectsNewTarget ()
    {
        Instant utcNow = Instant.FromUtc(2025, 6, 15, 10, 0, 0);
        IPrimeClock clock = new PrimeClock(new FakeClock(utcNow), DateTimeZone.Utc);
        LocalTimeOfDay initialTarget = new(new TimeOnly(18, 0, 0));
        using IClockDayTimeTimer registration = clock.RegisterTimeOfDay(initialTarget,
            static _ => { },
            TestContext.Current.CancellationToken);
        long beforeChange = registration.TimeUntilNextCallback;
        LocalTimeOfDay newTarget = new(new TimeOnly(20, 0, 0));
        bool changed = registration.Change(newTarget);
        changed.Should().BeTrue();
        long afterChange = registration.TimeUntilNextCallback;
        afterChange.Should().BeGreaterThan(beforeChange);
    }

    /// <summary>
    ///   Verifies <see cref="IClockDayTimeTimer.Change(UtcTimeOfDay)"/> on a UTC registration applies the
    ///   <see cref="TimeOnly"/> tick resolution path and reschedules to a later fire.
    /// </summary>
    [Fact]
    public void Change_UtcTimeOfDayNetSurfaceWhileEnabled_TimeUntilNextReflectsNewTarget ()
    {
        Instant utcNow = Instant.FromUtc(2025, 6, 15, 10, 0, 0);
        IPrimeClock clock = new PrimeClock(new FakeClock(utcNow), DateTimeZone.Utc);
        UtcTimeOfDay initialTarget = new(new TimeOnly(18, 0, 0));
        using IClockDayTimeTimer registration = clock.RegisterTimeOfDay(initialTarget,
            static _ => { },
            TestContext.Current.CancellationToken);
        long beforeChange = registration.TimeUntilNextCallback;
        UtcTimeOfDay newTarget = new(new TimeOnly(20, 0, 0));
        bool changed = registration.Change(newTarget);
        changed.Should().BeTrue();
        long afterChange = registration.TimeUntilNextCallback;
        afterChange.Should().BeGreaterThan(beforeChange);
    }
#endif
}
