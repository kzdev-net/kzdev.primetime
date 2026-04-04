// Copyright (c) Kevin Zehrer
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using AwesomeAssertions;
using AwesomeAssertions.Specialized;
using KZDev.PrimeTime.Tests;
using NodaTime;
using NodaTime.Testing;

namespace KZDev.PrimeTime.UnitTests;

//################################################################################
/// <summary>
///   Unit tests for <see cref="IPrimeClock"/> and <see cref="PrimeClock"/>.
///   Verifies that all "now" members return values consistent with a known instant and time zone.
///   Uses <see cref="FakeClock"/> from NodaTime.Testing for deterministic tests with
///   <see cref="PrimeClock"/>.
/// </summary>
public class UsingIPrimeClock : UnitTestBase
{
    // Use a short sleep so tests remain fast, but keep it above typical timer/scheduler resolution
    // (≈1–15ms on most platforms) so that elapsed time can be measured reliably and deterministically.
        //----------------------------------------------------------------------------
private const int SleepTestDurationMilliseconds = 30;

    // Allow some jitter in Sleep measurements to account for OS scheduling and GC pauses without
    // masking real regressions; 15ms has proven sufficient and stable on common CI environments.
    private const int SleepTimingToleranceMilliseconds = 15;

    // Upper bound guard to catch pathological delays (e.g., thread starvation) without failing the
    // test for minor noise; 100ms is well above the expected 30ms + 15ms tolerance window.
    private const int SleepUpperBoundToleranceMilliseconds = 100;

    // Slightly longer duration for async delay tests to accommodate Task scheduler overhead while
    // still keeping the test suite fast.
    private const int DelayAsyncTestDurationMilliseconds = 40;

    // Tighter tolerance for DelayAsync since Task.Delay is typically more precise than Thread.Sleep,
    // but still leaves room for minor scheduling variability on busy systems.
    private const int DelayAsyncTimingToleranceMilliseconds = 10;

    // Upper bound for DelayAsync elapsed time to avoid flaky failures when the task scheduler
    // or system is slightly slow; 30ms has proven sufficient across targets.
    private const int DelayAsyncUpperBoundToleranceMilliseconds = 30;
    //----------------------------------------------------------------------------

    #region Constructors/Finalizers

    //----------------------------------------------------------------------------
    /// <summary>
    ///   Initializes a new instance of the <see cref="UsingIPrimeClock"/> class.
    /// </summary>
    /// <param name="xUnitTestOutputHelper">
    ///   The xUnit test output helper that can be used to output test messages.
    /// </param>
    public UsingIPrimeClock (ITestOutputHelper xUnitTestOutputHelper)
        : base(xUnitTestOutputHelper)
    {
    }
    //----------------------------------------------------------------------------

    #endregion Constructors/Finalizers

    //----------------------------------------------------------------------------
    /// <summary>
    ///   Asserts that an elapsed duration lies within the expected range (expected minus lower
    ///   tolerance, and optionally expected plus upper tolerance).
    /// </summary>
    /// <param name="elapsed">
    ///   The measured elapsed duration.
    /// </param>
    /// <param name="expected">
    ///   The expected duration (e.g. requested sleep or delay).
    /// </param>
    /// <param name="lowerTolerance">
    ///   Allowed shortfall so elapsed may be as low as expected minus this value.
    /// </param>
    /// <param name="upperTolerance">
    ///   Optional allowed excess; if set, elapsed must be at most expected plus this value.
    /// </param>
    private static void AssertElapsedTimeInRange (Duration elapsed,
        Duration expected,
        Duration lowerTolerance,
        Duration? upperTolerance = null)
    {
        elapsed.Should().BeGreaterThanOrEqualTo(expected.Minus(lowerTolerance));
        if (upperTolerance.HasValue)
            elapsed.Should().BeLessThanOrEqualTo(expected.Plus(upperTolerance.Value));
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies that <see cref="IPrimeClock"/> extends <see cref="IPrimeTime"/>.
    /// </summary>
    [Fact]
    public void IPrimeClock_ExtendsIPrimeTime ()
    {
        typeof(IPrimeClock).GetInterfaces().Should().Contain(typeof(IPrimeTime));
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies that <see cref="PrimeClock"/> implements <see cref="IPrimeClock"/>.
    /// </summary>
    [Fact]
    public void PrimeClock_ImplementsIPrimeClock ()
    {
        IPrimeClock clock = new PrimeClock();
        clock.Should().NotBeNull();
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies that <see cref="PrimeClock"/> with a <see cref="FakeClock"/> at a fixed instant
    ///   yields consistent "now" values: Instant matches UtcNow.ToInstant(), LocalZonedNow and
    ///   LocalNow derive from the same instant, and time/date components match the zoned values.
    /// </summary>
    [Fact]
    public void PrimeClock_WithFakeClock_NowMembersAreConsistentWithFixedInstant ()
    {
        Instant instant = Instant.FromUtc(2025, 3, 7, 12, 0);
        DateTimeZone localZone = DateTimeZoneProviders.Bcl.GetSystemDefault();
        IPrimeClock clock = new PrimeClock(new FakeClock(instant), localZone);

        clock.NowInstant.Should().Be(instant);
        clock.UtcNow.ToInstant().Should().Be(instant);
        clock.UtcZonedNow.ToInstant().Should().Be(instant);
        clock.LocalZonedNow.ToInstant().Should().Be(instant);
        clock.LocalZonedNow.Zone.Should().Be(localZone);
        clock.LocalNow.Should().Be(clock.LocalZonedNow.LocalDateTime);
        clock.UtcNowTime.Should().Be(clock.UtcNow.TimeOfDay);
        clock.UtcNowDate.Should().Be(clock.UtcNow.Date);
        clock.LocalNowTime.Should().Be(clock.LocalZonedNow.TimeOfDay);
        clock.LocalNowDate.Should().Be(clock.LocalZonedNow.Date);
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies that <see cref="PrimeClock"/> UTC "now" members are consistent: zone is UTC,
    ///   UtcZonedNow is in UTC, and UtcNow time/date components match UtcNowTime and UtcNowDate.
    ///   Time is asserted within a 2-second tolerance because each property read obtains a fresh
    ///   instant; date must match exactly (same calendar day).
    /// </summary>
    [Fact]
    public void PrimeClock_UtcNowMembersAreConsistent ()
    {
        IPrimeClock clock = new PrimeClock();
        ZonedDateTime utcNow = clock.UtcNow;
        LocalTime utcNowTime = clock.UtcNowTime;
        LocalDate utcNowDate = clock.UtcNowDate;

        utcNow.Zone.Should().Be(DateTimeZone.Utc);
        clock.UtcZonedNow.Zone.Should().Be(DateTimeZone.Utc);
        long nanoDiff = Math.Abs(utcNow.TimeOfDay.NanosecondOfDay - utcNowTime.NanosecondOfDay);
        nanoDiff.Should().BeLessThanOrEqualTo(2_000_000_000L, "time of day should be within 2 seconds across sequential reads");
        utcNow.Date.Year.Should().Be(utcNowDate.Year);
        utcNow.Date.Month.Should().Be(utcNowDate.Month);
        utcNow.Date.Day.Should().Be(utcNowDate.Day);
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies that <see cref="PrimeClock"/> local "now" members are consistent:
    ///   LocalZonedNow has the system default zone and its LocalDateTime, TimeOfDay, and Date
    ///   are internally consistent.
    /// </summary>
    [Fact]
    public void PrimeClock_LocalNowMembersAreConsistent ()
    {
        IPrimeClock clock = new PrimeClock();
        ZonedDateTime localZonedNow = clock.LocalZonedNow;

        localZonedNow.Zone.Should().Be(DateTimeZoneProviders.Bcl.GetSystemDefault());
        localZonedNow.LocalDateTime.TimeOfDay.Should().Be(localZonedNow.TimeOfDay);
        localZonedNow.LocalDateTime.Date.Should().Be(localZonedNow.Date);
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies that at a fixed instant, <see cref="PrimeClock"/> with a <see cref="FakeClock"/>
    ///   returns exact time-only and date-only "now" values: UtcNowTime/UtcNowDate, and
    ///   LocalNowTime/LocalNowDate matching the same instant when using UTC as the local zone.
    /// </summary>
    [Fact]
    public void PrimeClock_WithFakeClock_TimeOnlyAndDateOnlyReturnExactValuesAtFixedInstant ()
    {
        Instant instant = Instant.FromUtc(2025, 3, 7, 12, 30, 45);
        DateTimeZone utc = DateTimeZone.Utc;
        IPrimeClock clock = new PrimeClock(new FakeClock(instant), utc);

        clock.UtcNowTime.Should().Be(new(12, 30, 45));
        clock.UtcNowDate.Should().Be(new(2025, 3, 7));
        clock.LocalNowTime.Should().Be(clock.UtcNowTime);
        clock.LocalNowDate.Should().Be(clock.UtcNowDate);
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies that advancing a <see cref="FakeClock"/> used by <see cref="PrimeClock"/> updates
    ///   all "now" values (Instant, UtcNow, LocalZonedNow, time/date components) to the new instant.
    /// </summary>
    [Fact]
    public void PrimeClock_WithFakeClock_AdvanceUpdatesNow ()
    {
        Instant initial = Instant.FromUtc(2025, 3, 7, 10, 0, 0);
        Duration advanceBy = Duration.FromHours(2).Plus(Duration.FromMinutes(30));
        Instant expectedAfter = initial.Plus(advanceBy);
        DateTimeZone utc = DateTimeZone.Utc;
        FakeClock fakeClock = new(initial);
        IPrimeClock clock = new PrimeClock(fakeClock, utc);

        clock.NowInstant.Should().Be(initial);
        clock.UtcNowDate.Should().Be(new(2025, 3, 7));
        clock.UtcNowTime.Should().Be(new(10, 0, 0));

        fakeClock.Advance(advanceBy);

        clock.NowInstant.Should().Be(expectedAfter);
        clock.UtcNow.ToInstant().Should().Be(expectedAfter);
        clock.UtcNowDate.Should().Be(new(2025, 3, 7));
        clock.UtcNowTime.Should().Be(new(12, 30, 0));
        clock.LocalZonedNow.ToInstant().Should().Be(expectedAfter);
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies that all "now" values from <see cref="PrimeClock"/> are recent (within the last 5 seconds).
    /// </summary>
    [Fact]
    public void PrimeClock_NowMembersAreRecent ()
    {
        IPrimeClock clock = new PrimeClock();
        Instant before = global::NodaTime.SystemClock.Instance.GetCurrentInstant().Minus(Duration.FromSeconds(5));
        Instant after = global::NodaTime.SystemClock.Instance.GetCurrentInstant().Plus(Duration.FromSeconds(5));

        clock.NowInstant.Should().BeGreaterThan(before).And.BeLessThan(after);
    }
    //----------------------------------------------------------------------------

    #region Sleep (TimeSpan and Duration)

    /// <summary>
    ///   Verifies that <see cref="IPrimeTime.Sleep(TimeSpan)"/> with zero completes
    ///   immediately (does not throw and elapsed time is under a small threshold).
    /// </summary>
    [Fact]
    public void Sleep_TimeSpanZero_CompletesWithoutThrowing ()
    {
        IPrimeClock clock = new PrimeClock();
        Instant before = clock.NowInstant;
        clock.Sleep(TimeSpan.Zero);
        Instant after = clock.NowInstant;
        (after - before).Should().BeLessThan(Duration.FromMilliseconds(100));
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies that <see cref="IPrimeClock.Sleep(Duration)"/> with zero completes
    ///   immediately (does not throw and elapsed time is under a small threshold).
    /// </summary>
    [Fact]
    public void Sleep_DurationZero_CompletesWithoutThrowing ()
    {
        IPrimeClock clock = new PrimeClock();
        Instant before = clock.NowInstant;
        clock.Sleep(Duration.Zero);
        Instant after = clock.NowInstant;
        (after - before).Should().BeLessThan(Duration.FromMilliseconds(100));
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies that <see cref="IPrimeClock.Sleep(Duration)"/> with a short duration
    ///   suspends for at least that duration (within timing tolerance) and does not block
    ///   significantly longer than expected (upper bound check).
    /// </summary>
    [Fact]
    public void Sleep_Duration_SuspendsForAtLeastDuration ()
    {
        IPrimeClock clock = new PrimeClock();
        Duration sleepDuration = Duration.FromMilliseconds(SleepTestDurationMilliseconds);
        Instant before = clock.NowInstant;
        clock.Sleep(sleepDuration);
        Instant after = clock.NowInstant;
        Duration elapsed = after - before;
        AssertElapsedTimeInRange(elapsed,
            sleepDuration,
            Duration.FromMilliseconds(SleepTimingToleranceMilliseconds),
            Duration.FromMilliseconds(SleepUpperBoundToleranceMilliseconds));
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies that <see cref="IPrimeClock.Sleep(Duration)"/> with a negative duration
    ///   completes immediately (negative is treated as zero); no throw and elapsed time is small.
    /// </summary>
    [Fact]
    public void Sleep_NegativeDuration_CompletesImmediately ()
    {
        IPrimeClock clock = new PrimeClock();
        Duration negativeDuration = Duration.FromSeconds(-1);
        Instant before = clock.NowInstant;
        clock.Sleep(negativeDuration);
        Instant after = clock.NowInstant;
        Duration elapsed = after - before;
        elapsed.Should().BeGreaterThanOrEqualTo(Duration.Zero);
        elapsed.Should().BeLessThan(Duration.FromSeconds(1));
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies that <see cref="IPrimeClock.Sleep(Duration)"/> with a very small duration (1ms)
    ///   completes without throwing. Timing precision may be unreliable at this scale; elapsed
    ///   time is only asserted to be non-negative.
    /// </summary>
    [Fact]
    public void Sleep_VerySmallDuration_CompletesWithoutThrowing ()
    {
        IPrimeClock clock = new PrimeClock();
        Duration sleepDuration = Duration.FromMilliseconds(1);
        Instant before = clock.NowInstant;
        clock.Sleep(sleepDuration);
        Instant after = clock.NowInstant;
        (after - before).Should().BeGreaterThanOrEqualTo(Duration.Zero);
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies that when sleep duration equals the timing tolerance, the minimum accepted
    ///   elapsed time is zero (tolerance boundary is coherent).
    /// </summary>
    [Fact]
    public void Sleep_DurationAtToleranceBoundary_ElapsedAtLeastZero ()
    {
        IPrimeClock clock = new PrimeClock();
        Duration sleepDuration = Duration.FromMilliseconds(SleepTimingToleranceMilliseconds);
        Instant before = clock.NowInstant;
        clock.Sleep(sleepDuration);
        Instant after = clock.NowInstant;
        AssertElapsedTimeInRange(after - before,
            sleepDuration,
            Duration.FromMilliseconds(SleepTimingToleranceMilliseconds));
    }
    //----------------------------------------------------------------------------

    #endregion Sleep (TimeSpan and Duration)

    #region DelayAsync (IPrimeTime and Duration)

    /// <summary>
    ///   Verifies that <see cref="IPrimeTime.DelayAsync(TimeSpan)"/> with zero completes
    ///   immediately (does not throw and elapsed time is under a small threshold).
    /// </summary>
    [Fact]
    public async Task DelayAsync_TimeSpanZero_CompletesWithoutThrowing ()
    {
        IPrimeClock clock = new PrimeClock();
        Instant before = clock.NowInstant;
        await clock.DelayAsync(TimeSpan.Zero, TestContext.Current.CancellationToken);
        Instant after = clock.NowInstant;
        (after - before).Should().BeLessThan(Duration.FromMilliseconds(100));
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies that <see cref="IPrimeClock.DelayAsync(Duration)"/> with zero completes
    ///   immediately (does not throw and elapsed time is under a small threshold).
    /// </summary>
    [Fact]
    public async Task DelayAsync_DurationZero_CompletesWithoutThrowing ()
    {
        IPrimeClock clock = new PrimeClock();
        Instant before = clock.NowInstant;
        await clock.DelayAsync(Duration.Zero, TestContext.Current.CancellationToken);
        Instant after = clock.NowInstant;
        (after - before).Should().BeLessThan(Duration.FromMilliseconds(100));
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies that <see cref="IPrimeClock.DelayAsync(Duration)"/> with a short duration
    ///   completes after approximately that duration.
    /// </summary>
    [Fact]
    public async Task DelayAsync_Duration_CompletesAfterDuration ()
    {
        IPrimeClock clock = new PrimeClock();
        Duration delay = Duration.FromMilliseconds(DelayAsyncTestDurationMilliseconds);
        Instant before = clock.NowInstant;
        await clock.DelayAsync(delay, TestContext.Current.CancellationToken);
        Instant after = clock.NowInstant;
        Duration elapsed = after - before;
        AssertElapsedTimeInRange(elapsed,
            delay,
            Duration.FromMilliseconds(DelayAsyncTimingToleranceMilliseconds),
            Duration.FromMilliseconds(DelayAsyncUpperBoundToleranceMilliseconds));
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies that <see cref="IPrimeClock.DelayAsync(Duration, CancellationToken)"/> with a
    ///   very small duration (1ms) completes without throwing. Timing precision may be unreliable
    ///   at this scale; elapsed time is only asserted to be non-negative.
    /// </summary>
    [Fact]
    public async Task DelayAsync_VerySmallDuration_CompletesWithoutThrowing ()
    {
        IPrimeClock clock = new PrimeClock();
        Duration delay = Duration.FromMilliseconds(1);
        Instant before = clock.NowInstant;
        await clock.DelayAsync(delay, TestContext.Current.CancellationToken);
        Instant after = clock.NowInstant;
        (after - before).Should().BeGreaterThanOrEqualTo(Duration.Zero);
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies that when delay duration equals the timing tolerance, the minimum accepted
    ///   elapsed time is zero (tolerance boundary is coherent).
    /// </summary>
    [Fact]
    public async Task DelayAsync_DurationAtToleranceBoundary_ElapsedAtLeastZero ()
    {
        IPrimeClock clock = new PrimeClock();
        Duration delay = Duration.FromMilliseconds(DelayAsyncTimingToleranceMilliseconds);
        Instant before = clock.NowInstant;
        await clock.DelayAsync(delay, TestContext.Current.CancellationToken);
        Instant after = clock.NowInstant;
        AssertElapsedTimeInRange(after - before,
            delay,
            Duration.FromMilliseconds(DelayAsyncTimingToleranceMilliseconds));
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies that <see cref="IPrimeClock.DelayAsync(Duration, CancellationToken)"/>
    ///   throws <see cref="OperationCanceledException"/> when the token is cancelled before
    ///   the delay completes.
    /// </summary>
    [Fact]
    public async Task DelayAsync_DurationWithToken_CancelledEarly_ThrowsOperationCanceledException ()
    {
        IPrimeClock clock = new PrimeClock();
        using CancellationTokenSource cts = new();
        using CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cts.Token,
            TestContext.Current.CancellationToken);
        CancellationToken linked = linkedCts.Token;
#pragma warning disable xUnit1051 // xUnit1051 warns if TestContext.Current.CancellationToken is not passed directly, but it is already included in the linked token used here.
        Task delayTask = clock.DelayAsync(Duration.FromSeconds(10), linked);
#pragma warning restore xUnit1051
#if NET
        await cts.CancelAsync();
#else
        cts.Cancel();
#endif
        Func<Task> act = async () => await delayTask;
        ExceptionAssertions<OperationCanceledException>? throwAssertion = await act.Should().ThrowAsync<OperationCanceledException>();
#if NET5_0_OR_GREATER
        throwAssertion.Which.CancellationToken.Should().Be(linked);
#endif
    }
    //----------------------------------------------------------------------------

    #endregion DelayAsync (IPrimeTime and Duration)

    #region GetTimeCancellationToken (Duration)

    /// <summary>
    ///   Verifies that <see cref="IPrimeClock.GetTimeCancellationToken(Duration)"/> returns
    ///   a source whose token is initially active and becomes cancelled after one combined delay
    ///   that runs past the requested cancellation duration.
    /// </summary>
    [Fact]
    public async Task GetTimeCancellationToken_Duration_ExpiresAfterTime ()
    {
        IPrimeClock clock = new PrimeClock();
        Duration cancelAfter = Duration.FromMilliseconds(120);
        using TimeCancellationTokenSource timeCts = clock.GetTimeCancellationToken(cancelAfter);
        timeCts.Token.IsCancellationRequested.Should().BeFalse();
        // Use one delay that runs past cancelAfter rather than a "wait until just before expiry,
        // assert false, then wait a little longer" split-delay sequence. On net481 and
        // netstandard2.0, coarse timer resolution plus thread-pool scheduling latency can resume
        // the test after the cancellation deadline has already passed, making the intermediate
        // "not cancelled yet" assertion flaky. The single-delay pattern avoids that race while
        // still verifying that the token is eventually cancelled.
        Duration additionalDelayForCancellation = Duration.FromMilliseconds(80);
        await clock.DelayAsync(cancelAfter + additionalDelayForCancellation, TestContext.Current.CancellationToken);
        timeCts.Token.IsCancellationRequested.Should().BeTrue();
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies that <see cref="IPrimeClock.GetTimeCancellationToken(Duration)"/> with a
    ///   duration exceeding the maximum delay supported by
    ///   <see cref="CancellationTokenSource"/> (<see cref="int.MaxValue"/> milliseconds)
    ///   does not throw; the duration is clamped to that maximum internally.
    /// </summary>
    [Fact]
    public void GetTimeCancellationToken_DurationExceedingCancellationMax_DoesNotThrow ()
    {
        IPrimeClock clock = new PrimeClock();
        Duration exceedingMax =
            Duration.FromMilliseconds(int.MaxValue).Plus(Duration.FromSeconds(1));
        using TimeCancellationTokenSource timeCts = clock.GetTimeCancellationToken(exceedingMax);
        timeCts.Token.IsCancellationRequested.Should().BeFalse();
    }
    //----------------------------------------------------------------------------

    #endregion GetTimeCancellationToken (Duration)

    #region LinkTimeCancellationToken (Duration)

    /// <summary>
    ///   Verifies that <see cref="IPrimeClock.LinkTimeCancellationToken(Duration, CancellationToken)"/>
    ///   returns a source whose token cancels when the linked token is cancelled.
    /// </summary>
    [Fact]
    public void LinkTimeCancellationToken_DurationAndToken_CancelsWhenLinkedCancels ()
    {
        IPrimeClock clock = new PrimeClock();
        using CancellationTokenSource userCts = new();
        using TimeCancellationTokenSource linkedSource =
            clock.LinkTimeCancellationToken(Duration.FromSeconds(10), userCts.Token);
        linkedSource.Token.IsCancellationRequested.Should().BeFalse();
        userCts.Cancel();
        linkedSource.Token.IsCancellationRequested.Should().BeTrue();
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies that <see cref="IPrimeClock.LinkTimeCancellationToken(Duration, CancellationToken, CancellationToken)"/>
    ///   returns a source whose token cancels when either linked token is cancelled.
    /// </summary>
    [Fact]
    public void LinkTimeCancellationToken_DurationAndTwoTokens_CancelsWhenOneTokenCancels ()
    {
        IPrimeClock clock = new PrimeClock();
        using CancellationTokenSource cts1 = new();
        using CancellationTokenSource cts2 = new();
        using TimeCancellationTokenSource linkedSource =
            clock.LinkTimeCancellationToken(Duration.FromSeconds(10), cts1.Token, cts2.Token);
        linkedSource.Token.IsCancellationRequested.Should().BeFalse();
        cts1.Cancel();
        linkedSource.Token.IsCancellationRequested.Should().BeTrue();
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies that <see cref="IPrimeClock.LinkTimeCancellationToken(Duration, CancellationToken[])"/>
    ///   returns a source whose token cancels when any of the linked tokens is cancelled.
    /// </summary>
    [Fact]
    public void LinkTimeCancellationToken_DurationAndParams_CancelsWhenOneTokenCancels ()
    {
        IPrimeClock clock = new PrimeClock();
        using CancellationTokenSource cts1 = new();
        using CancellationTokenSource cts2 = new();
        using TimeCancellationTokenSource linkedSource =
            clock.LinkTimeCancellationToken(Duration.FromSeconds(10), cts1.Token, cts2.Token);
        linkedSource.Token.IsCancellationRequested.Should().BeFalse();
        cts2.Cancel();
        linkedSource.Token.IsCancellationRequested.Should().BeTrue();
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies that a linked time cancellation source's token expires after the specified
    ///   duration when the linked token is not cancelled.
    /// </summary>
    [Fact]
    public async Task LinkTimeCancellationToken_Duration_ExpiresAfterTimeWhenLinkedNotCancelled ()
    {
        IPrimeClock clock = new PrimeClock();
        using CancellationTokenSource neverCancelled = new();
        using TimeCancellationTokenSource linkedSource =
            clock.LinkTimeCancellationToken(Duration.FromMilliseconds(50), neverCancelled.Token);
        linkedSource.Token.IsCancellationRequested.Should().BeFalse();
        await clock.DelayAsync(Duration.FromMilliseconds(70), TestContext.Current.CancellationToken);
        linkedSource.Token.IsCancellationRequested.Should().BeTrue();
    }
    //----------------------------------------------------------------------------

    #endregion LinkTimeCancellationToken (Duration)
}
//################################################################################

