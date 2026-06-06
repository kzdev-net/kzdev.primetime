// Copyright (c) Kevin Zehrer
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.CompilerServices;

using KZDev.PrimeTime.Testing.Resources;
#if SYSTEMCLOCK
using KZDev.SystemClock.PrimeTime;
#else
using KZDev.PrimeTime;
#endif

#if SYSTEMCLOCK
namespace KZDev.SystemClock.PrimeTime.Testing.Helpers;
#else
namespace KZDev.PrimeTime.Testing.Helpers;
#endif

//################################################################################
internal static partial class ThrowHelper
{
    private const string InvalidOperation_RunnerStopJoinTimedOut_BlockedOperations =
        "Start(), SetTime, Advance, and RunFor";

    private const string InvalidOperation_RunnerStopJoinTimedOut_LikelyCause =
        "The runner thread may still be executing and is typically blocked inside a ClockEvents subscriber "
        + "or virtual-time dispatch callback.";

    #region InvalidOperation Errors

    //--------------------------------------------------------------------------------
    /// <summary>
    ///   Throws an <see cref="InvalidOperationException"/> when a non-repeating timer
    ///   is changed to a repeating timer.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// </exception>
#if !NETSTANDARD2_0
    [DoesNotReturn]
#endif
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void ThrowInvalidOperation_TimerNonRepeatingToRepeating () =>
        throw new InvalidOperationException(TestingStrings.InvalidOperation_TimerNonRepeatingToRepeating);
    //--------------------------------------------------------------------------------
    /// <summary>
    ///   Throws an <see cref="InvalidOperationException"/> for an unsupported
    ///   <see cref="TimerCallbackKind"/>.
    /// </summary>
    /// <param name="kind">
    ///   The unsupported callback kind.
    /// </param>
    /// <exception cref="InvalidOperationException">
    /// </exception>
#if !NETSTANDARD2_0
    [DoesNotReturn]
#endif
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void ThrowInvalidOperation_UnsupportedTimerCallbackKind (TimerCallbackKind kind) =>
        throw new InvalidOperationException(
            string.Format(
                CultureInfo.CurrentCulture,
                TestingStrings.InvalidOperation_UnsupportedTimerCallbackKind,
                kind));
    //--------------------------------------------------------------------------------
    /// <summary>
    ///   Throws an <see cref="InvalidOperationException"/> when virtual time is moved
    ///   backward while the test clock is running.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// </exception>
#if !NETSTANDARD2_0
    [DoesNotReturn]
#endif
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void ThrowInvalidOperation_VirtualTimeBackwardWhileRunning () =>
        throw new InvalidOperationException(TestingStrings.InvalidOperation_VirtualTimeBackwardWhileRunning);
    //--------------------------------------------------------------------------------
    /// <summary>
    ///   Throws an <see cref="InvalidOperationException"/> when virtual time is moved
    ///   backward while an interval timer registration is active.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// </exception>
#if !NETSTANDARD2_0
    [DoesNotReturn]
#endif
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void ThrowInvalidOperation_VirtualTimeBackwardWhileIntervalTimerActive () =>
        throw new InvalidOperationException(TestingStrings.InvalidOperation_VirtualTimeBackwardWhileIntervalTimerActive);
    //--------------------------------------------------------------------------------
    /// <summary>
    ///   Throws an <see cref="InvalidOperationException"/> when a previous
    ///   <c>Stop()</c> did not join the automatic runner within the configured timeout.
    /// </summary>
    /// <param name="stopJoinTimeoutSeconds">
    ///   The stop join timeout, in seconds.
    /// </param>
    /// <exception cref="InvalidOperationException">
    /// </exception>
#if !NETSTANDARD2_0
    [DoesNotReturn]
#endif
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void ThrowInvalidOperation_RunnerStopJoinFailed (int stopJoinTimeoutSeconds) =>
        throw new InvalidOperationException(
            string.Format(
                CultureInfo.InvariantCulture,
                TestingStrings.InvalidOperation_RunnerStopJoinFailed,
                stopJoinTimeoutSeconds.ToString("g0", CultureInfo.InvariantCulture)));
    //--------------------------------------------------------------------------------
    /// <summary>
    ///   Throws an <see cref="InvalidOperationException"/> when virtual time is mutated
    ///   while <c>Stop()</c> is joining the automatic runner.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// </exception>
#if !NETSTANDARD2_0
    [DoesNotReturn]
#endif
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void ThrowInvalidOperation_VirtualTimeMutationDuringStopJoin () =>
        throw new InvalidOperationException(TestingStrings.InvalidOperation_VirtualTimeMutationDuringStopJoin);
    //--------------------------------------------------------------------------------
    /// <summary>
    ///   Throws an <see cref="InvalidOperationException"/> when <c>Start</c> timed out
    ///   waiting for an overlapping <c>Stop</c> to finish joining the automatic runner.
    /// </summary>
    /// <param name="maxWaitSeconds">
    ///   The maximum wait duration, in seconds.
    /// </param>
    /// <param name="stopJoinTimeoutSeconds">
    ///   The stop join timeout, in seconds.
    /// </param>
    /// <exception cref="InvalidOperationException">
    /// </exception>
#if !NETSTANDARD2_0
    [DoesNotReturn]
#endif
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void ThrowInvalidOperation_StartWaitForOverlappingStopTimedOut (
        int maxWaitSeconds,
        int stopJoinTimeoutSeconds) =>
        throw new InvalidOperationException(
            string.Format(
                CultureInfo.InvariantCulture,
                TestingStrings.InvalidOperation_StartWaitForOverlappingStopTimedOut,
                maxWaitSeconds.ToString("g0", CultureInfo.InvariantCulture),
                stopJoinTimeoutSeconds.ToString("g0", CultureInfo.InvariantCulture)));
    //--------------------------------------------------------------------------------
    /// <summary>
    ///   Throws an <see cref="InvalidOperationException"/> when stopping the automatic
    ///   runner timed out.
    /// </summary>
    /// <param name="stopJoinTimeoutSeconds">
    ///   The stop join timeout, in seconds.
    /// </param>
    /// <exception cref="InvalidOperationException">
    /// </exception>
#if !NETSTANDARD2_0
    [DoesNotReturn]
#endif
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void ThrowInvalidOperation_RunnerStopJoinTimedOut (int stopJoinTimeoutSeconds)
    {
        InvalidOperationException exception = new(
            string.Format(
                CultureInfo.InvariantCulture,
                TestingStrings.InvalidOperation_RunnerStopJoinTimedOut,
                stopJoinTimeoutSeconds.ToString("g0", CultureInfo.InvariantCulture)))
        {
            Data =
            {
                ["BlockedOperations"] = InvalidOperation_RunnerStopJoinTimedOut_BlockedOperations,
                ["LikelyCause"] = InvalidOperation_RunnerStopJoinTimedOut_LikelyCause
            }
        };
        throw exception;
    }

    #endregion

    #region Overflow Errors

    //--------------------------------------------------------------------------------
    /// <summary>
    ///   Throws an <see cref="OverflowException"/> when scaled virtual elapsed time
    ///   exceeds the representable <see cref="TimeSpan"/> range.
    /// </summary>
    /// <exception cref="OverflowException">
    /// </exception>
#if !NETSTANDARD2_0
    [DoesNotReturn]
#endif
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void ThrowOverflowException_ScaledVirtualElapsedExceedsTimeSpanRange () =>
        throw new OverflowException(TestingStrings.Overflow_ScaledVirtualElapsedExceedsTimeSpanRange);

    #endregion

    #region Argument Errors

    //--------------------------------------------------------------------------------
    /// <summary>
    ///   Throws an <see cref="ArgumentNullException"/> when the service collection is null.
    /// </summary>
    /// <exception cref="ArgumentNullException">
    /// </exception>
#if !NETSTANDARD2_0
    [DoesNotReturn]
#endif
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void ThrowArgumentNullException_Services () =>
        throw new ArgumentNullException("services", TestingStrings.Argument_ServicesNull);

    #endregion

    #region ArgumentOutOfRange Errors

    //--------------------------------------------------------------------------------
    /// <summary>
    ///   Throws an <see cref="ArgumentOutOfRangeException"/> when the virtual time per
    ///   real second is outside the allowed range.
    /// </summary>
    /// <param name="perSecondRate">
    ///   The invalid virtual time per real second.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// </exception>
#if !NETSTANDARD2_0
    [DoesNotReturn]
#endif
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void ThrowArgumentOutOfRangeException_StartRunRateOutOfRange (TimeSpan perSecondRate) =>
        throw new ArgumentOutOfRangeException(
            nameof(perSecondRate),
            perSecondRate,
            TestingStrings.ArgumentOutOfRange_StartRunRateOutOfRange);

    #endregion
}
//################################################################################
