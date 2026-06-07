// Copyright (c) Kevin Zehrer
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;

using AwesomeAssertions;

using KZDev.PrimeTime.Testing.Helpers;
using KZDev.PrimeTime.Tests;

namespace KZDev.PrimeTime.Testing.UnitTests;

//################################################################################
/// <summary>
///   Unit tests for Testing <see cref="ThrowHelper"/> methods used by shared
///   <see cref="PrimeTestClock"/> throw paths.
/// </summary>
[ExcludeFromCodeCoverage]
public class UsingThrowHelper : UnitTestBase
{
    #region Constructors/Finalizers

    //----------------------------------------------------------------------------
    /// <summary>
    ///   Initializes a new instance of the <see cref="UsingThrowHelper"/> class.
    /// </summary>
    /// <param name="xUnitTestOutputHelper">
    ///   The xUnit test output helper that can be used to output test messages.
    /// </param>
    public UsingThrowHelper (ITestOutputHelper xUnitTestOutputHelper)
        : base(xUnitTestOutputHelper)
    {
    }
    //----------------------------------------------------------------------------

    #endregion Constructors/Finalizers

    /// <summary>
    ///   Verifies that scaled virtual elapsed overflow throws <see cref="OverflowException"/> with the resx message.
    /// </summary>
    [Fact]
    public void ThrowOverflowException_ScaledVirtualElapsedExceedsTimeSpanRange_ThrowsOverflowExceptionWithResxMessage ()
    {
        Action act = ThrowHelper.ThrowOverflowException_ScaledVirtualElapsedExceedsTimeSpanRange;
        act.Should().Throw<OverflowException>()
            .WithMessage(ThrowHelperContractMessages.Overflow_ScaledVirtualElapsedExceedsTimeSpanRange);
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies that backward virtual time while running throws <see cref="InvalidOperationException"/>
    ///   with the resx message.
    /// </summary>
    [Fact]
    public void ThrowInvalidOperation_VirtualTimeBackwardWhileRunning_ThrowsInvalidOperationExceptionWithResxMessage ()
    {
        Action act = ThrowHelper.ThrowInvalidOperation_VirtualTimeBackwardWhileRunning;
        act.Should().Throw<InvalidOperationException>()
            .WithMessage(ThrowHelperContractMessages.InvalidOperation_VirtualTimeBackwardWhileRunning);
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies that backward virtual time with an active interval timer throws
    ///   <see cref="InvalidOperationException"/> with the resx message.
    /// </summary>
    [Fact]
    public void ThrowInvalidOperation_VirtualTimeBackwardWhileIntervalTimerActive_ThrowsInvalidOperationExceptionWithResxMessage ()
    {
        Action act = ThrowHelper.ThrowInvalidOperation_VirtualTimeBackwardWhileIntervalTimerActive;
        act.Should().Throw<InvalidOperationException>()
            .WithMessage(ThrowHelperContractMessages.InvalidOperation_VirtualTimeBackwardWhileIntervalTimerActive);
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies that an out-of-range start run rate throws <see cref="ArgumentOutOfRangeException"/>
    ///   with the expected parameter name and resx message.
    /// </summary>
    [Fact]
    public void ThrowArgumentOutOfRangeException_StartRunRateOutOfRange_ThrowsArgumentOutOfRangeExceptionWithParamNameAndResxMessage ()
    {
        TimeSpan perSecondRate = TimeSpan.FromMilliseconds(50);
        Action act = () => ThrowHelper.ThrowArgumentOutOfRangeException_StartRunRateOutOfRange(perSecondRate);
        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("perSecondRate")
            .WithMessage($"{ThrowHelperContractMessages.ArgumentOutOfRange_StartRunRateOutOfRange}*");
    }
    //----------------------------------------------------------------------------
}
//################################################################################
