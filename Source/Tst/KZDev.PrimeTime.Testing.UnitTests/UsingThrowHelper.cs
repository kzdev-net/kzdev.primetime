// Copyright (c) Kevin Zehrer
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using System.Globalization;

using AwesomeAssertions;

using KZDev.PrimeTime;
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
    ///   Verifies that changing a non-repeating timer to a repeating timer throws
    ///   <see cref="InvalidOperationException"/> with the resx message.
    /// </summary>
    [Fact]
    public void ThrowInvalidOperation_TimerNonRepeatingToRepeating_ThrowsInvalidOperationExceptionWithResxMessage ()
    {
        Action act = ThrowHelper.ThrowInvalidOperation_TimerNonRepeatingToRepeating;
        act.Should().Throw<InvalidOperationException>()
            .WithMessage(ThrowHelperContractMessages.InvalidOperation_TimerNonRepeatingToRepeating);
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies that an unsupported <see cref="TimerCallbackKind"/> throws
    ///   <see cref="InvalidOperationException"/> with the resx message template.
    /// </summary>
    [Fact]
    public void ThrowInvalidOperation_UnsupportedTimerCallbackKind_ThrowsInvalidOperationExceptionWithResxMessage ()
    {
        int maxDefinedTimerCallbackKindValue = Enum.GetValues(typeof(TimerCallbackKind)).Cast<int>().Max();
        TimerCallbackKind unsupportedKind = (TimerCallbackKind)(maxDefinedTimerCallbackKindValue + 1);
        Action act = () => ThrowHelper.ThrowInvalidOperation_UnsupportedTimerCallbackKind(unsupportedKind);
        act.Should().Throw<InvalidOperationException>()
            .WithMessage($"{ThrowHelperContractMessages.InvalidOperation_UnsupportedTimerCallbackKind_Prefix}{unsupportedKind}");
    }
    //----------------------------------------------------------------------------

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

    /// <summary>
    ///   Verifies that a previous <c>Stop()</c> join failure throws <see cref="InvalidOperationException"/>
    ///   with the resx message template.
    /// </summary>
    [Fact]
    public void ThrowInvalidOperation_RunnerStopJoinFailed_ThrowsInvalidOperationExceptionWithResxMessage ()
    {
        TimeSpan stopJoinTimeout = TimeSpan.FromSeconds(5);
        string expectedStopJoinTimeoutSeconds =
            stopJoinTimeout.TotalSeconds.ToString("g0", CultureInfo.InvariantCulture);
        Action act = () => ThrowHelper.ThrowInvalidOperation_RunnerStopJoinFailed(stopJoinTimeout);
        act.Should().Throw<InvalidOperationException>()
            .WithMessage(
                $"{ThrowHelperContractMessages.InvalidOperation_RunnerStopJoinFailed_Prefix} "
                + $"because the automatic runner did not join within {expectedStopJoinTimeoutSeconds} seconds*");
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies that virtual time mutation during <c>Stop()</c> join throws
    ///   <see cref="InvalidOperationException"/> with the resx message.
    /// </summary>
    [Fact]
    public void ThrowInvalidOperation_VirtualTimeMutationDuringStopJoin_ThrowsInvalidOperationExceptionWithResxMessage ()
    {
        Action act = ThrowHelper.ThrowInvalidOperation_VirtualTimeMutationDuringStopJoin;
        act.Should().Throw<InvalidOperationException>()
            .WithMessage(ThrowHelperContractMessages.InvalidOperation_VirtualTimeMutationDuringStopJoin);
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies that <c>Start</c> waiting for an overlapping <c>Stop</c> join timeout throws
    ///   <see cref="InvalidOperationException"/> with the resx message template.
    /// </summary>
    [Fact]
    public void ThrowInvalidOperation_StartWaitForOverlappingStopTimedOut_ThrowsInvalidOperationExceptionWithResxMessage ()
    {
        TimeSpan maximumWaitForOverlappingRunnerStop = TimeSpan.FromSeconds(10);
        TimeSpan stopJoinTimeout = TimeSpan.FromSeconds(5);
        string expectedMaximumWaitSeconds =
            maximumWaitForOverlappingRunnerStop.TotalSeconds.ToString("g0", CultureInfo.InvariantCulture);
        string expectedStopJoinTimeoutSeconds =
            stopJoinTimeout.TotalSeconds.ToString("g0", CultureInfo.InvariantCulture);
        Action act = () => ThrowHelper.ThrowInvalidOperation_StartWaitForOverlappingStopTimedOut(
            maximumWaitForOverlappingRunnerStop,
            stopJoinTimeout);
        act.Should().Throw<InvalidOperationException>()
            .WithMessage(
                $"{ThrowHelperContractMessages.InvalidOperation_StartWaitForOverlappingStopTimedOut_Prefix}"
                + $"{expectedMaximumWaitSeconds} seconds ({expectedStopJoinTimeoutSeconds} second join timeout*");
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies that automatic runner stop join timeout throws <see cref="InvalidOperationException"/>
    ///   with the resx message template and diagnostic <see cref="Exception.Data"/> entries.
    /// </summary>
    [Fact]
    public void ThrowInvalidOperation_RunnerStopJoinTimedOut_ThrowsInvalidOperationExceptionWithResxMessageAndData ()
    {
        TimeSpan stopJoinTimeout = TimeSpan.FromSeconds(30);
        string expectedStopJoinTimeoutSeconds =
            stopJoinTimeout.TotalSeconds.ToString("g0", CultureInfo.InvariantCulture);
        Action act = () => ThrowHelper.ThrowInvalidOperation_RunnerStopJoinTimedOut(stopJoinTimeout);
        InvalidOperationException exception = act.Should().Throw<InvalidOperationException>()
            .WithMessage(
                $"{ThrowHelperContractMessages.InvalidOperation_RunnerStopJoinTimedOut_Prefix}"
                + $"{expectedStopJoinTimeoutSeconds} seconds.")
            .Which;
        exception.Data[ThrowHelperContractMessages.InvalidOperation_RunnerStopJoinTimedOut_DataKey_BlockedOperations]
            .Should().Be(ThrowHelperContractMessages.InvalidOperation_RunnerStopJoinTimedOut_BlockedOperations);
        exception.Data[ThrowHelperContractMessages.InvalidOperation_RunnerStopJoinTimedOut_DataKey_LikelyCause]
            .Should().Be(ThrowHelperContractMessages.InvalidOperation_RunnerStopJoinTimedOut_LikelyCause);
    }
    //----------------------------------------------------------------------------
}
//################################################################################
