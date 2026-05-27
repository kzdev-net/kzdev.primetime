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
    ///   Fast virtual-time-per-real-second rate for bounded <see cref="IPrimeTestClock.RunFor"/> tests.
    /// </summary>
    protected static readonly TimeSpan RunForTestFastPerSecondRate = TimeSpan.FromHours(1);

    /// <summary>
    ///   Wall-clock guard while polling for a bounded <see cref="IPrimeTestClock.RunFor"/> run to finish.
    /// </summary>
    protected static readonly TimeSpan RunForTestWaitTimeout = TimeSpan.FromSeconds(5);

    /// <summary>
    ///   Real time between cooperative poll attempts in test-clock wait helpers.
    /// </summary>
    private static readonly TimeSpan TestClockPollInterval = TimeSpan.FromMilliseconds(25);
    //----------------------------------------------------------------------------

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
    protected static int GetSemaphoreWaitTimeoutMilliseconds (TimeSpan timeout)
    {
        return (int)Math.Min(int.MaxValue, timeout.TotalMilliseconds);
    }
    //----------------------------------------------------------------------------

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
    ///   Polls until <paramref name="conditionMet"/> returns <c>true</c> or the timeout elapses, waiting between
    ///   attempts so the loop does not busy-spin.
    /// </summary>
    /// <param name="conditionMet">Returns whether the wait condition is satisfied.</param>
    /// <param name="timeout">Maximum real time to wait.</param>
    /// <param name="cancellationToken">Cancellation token for the test run.</param>
    /// <param name="timeoutMessage">Message for the thrown <see cref="TimeoutException"/>.</param>
    /// <exception cref="TimeoutException">
    ///   Thrown when <paramref name="conditionMet"/> remains <c>false</c> until the timeout elapses.
    /// </exception>
    protected static void WaitUntilCondition (Func<bool> conditionMet, TimeSpan timeout,
        CancellationToken cancellationToken, string timeoutMessage)
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
    ///   Asynchronously polls until <paramref name="conditionMet"/> returns <c>true</c> or the timeout elapses.
    /// </summary>
    /// <param name="conditionMet">Returns whether the wait condition is satisfied.</param>
    /// <param name="timeout">Maximum real time to wait.</param>
    /// <param name="cancellationToken">Cancellation token for the test run.</param>
    /// <param name="timeoutMessage">Message for the thrown <see cref="TimeoutException"/>.</param>
    /// <returns>A task that completes when the condition is met.</returns>
    /// <exception cref="TimeoutException">
    ///   Thrown when <paramref name="conditionMet"/> remains <c>false</c> until the timeout elapses.
    /// </exception>
    protected static async Task WaitUntilConditionAsync (
        Func<bool> conditionMet,
        TimeSpan timeout,
        CancellationToken cancellationToken,
        string timeoutMessage)
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
            cancellationToken,
            "Timed out waiting for the test clock to stop running after "
                + timeout.TotalSeconds.ToString("g0", CultureInfo.InvariantCulture)
                + " seconds.");
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
            cancellationToken,
            "Timed out waiting for the test clock to stop running after "
                + timeout.TotalSeconds.ToString("g0", CultureInfo.InvariantCulture)
                + " seconds.");
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
