// Copyright (c) Kevin Zehrer
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

// ReSharper disable InconsistentNaming
namespace KZDev.PrimeTime.Tests;

//################################################################################
/// <summary>
///   Expected Testing throw messages for contract tests. Values mirror
///   <c>TestingStrings.resx</c> in <c>KZDev.PrimeTime.Testing</c>.
/// </summary>
internal static class ThrowHelperContractMessages
{
    internal const string Overflow_ScaledVirtualElapsedExceedsTimeSpanRange =
        "Scaled virtual elapsed time exceeds the representable TimeSpan range.";

    internal const string InvalidOperation_VirtualTimeBackwardWhileRunning =
        "Cannot move the test clock's virtual time backward while it is running.";

    internal const string InvalidOperation_VirtualTimeBackwardWhileIntervalTimerActive =
        "Cannot move the test clock's virtual time backward while an interval timer registration is active.";

    internal const string ArgumentOutOfRange_StartRunRateOutOfRange =
        "Virtual time per real second must be between 100 milliseconds and 1 hour, inclusive.";

    internal const string InvalidOperation_TimerNonRepeatingToRepeating =
        "Cannot change a non-repeating timer to a repeating timer.";

    // Resx key: InvalidOperation_UnsupportedTimerCallbackKind (TestingStrings.resx)
    // Static prefix only (through "Unsupported callback kind: ", before the "{0}" templated segment).
    internal const string InvalidOperation_UnsupportedTimerCallbackKind_Prefix =
        "Unsupported callback kind: ";

    // Resx key: InvalidOperation_RunnerStopJoinFailed (TestingStrings.resx)
    // Static prefix only (through "Stop() returned false", before the "{0}" templated segment).
    // Tests assert $"{InvalidOperation_RunnerStopJoinFailed_Prefix}*" for the formatted remainder.
    internal const string InvalidOperation_RunnerStopJoinFailed_Prefix =
        "A previous Stop() returned false";

    internal const string InvalidOperation_VirtualTimeMutationDuringStopJoin =
        "Virtual time cannot be changed while Stop() is joining the automatic runner. "
        + "Wait for Stop() to complete before calling SetTime, Advance, or RunFor.";

    // Resx key: InvalidOperation_StartWaitForOverlappingStopTimedOut (TestingStrings.resx)
    // Static prefix only (through "within ", before the "{0}" templated segment).
    internal const string InvalidOperation_StartWaitForOverlappingStopTimedOut_Prefix =
        "Start waited for an overlapping Stop to finish joining the automatic runner, "
        + "but Stop did not complete within ";

    // Resx key: InvalidOperation_RunnerStopJoinTimedOut (TestingStrings.resx)
    // Static prefix only (through "after ", before the "{0}" templated segment).
    internal const string InvalidOperation_RunnerStopJoinTimedOut_Prefix =
        "Timed out stopping the automatic runner after ";

    // Exception.Data keys and values mirror ThrowHelper for InvalidOperation_RunnerStopJoinTimedOut.
    internal const string InvalidOperation_RunnerStopJoinTimedOut_DataKey_BlockedOperations = "BlockedOperations";

    internal const string InvalidOperation_RunnerStopJoinTimedOut_DataKey_LikelyCause = "LikelyCause";

    internal const string InvalidOperation_RunnerStopJoinTimedOut_BlockedOperations =
        "Start(), SetTime, Advance, and RunFor";

    internal const string InvalidOperation_RunnerStopJoinTimedOut_LikelyCause =
        "The runner thread may still be executing and is typically blocked inside a ClockEvents subscriber "
        + "or virtual-time dispatch callback.";
}
//################################################################################
