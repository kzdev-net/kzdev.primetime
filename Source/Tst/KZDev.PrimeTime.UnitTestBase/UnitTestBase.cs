// Copyright (c) Kevin Zehrer
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;

using Xunit;

namespace KZDev.PrimeTime.Tests;

//################################################################################
/// <summary>
///   The base class for all unit tests.
/// </summary>
[ExcludeFromCodeCoverage]
public abstract class UnitTestBase : TestBase
{
    //----------------------------------------------------------------------------
    /// <summary>
    ///   Millisecond half-range (<c>±</c>) for inclusive virtual-clock timer assertions that use AwesomeAssertions
    ///   <c>BeInRange</c> around an expected millisecond value.
    /// </summary>
    protected const int VirtualClockTimerAssertionToleranceMilliseconds = 2500;

    /// <summary>
    ///   Fast virtual-time-per-real-second rate for bounded IPrimeTestClock.RunFor tests.
    /// </summary>
    protected static readonly TimeSpan RunForTestFastPerSecondRate = TimeSpan.FromHours(1);

    /// <summary>
    ///   Wall-clock guard while polling for a bounded IPrimeTestClock.RunFor run to finish.
    /// </summary>
    protected static readonly TimeSpan RunForTestWaitTimeout = TimeSpan.FromSeconds(5);

    /// <summary>
    ///   Real time between cooperative poll attempts in test-clock wait helpers.
    /// </summary>
    private static readonly TimeSpan TestClockPollInterval = TimeSpan.FromMilliseconds(25);

    //----------------------------------------------------------------------------
    /// <summary>
    ///   Asynchronously polls until <paramref name="conditionMet"/> returns <c>true</c> or the timeout elapses.
    /// </summary>
    /// <param name="conditionMet">Returns whether the wait condition is satisfied.</param>
    /// <param name="timeout">Maximum real time to wait.</param>
    /// <param name="timeoutMessage">Message for the thrown <see cref="TimeoutException"/>.</param>
    /// <param name="cancellationToken">Cancellation token for the test run.</param>
    /// <returns>A task that completes when the condition is met.</returns>
    /// <exception cref="TimeoutException">
    ///   Thrown when <paramref name="conditionMet"/> remains <c>false</c> until the timeout elapses.
    /// </exception>
    private static async Task WaitUntilConditionAsync (Func<bool> conditionMet,
        TimeSpan timeout, string timeoutMessage, CancellationToken cancellationToken)
    {
        Stopwatch elapsed = Stopwatch.StartNew();
        while (!conditionMet())
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (elapsed.Elapsed >= timeout)
            {
                throw new TimeoutException(timeoutMessage);
            }

            TimeSpan remaining = timeout - elapsed.Elapsed;
            TimeSpan pollDelay = remaining < TestClockPollInterval ? remaining : TestClockPollInterval;
            if (pollDelay > TimeSpan.Zero)
            {
                await Task.Delay(pollDelay, cancellationToken).ConfigureAwait(false);
            }
        }
    }
    //----------------------------------------------------------------------------
    /// <summary>
    ///   Converts a wall-clock timeout to milliseconds for
    ///   <see cref="SemaphoreSlim.WaitAsync(int, CancellationToken)"/>, capping at
    ///   <see cref="int.MaxValue"/> so <see cref="TimeSpan.TotalMilliseconds"/> cannot overflow
    ///   <see cref="int"/> when passed to the runtime API.
    /// </summary>
    /// <param name="timeout">The maximum time to wait.</param>
    /// <returns>
    ///   A non-negative millisecond count suitable for <see cref="SemaphoreSlim.WaitAsync(int, CancellationToken)"/>.
    /// </returns>
    private static int GetSemaphoreWaitTimeoutMilliseconds (TimeSpan timeout)
    {
        return (int)Math.Min(int.MaxValue, timeout.TotalMilliseconds);
    }
    //----------------------------------------------------------------------------
    /// <summary>
    ///   Millisecond timeout for interval-timer tests that keep an async callback alive until
    ///   cancelled or released: <paramref name="waitMargin"/> plus one second, converted via
    ///   <see cref="GetSemaphoreWaitTimeoutMilliseconds"/>.
    /// </summary>
    /// <param name="waitMargin">
    ///   The same extra margin each test class uses for timing tolerance (interval timer test
    ///   <c>WaitMargin</c> fields).
    /// </param>
    /// <returns>
    ///   A millisecond count for <see cref="SemaphoreSlim.WaitAsync(int, CancellationToken)"/>.
    /// </returns>
    protected static int GetAsyncTimerCallbackHoldTimeoutMilliseconds (TimeSpan waitMargin)
    {
        return GetSemaphoreWaitTimeoutMilliseconds(waitMargin + TimeSpan.FromSeconds(1));
    }
    //----------------------------------------------------------------------------
    /// <summary>
    ///   Computes the wall-clock wait budget for overlapping real-clock interval callbacks to start
    ///   or release under CI thread-pool load.
    /// </summary>
    /// <param name="waitMargin">Extra wait time beyond expected delay to avoid flaky failures.</param>
    /// <param name="repeatInterval">Repeat interval for the overlapping timer registration.</param>
    /// <returns>
    ///   <paramref name="waitMargin"/> + <paramref name="repeatInterval"/> + <paramref name="waitMargin"/>.
    /// </returns>
    protected static TimeSpan GetOverlapSynchronizationWait (TimeSpan waitMargin, TimeSpan repeatInterval) =>
        waitMargin + repeatInterval + waitMargin;
    //----------------------------------------------------------------------------
    /// <summary>
    ///   Computes the wall-clock wait budget for an overlapping callback blocked until the test thread
    ///   releases it. Must exceed <see cref="GetOverlapSynchronizationWait"/> so the main thread can
    ///   finish observing overlap and run assertions before the callback-side wait times out.
    /// </summary>
    /// <param name="waitMargin">Extra wait time beyond expected delay to avoid flaky failures.</param>
    /// <param name="repeatInterval">Repeat interval for the overlapping timer registration.</param>
    /// <returns>
    ///   <see cref="GetOverlapSynchronizationWait"/> + <paramref name="waitMargin"/>.
    /// </returns>
    protected static TimeSpan GetOverlapCallbackReleaseWait (TimeSpan waitMargin, TimeSpan repeatInterval) =>
        GetOverlapSynchronizationWait(waitMargin, repeatInterval) + waitMargin;
    //----------------------------------------------------------------------------
    /// <summary>
    ///   Validates that <paramref name="value"/> is not less than <see cref="TimeSpan.Zero"/>.
    /// </summary>
    /// <param name="value">The time span to validate.</param>
    /// <param name="paramName">The parameter name for exception reporting.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    ///   Thrown when <paramref name="value"/> is less than <see cref="TimeSpan.Zero"/>.
    /// </exception>
    private static void ValidateNonNegativeTimeSpan (TimeSpan value, string paramName)
    {
        if (value < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(paramName, value,
                "Value must be greater than or equal to TimeSpan.Zero.");
        }
    }
    //----------------------------------------------------------------------------
    /// <summary>
    ///   Adds two non-negative time spans using checked tick arithmetic.
    /// </summary>
    /// <param name="first">The first summand.</param>
    /// <param name="second">The second summand.</param>
    /// <returns>The sum of <paramref name="first"/> and <paramref name="second"/>.</returns>
    /// <exception cref="OverflowException">
    ///   Thrown when the sum exceeds <see cref="TimeSpan"/> range.
    /// </exception>
    private static TimeSpan AddCheckedTimeSpans (TimeSpan first, TimeSpan second)
    {
        checked
        {
            return TimeSpan.FromTicks(first.Ticks + second.Ticks);
        }
    }
    //----------------------------------------------------------------------------
    /// <summary>
    ///   Computes a wall-clock wait budget for a real-clock timer callback expected after
    ///   <paramref name="expectedDelay"/>.
    /// </summary>
    /// <param name="expectedDelay">Expected elapsed time before the callback should enter user code.</param>
    /// <param name="waitMargin">Extra wait time beyond expected delay to avoid flaky failures.</param>
    /// <returns>
    ///   <paramref name="expectedDelay"/> + 2 × <paramref name="waitMargin"/>.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">
    ///   Thrown when <paramref name="expectedDelay"/> or <paramref name="waitMargin"/> is less than
    ///   <see cref="TimeSpan.Zero"/>.
    /// </exception>
    /// <exception cref="OverflowException">
    ///   Thrown when the computed timeout exceeds <see cref="TimeSpan"/> range.
    /// </exception>
    /// <remarks>
    ///   Two margins are used: one for slack beyond the expected delay, and a second for CI
    ///   thread-pool scheduling latency before the callback enters user code.
    /// </remarks>
    protected static TimeSpan GetCallbackWaitTimeout (TimeSpan expectedDelay, TimeSpan waitMargin)
    {
        ValidateNonNegativeTimeSpan(expectedDelay, nameof(expectedDelay));
        ValidateNonNegativeTimeSpan(waitMargin, nameof(waitMargin));
        return AddCheckedTimeSpans(expectedDelay, AddCheckedTimeSpans(waitMargin, waitMargin));
    }
    //----------------------------------------------------------------------------
    /// <summary>
    ///   Computes the wall-clock wait budget for the first real-clock interval callback to start
    ///   under CI thread-pool load.
    /// </summary>
    /// <param name="shortDelay">Initial timer delay before the first callback.</param>
    /// <param name="waitMargin">Extra wait time beyond expected delay to avoid flaky failures.</param>
    /// <returns>
    ///   <paramref name="shortDelay"/> + 2 × <paramref name="waitMargin"/>.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">
    ///   Thrown when <paramref name="shortDelay"/> or <paramref name="waitMargin"/> is less than
    ///   <see cref="TimeSpan.Zero"/>.
    /// </exception>
    /// <exception cref="OverflowException">
    ///   Thrown when the computed timeout exceeds <see cref="TimeSpan"/> range.
    /// </exception>
    protected static TimeSpan GetFirstCallbackWaitTimeout (TimeSpan shortDelay, TimeSpan waitMargin) =>
        GetCallbackWaitTimeout(shortDelay, waitMargin);
    //----------------------------------------------------------------------------
    /// <summary>
    ///   Computes a wall-clock wait budget for timer state to settle after a callback completes
    ///   under CI thread-pool load.
    /// </summary>
    /// <param name="waitMargin">Extra wait time beyond expected delay to avoid flaky failures.</param>
    /// <returns>
    ///   2 × <paramref name="waitMargin"/>.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">
    ///   Thrown when <paramref name="waitMargin"/> is less than <see cref="TimeSpan.Zero"/>.
    /// </exception>
    /// <exception cref="OverflowException">
    ///   Thrown when the computed timeout exceeds <see cref="TimeSpan"/> range.
    /// </exception>
    protected static TimeSpan GetStateSettleWaitTimeout (TimeSpan waitMargin)
    {
        ValidateNonNegativeTimeSpan(waitMargin, nameof(waitMargin));
        return AddCheckedTimeSpans(waitMargin, waitMargin);
    }
    //----------------------------------------------------------------------------
    /// <summary>
    ///   Polls until <paramref name="conditionMet"/> returns <c>true</c> or the timeout elapses, waiting between
    ///   attempts so the loop does not busy-spin.
    /// </summary>
    /// <param name="conditionMet">Returns whether the wait condition is satisfied.</param>
    /// <param name="timeout">Maximum real time to wait.</param>
    /// <param name="timeoutMessage">Message for the thrown <see cref="TimeoutException"/>.</param>
    /// <param name="cancellationToken">Cancellation token for the test run.</param>
    /// <exception cref="TimeoutException">
    ///   Thrown when <paramref name="conditionMet"/> remains <c>false</c> until the timeout elapses.
    /// </exception>
    protected static void WaitUntilCondition (Func<bool> conditionMet, TimeSpan timeout,
        string timeoutMessage, CancellationToken cancellationToken)
    {
        Stopwatch elapsed = Stopwatch.StartNew();
        while (!conditionMet())
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (elapsed.Elapsed >= timeout)
            {
                throw new TimeoutException(timeoutMessage);
            }

            TimeSpan remaining = timeout - elapsed.Elapsed;
            TimeSpan pollDelay = remaining < TestClockPollInterval ? remaining : TestClockPollInterval;
            WaitForPollInterval(pollDelay, cancellationToken);
        }
    }
    //----------------------------------------------------------------------------
    /// <summary>
    ///   Blocks for up to <paramref name="pollDelay"/> between synchronous poll attempts, honoring
    ///   <paramref name="cancellationToken"/> when it can be canceled.
    /// </summary>
    /// <param name="pollDelay">Maximum real time to wait before the next poll attempt.</param>
    /// <param name="cancellationToken">Cancellation token for the test run.</param>
    private static void WaitForPollInterval (TimeSpan pollDelay, CancellationToken cancellationToken)
    {
        if (pollDelay <= TimeSpan.Zero)
        {
            return;
        }

        if (!cancellationToken.CanBeCanceled)
        {
            Thread.Sleep(pollDelay);
            return;
        }

        bool signaled = cancellationToken.WaitHandle.WaitOne(pollDelay);
        if (signaled)
        {
            cancellationToken.ThrowIfCancellationRequested();
        }
    }
    //----------------------------------------------------------------------------
    /// <summary>
    ///   Polls until <paramref name="isRunning"/> returns <c>false</c> or the timeout elapses.
    /// </summary>
    /// <param name="isRunning">
    ///   Returns whether the automatic test clock runner is still active.
    /// </param>
    /// <param name="timeout">Maximum real time to wait.</param>
    /// <param name="cancellationToken">Cancellation token for the test run.</param>
    /// <exception cref="TimeoutException">
    ///   Thrown when <paramref name="isRunning"/> remains <c>true</c> until the timeout elapses.
    /// </exception>
    protected static void WaitUntilClockStopped (
        Func<bool> isRunning,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        WaitUntilCondition(
            () => !isRunning(),
            timeout,
            "Timed out waiting for the test clock to stop running after "
                + timeout.TotalSeconds.ToString("g0", CultureInfo.InvariantCulture)
                + " seconds.",
            cancellationToken);
    }
    //----------------------------------------------------------------------------
    /// <summary>
    ///   Asynchronously polls until <paramref name="isRunning"/> returns <c>false</c> or the timeout elapses.
    /// </summary>
    /// <param name="isRunning">
    ///   Returns whether the automatic test clock runner is still active.
    /// </param>
    /// <param name="timeout">Maximum real time to wait.</param>
    /// <param name="cancellationToken">Cancellation token for the test run.</param>
    /// <returns>A task that completes when the clock has stopped running.</returns>
    /// <exception cref="TimeoutException">
    ///   Thrown when <paramref name="isRunning"/> remains <c>true</c> until the timeout elapses.
    /// </exception>
    protected static Task WaitUntilClockStoppedAsync (
        Func<bool> isRunning,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        return WaitUntilConditionAsync(
            () => !isRunning(),
            timeout,
            "Timed out waiting for the test clock to stop running after "
                + timeout.TotalSeconds.ToString("g0", CultureInfo.InvariantCulture)
                + " seconds.",
            cancellationToken);
    }
    //----------------------------------------------------------------------------

    #region Constructors/Finalizers

    //----------------------------------------------------------------------------
    /// <summary>
    ///   Initializes a new instance of the <see cref="UnitTestBase"/> class.
    /// </summary>
    /// <param name="xUnitTestOutputHelper">
    ///   The Xunit test output helper that can be used to output test messages.
    /// </param>
    /// <exception cref="ArgumentNullException">
    ///   <paramref name="xUnitTestOutputHelper"/> is <c>null</c> (thrown by the <see cref="TestBase"/> constructor).
    /// </exception>
    protected UnitTestBase (ITestOutputHelper xUnitTestOutputHelper)
        : base(xUnitTestOutputHelper)
    {
    }
    //----------------------------------------------------------------------------

    #endregion Constructors/Finalizers
}
//################################################################################
