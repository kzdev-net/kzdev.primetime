// Copyright (c) Kevin Zehrer
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;

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
