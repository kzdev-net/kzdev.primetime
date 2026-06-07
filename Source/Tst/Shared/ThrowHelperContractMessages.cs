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
}
//################################################################################
