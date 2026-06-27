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
    ///   Verifies that <see cref="UnitTestBase.GetFirstCallbackWaitTimeout"/> adds the initial delay
    ///   plus two wait margins.
    /// </summary>
    [Fact]
    public void GetFirstCallbackWaitTimeout_AddsShortDelayAndTwoWaitMargins ()
    {
        TimeSpan shortDelay = TimeSpan.FromMilliseconds(80);
        TimeSpan waitMargin = TimeSpan.FromMilliseconds(400);

        TimeSpan result = GetFirstCallbackWaitTimeout(shortDelay, waitMargin);

        result.Should().Be(shortDelay + waitMargin + waitMargin);
        result.Should().BeGreaterThan(shortDelay + waitMargin);
    }
    //----------------------------------------------------------------------------
}
