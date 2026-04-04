// Copyright (c) Kevin Zehrer
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using AwesomeAssertions;
using KZDev.PrimeTime.Tests;

namespace KZDev.SystemClock.PrimeTime.UnitTests;

//################################################################################
/// <summary>
///   Unit tests for <see cref="PrimeClock"/> (IPrimeTime delay and time-cancellation members).
///   Different test genres are grouped in regions.
/// </summary>
/// <remarks>
///   Delay and time-cancellation scenarios use wall-clock time; use <see cref="PrimeTestClock"/> for fully
///   deterministic tests.
/// </remarks>
public class UsingPrimeClock : UnitTestBase
{
    #region Constructors/Finalizers

    //----------------------------------------------------------------------------
    /// <summary>
    ///   Initializes a new instance of the <see cref="UsingPrimeClock"/> class.
    /// </summary>
    /// <param name="xUnitTestOutputHelper">
    ///   The xUnit test output helper that can be used to output test messages.
    /// </param>
    public UsingPrimeClock (ITestOutputHelper xUnitTestOutputHelper)
        : base(xUnitTestOutputHelper)
    {
    }
    //----------------------------------------------------------------------------

    #endregion Constructors/Finalizers

    #region Sleep

    /// <summary>
    ///   Verifies that <see cref="IPrimeTime.Sleep(TimeSpan)"/> with zero completes immediately
    ///   and does not throw.
    /// </summary>
    [Fact]
    public void Sleep_TimeSpanZero_CompletesWithoutThrowing ()
    {
        IPrimeClock clock = new PrimeClock();
        Action act = () => clock.Sleep(TimeSpan.Zero);
        act.Should().NotThrow();
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies that <see cref="IPrimeTime.Sleep(int)"/> with zero completes immediately
    ///   and does not throw.
    /// </summary>
    [Fact]
    public void Sleep_MillisecondsZero_CompletesWithoutThrowing ()
    {
        IPrimeClock clock = new PrimeClock();
        Action act = () => clock.Sleep(0);
        act.Should().NotThrow();
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies that <see cref="IPrimeTime.Sleep(TimeSpan)"/> with a short duration
    ///   suspends for at least that duration (within timing tolerance).
    /// </summary>
    [Fact]
    public void Sleep_TimeSpan_SuspendsForAtLeastDuration ()
    {
        IPrimeClock clock = new PrimeClock();
        TimeSpan sleepDuration = TimeSpan.FromMilliseconds(30);
        DateTimeOffset before = DateTimeOffset.UtcNow;
        clock.Sleep(sleepDuration);
        DateTimeOffset after = DateTimeOffset.UtcNow;
        (after - before).Should().BeGreaterThanOrEqualTo(sleepDuration.Subtract(TimeSpan.FromMilliseconds(20)));
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies that <see cref="IPrimeTime.Sleep(int)"/> with a short duration in ms
    ///   suspends for at least that duration (within timing tolerance).
    /// </summary>
    [Fact]
    public void Sleep_Milliseconds_SuspendsForAtLeastDuration ()
    {
        IPrimeClock clock = new PrimeClock();
        const int sleepMs = 30;
        DateTimeOffset before = DateTimeOffset.UtcNow;
        clock.Sleep(sleepMs);
        DateTimeOffset after = DateTimeOffset.UtcNow;
        (after - before).TotalMilliseconds.Should().BeGreaterThanOrEqualTo(sleepMs - 20);
    }
    //----------------------------------------------------------------------------

    #endregion Sleep

    #region DelayAsync

    /// <summary>
    ///   Verifies that <see cref="IPrimeTime.DelayAsync(TimeSpan)"/> with zero completes
    ///   without throwing.
    /// </summary>
    [Fact]
    public async Task DelayAsync_TimeSpanZero_CompletesWithoutThrowing ()
    {
        IPrimeClock clock = new PrimeClock();
        Func<Task> act = async () => await clock.DelayAsync(TimeSpan.Zero);
        await act.Should().NotThrowAsync();
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies that <see cref="IPrimeTime.DelayAsync(int)"/> with zero completes
    ///   without throwing.
    /// </summary>
    [Fact]
    public async Task DelayAsync_MillisecondsZero_CompletesWithoutThrowing ()
    {
        IPrimeClock clock = new PrimeClock();
        Func<Task> act = async () => await clock.DelayAsync(0);
        await act.Should().NotThrowAsync();
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies that <see cref="IPrimeTime.DelayAsync(TimeSpan)"/> with a short duration
    ///   completes after approximately that duration.
    /// </summary>
    [Fact]
    public async Task DelayAsync_TimeSpan_CompletesAfterDuration ()
    {
        IPrimeClock clock = new PrimeClock();
        TimeSpan delay = TimeSpan.FromMilliseconds(40);
        DateTimeOffset before = DateTimeOffset.UtcNow;
        await clock.DelayAsync(delay, TestContext.Current.CancellationToken);
        DateTimeOffset after = DateTimeOffset.UtcNow;
        (after - before).Should().BeGreaterThanOrEqualTo(delay.Subtract(TimeSpan.FromMilliseconds(25)));
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies that <see cref="IPrimeTime.DelayAsync(int)"/> with a short duration in ms
    ///   completes after approximately that duration.
    /// </summary>
    [Fact]
    public async Task DelayAsync_Milliseconds_CompletesAfterDuration ()
    {
        IPrimeClock clock = new PrimeClock();
        const int delayMs = 40;
        DateTimeOffset before = DateTimeOffset.UtcNow;
        await clock.DelayAsync(delayMs, TestContext.Current.CancellationToken);
        DateTimeOffset after = DateTimeOffset.UtcNow;
        (after - before).TotalMilliseconds.Should().BeGreaterThanOrEqualTo(delayMs - 25);
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies that <see cref="IPrimeTime.DelayAsync(TimeSpan, CancellationToken)"/>
    ///   throws <see cref="OperationCanceledException"/> when the token is cancelled before
    ///   the delay completes.
    /// </summary>
    [Fact]
    public async Task DelayAsync_TimeSpanWithToken_CancelledEarly_ThrowsOperationCanceledException ()
    {
        IPrimeClock clock = new PrimeClock();
        using CancellationTokenSource cts = new();
        CancellationToken linked = CancellationTokenSource.CreateLinkedTokenSource(cts.Token,
            TestContext.Current.CancellationToken).Token;
#pragma warning disable xUnit1051 // Linked token includes TestContext.Current.CancellationToken for test cancellation
        Task delayTask = clock.DelayAsync(TimeSpan.FromSeconds(10), linked);
#pragma warning restore xUnit1051
        cts.Cancel();
        Func<Task> act = async () => await delayTask;
        await act.Should().ThrowAsync<OperationCanceledException>();
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies that <see cref="IPrimeTime.DelayAsync(int, CancellationToken)"/>
    ///   throws <see cref="OperationCanceledException"/> when the token is cancelled before
    ///   the delay completes.
    /// </summary>
    [Fact]
    public async Task DelayAsync_MillisecondsWithToken_CancelledEarly_ThrowsOperationCanceledException ()
    {
        IPrimeClock clock = new PrimeClock();
        using CancellationTokenSource cts = new();
        CancellationToken linked = CancellationTokenSource.CreateLinkedTokenSource(cts.Token,
            TestContext.Current.CancellationToken).Token;
#pragma warning disable xUnit1051 // Linked token includes TestContext.Current.CancellationToken for test cancellation
        Task delayTask = clock.DelayAsync(10_000, linked);
#pragma warning restore xUnit1051
        cts.Cancel();
        Func<Task> act = async () => await delayTask;
        await act.Should().ThrowAsync<OperationCanceledException>();
    }
    //----------------------------------------------------------------------------

    #endregion DelayAsync

    #region GetTimeCancellationToken

    /// <summary>
    ///   Verifies that <see cref="IPrimeTime.GetTimeCancellationToken(TimeSpan)"/> returns
    ///   a source whose token becomes cancelled after the specified time.
    /// </summary>
    [Fact]
    public async Task GetTimeCancellationToken_TimeSpan_ExpiresAfterTime ()
    {
        IPrimeClock clock = new PrimeClock();
        TimeSpan cancelAfter = TimeSpan.FromMilliseconds(50);
        using TimeCancellationTokenSource timeCts = clock.GetTimeCancellationToken(cancelAfter);
        timeCts.Token.IsCancellationRequested.Should().BeFalse();
        TimeSpan additionalDelayForCancellation = TimeSpan.FromMilliseconds(80);
        await clock.DelayAsync(cancelAfter.Add(additionalDelayForCancellation), TestContext.Current.CancellationToken);
        timeCts.Token.IsCancellationRequested.Should().BeTrue();
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies that <see cref="IPrimeTime.GetTimeCancellationToken(int)"/> returns
    ///   a source whose token becomes cancelled after the specified milliseconds.
    /// </summary>
    [Fact]
    public async Task GetTimeCancellationToken_Milliseconds_ExpiresAfterTime ()
    {
        IPrimeClock clock = new PrimeClock();
        const int cancelAfterMs = 50;
        using TimeCancellationTokenSource timeCts = clock.GetTimeCancellationToken(cancelAfterMs);
        timeCts.Token.IsCancellationRequested.Should().BeFalse();
        const int additionalDelayForCancellationMs = 80;
        await clock.DelayAsync(cancelAfterMs + additionalDelayForCancellationMs, TestContext.Current.CancellationToken);
        timeCts.Token.IsCancellationRequested.Should().BeTrue();
    }
    //----------------------------------------------------------------------------

    #endregion GetTimeCancellationToken

    #region LinkTimeCancellationToken

    /// <summary>
    ///   Verifies that <see cref="IPrimeTime.LinkTimeCancellationToken(TimeSpan, CancellationToken)"/>
    ///   returns a source whose token cancels when either the time expires or the linked token is cancelled.
    /// </summary>
    [Fact]
    public async Task LinkTimeCancellationToken_TimeSpanAndToken_CancelsWhenLinkedCancels ()
    {
        IPrimeClock clock = new PrimeClock();
        using CancellationTokenSource userCts = new();
        using TimeCancellationTokenSource linkedSource =
            clock.LinkTimeCancellationToken(TimeSpan.FromSeconds(10), userCts.Token);
        linkedSource.Token.IsCancellationRequested.Should().BeFalse();
        userCts.Cancel();
        linkedSource.Token.IsCancellationRequested.Should().BeTrue();
        await Task.CompletedTask;
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies that <see cref="IPrimeTime.LinkTimeCancellationToken(TimeSpan, CancellationToken, CancellationToken)"/>
    ///   returns a source whose token cancels when the time expires or either linked token is cancelled.
    /// </summary>
    [Fact]
    public void LinkTimeCancellationToken_TimeSpanAndTwoTokens_CancelsWhenOneTokenCancels ()
    {
        IPrimeClock clock = new PrimeClock();
        using CancellationTokenSource cts1 = new();
        using CancellationTokenSource cts2 = new();
        using TimeCancellationTokenSource linkedSource =
            clock.LinkTimeCancellationToken(TimeSpan.FromSeconds(10), cts1.Token, cts2.Token);
        linkedSource.Token.IsCancellationRequested.Should().BeFalse();
        cts1.Cancel();
        linkedSource.Token.IsCancellationRequested.Should().BeTrue();
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies that <see cref="IPrimeTime.LinkTimeCancellationToken(TimeSpan, CancellationToken[])"/>
    ///   returns a source whose token cancels when the time expires or any of the linked tokens is cancelled.
    /// </summary>
    [Fact]
    public void LinkTimeCancellationToken_TimeSpanAndParams_CancelsWhenOneTokenCancels ()
    {
        IPrimeClock clock = new PrimeClock();
        using CancellationTokenSource cts1 = new();
        using CancellationTokenSource cts2 = new();
        using TimeCancellationTokenSource linkedSource =
            clock.LinkTimeCancellationToken(TimeSpan.FromSeconds(10), cts1.Token, cts2.Token);
        linkedSource.Token.IsCancellationRequested.Should().BeFalse();
        cts2.Cancel();
        linkedSource.Token.IsCancellationRequested.Should().BeTrue();
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies that <see cref="IPrimeTime.LinkTimeCancellationToken(int, CancellationToken)"/>
    ///   returns a source whose token cancels when either the time expires or the linked token is cancelled.
    /// </summary>
    [Fact]
    public void LinkTimeCancellationToken_MillisecondsAndToken_CancelsWhenLinkedCancels ()
    {
        IPrimeClock clock = new PrimeClock();
        using CancellationTokenSource userCts = new();
        using TimeCancellationTokenSource linkedSource = clock.LinkTimeCancellationToken(10_000, userCts.Token);
        linkedSource.Token.IsCancellationRequested.Should().BeFalse();
        userCts.Cancel();
        linkedSource.Token.IsCancellationRequested.Should().BeTrue();
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies that a linked time cancellation source's token expires after the specified time
    ///   when the linked token is not cancelled.
    /// </summary>
    [Fact]
    public async Task LinkTimeCancellationToken_ExpiresAfterTimeWhenLinkedNotCancelled ()
    {
        IPrimeClock clock = new PrimeClock();
        using CancellationTokenSource neverCancelled = new();
        using TimeCancellationTokenSource linkedSource =
            clock.LinkTimeCancellationToken(TimeSpan.FromMilliseconds(50), neverCancelled.Token);
        linkedSource.Token.IsCancellationRequested.Should().BeFalse();
        await clock.DelayAsync(TimeSpan.FromMilliseconds(140), TestContext.Current.CancellationToken);
        linkedSource.Token.IsCancellationRequested.Should().BeTrue();
    }
    //----------------------------------------------------------------------------

    #endregion LinkTimeCancellationToken
}
//################################################################################


