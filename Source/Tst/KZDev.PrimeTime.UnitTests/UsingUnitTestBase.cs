// Copyright (c) Kevin Zehrer
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;

using AwesomeAssertions;

using KZDev.PrimeTime.Tests;

namespace KZDev.PrimeTime.UnitTests;

//################################################################################
/// <summary>
///   Validates timing helpers on <see cref="UnitTestBase"/> used by real-clock interval timer tests.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed class UsingUnitTestBase : UnitTestBase
{
    //----------------------------------------------------------------------------
    /// <summary>
    ///   Initializes a new instance of the <see cref="UsingUnitTestBase"/> class.
    /// </summary>
    /// <param name="xUnitTestOutputHelper">
    ///   The xUnit test output helper for diagnostic output.
    /// </param>
    public UsingUnitTestBase (ITestOutputHelper xUnitTestOutputHelper)
        : base(xUnitTestOutputHelper)
    {
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies that <see cref="UnitTestBase.GetFirstCallbackWaitTimeout"/> delegates to
    ///   <see cref="UnitTestBase.GetCallbackWaitTimeout"/>.
    /// </summary>
    [Fact]
    public void GetFirstCallbackWaitTimeout_DelegatesToGetCallbackWaitTimeout ()
    {
        TimeSpan shortDelay = TimeSpan.FromMilliseconds(80);
        TimeSpan waitMargin = TimeSpan.FromMilliseconds(400);

        GetFirstCallbackWaitTimeout(shortDelay, waitMargin)
            .Should().Be(GetCallbackWaitTimeout(shortDelay, waitMargin));
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies that <see cref="UnitTestBase.GetCallbackWaitTimeout"/> adds two wait margins beyond
    ///   the expected delay.
    /// </summary>
    [Fact]
    public void GetCallbackWaitTimeout_AddsExpectedDelayAndTwoWaitMargins ()
    {
        TimeSpan expectedDelay = TimeSpan.FromMilliseconds(140);
        TimeSpan waitMargin = TimeSpan.FromMilliseconds(400);

        GetCallbackWaitTimeout(expectedDelay, waitMargin)
            .Should().Be(expectedDelay + waitMargin + waitMargin);
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies that <see cref="UnitTestBase.GetStateSettleWaitTimeout"/> doubles the wait margin.
    /// </summary>
    [Fact]
    public void GetStateSettleWaitTimeout_DoublesWaitMargin ()
    {
        TimeSpan waitMargin = TimeSpan.FromMilliseconds(400);

        GetStateSettleWaitTimeout(waitMargin).Should().Be(waitMargin + waitMargin);
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies that <see cref="UnitTestBase.GetCallbackWaitTimeout"/> rejects a negative expected delay.
    /// </summary>
    [Fact]
    public void GetCallbackWaitTimeout_NegativeExpectedDelay_ThrowsArgumentOutOfRangeException ()
    {
        Action act = () => GetCallbackWaitTimeout(TimeSpan.FromMilliseconds(-1), TimeSpan.FromMilliseconds(400));

        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("expectedDelay");
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies that <see cref="UnitTestBase.GetCallbackWaitTimeout"/> rejects a negative wait margin.
    /// </summary>
    [Fact]
    public void GetCallbackWaitTimeout_NegativeWaitMargin_ThrowsArgumentOutOfRangeException ()
    {
        Action act = () => GetCallbackWaitTimeout(TimeSpan.FromMilliseconds(80), TimeSpan.FromMilliseconds(-1));

        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("waitMargin");
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies that <see cref="UnitTestBase.GetStateSettleWaitTimeout"/> rejects a negative wait margin.
    /// </summary>
    [Fact]
    public void GetStateSettleWaitTimeout_NegativeWaitMargin_ThrowsArgumentOutOfRangeException ()
    {
        Action act = () => GetStateSettleWaitTimeout(TimeSpan.FromMilliseconds(-1));

        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("waitMargin");
    }
    //----------------------------------------------------------------------------
}
