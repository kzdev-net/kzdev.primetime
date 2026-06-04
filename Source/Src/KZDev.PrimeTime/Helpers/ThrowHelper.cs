// Copyright (c) Kevin Zehrer
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.CompilerServices;
using KZDev.PrimeTime.Resources;
#if SYSTEMCLOCK
using KZDev.SystemClock.PrimeTime;
#else
using KZDev.PrimeTime;
#endif

#if SYSTEMCLOCK
namespace KZDev.SystemClock.PrimeTime.Helpers;
#else
namespace KZDev.PrimeTime.Helpers;
#endif

//################################################################################
internal static partial class ThrowHelper
{
    #region InvalidOperation Errors

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
    internal static void ThrowInvalidOperation_UnsupportedTimerCallbackKind(TimerCallbackKind kind) =>
        throw new InvalidOperationException(
            string.Format(
                CultureInfo.CurrentCulture,
                ProductionStrings.InvalidOperation_UnsupportedTimerCallbackKind,
                kind));
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
    internal static void ThrowInvalidOperation_TimerNonRepeatingToRepeating() =>
        throw new InvalidOperationException(ProductionStrings.InvalidOperation_TimerNonRepeatingToRepeating);
    //--------------------------------------------------------------------------------
    /// <summary>
    ///   Throws an <see cref="InvalidOperationException"/> when an invalid local time
    ///   window was expected on a calendar date.
    /// </summary>
    /// <param name="resolutionContext">
    ///   Context describing the resolution attempt.
    /// </param>
    /// <exception cref="InvalidOperationException">
    /// </exception>
#if !NETSTANDARD2_0
    [DoesNotReturn]
#endif
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void ThrowInvalidOperation_ExpectedInvalidLocalTimeWindow(string resolutionContext) =>
        throw new InvalidOperationException(
            string.Format(
                CultureInfo.CurrentCulture,
                ProductionStrings.InvalidOperation_ExpectedInvalidLocalTimeWindow,
                resolutionContext));
    //--------------------------------------------------------------------------------
    /// <summary>
    ///   Throws an <see cref="InvalidOperationException"/> when a RunAfter instant
    ///   cannot be resolved after a spring-forward gap.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// </exception>
#if !NETSTANDARD2_0
    [DoesNotReturn]
#endif
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void ThrowInvalidOperation_RunAfterSpringForwardGapUnresolved() =>
        throw new InvalidOperationException(ProductionStrings.InvalidOperation_RunAfterSpringForwardGapUnresolved);
    //--------------------------------------------------------------------------------
    /// <summary>
    ///   Throws an <see cref="InvalidOperationException"/> when a RunBefore instant
    ///   cannot be resolved before a spring-forward gap.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// </exception>
#if !NETSTANDARD2_0
    [DoesNotReturn]
#endif
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void ThrowInvalidOperation_RunBeforeSpringForwardGapUnresolved() =>
        throw new InvalidOperationException(ProductionStrings.InvalidOperation_RunBeforeSpringForwardGapUnresolved);
    //--------------------------------------------------------------------------------
    /// <summary>
    ///   Throws an <see cref="InvalidOperationException"/> when no valid local day-time
    ///   fire instant is found within the search window.
    /// </summary>
    /// <param name="maxDays">
    ///   Maximum calendar days searched.
    /// </param>
    /// <param name="zoneId">
    ///   Time zone identifier used in the message.
    /// </param>
    /// <param name="targetTimeOfDay">
    ///   Target local time of day used in the message.
    /// </param>
    /// <exception cref="InvalidOperationException">
    /// </exception>
#if !NETSTANDARD2_0
    [DoesNotReturn]
#endif
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void ThrowInvalidOperation_LocalDayTimeFireInstantNotFound(
        int maxDays,
        string zoneId,
        string targetTimeOfDay) =>
        throw new InvalidOperationException(
            string.Format(
                CultureInfo.CurrentCulture,
                ProductionStrings.InvalidOperation_LocalDayTimeFireInstantNotFound,
                maxDays,
                zoneId,
                targetTimeOfDay));
#if !SYSTEMCLOCK
    //--------------------------------------------------------------------------------
    /// <summary>
    ///   Throws an <see cref="InvalidOperationException"/> for an unexpected
    ///   <c>ZoneLocalMapping.Count</c>.
    /// </summary>
    /// <param name="mappingCount">
    ///   The unexpected mapping count.
    /// </param>
    /// <exception cref="InvalidOperationException">
    /// </exception>
#if !NETSTANDARD2_0
    [DoesNotReturn]
#endif
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void ThrowInvalidOperation_UnexpectedZoneLocalMappingCount(int mappingCount) =>
        throw new InvalidOperationException(
            string.Format(
                CultureInfo.CurrentCulture,
                ProductionStrings.InvalidOperation_UnexpectedZoneLocalMappingCount,
                mappingCount));
#endif

    #endregion

    #region Argument Errors

    //--------------------------------------------------------------------------------
    /// <summary>
    ///   Throws an <see cref="ArgumentException"/> when schedule-zone projection
    ///   requires <see cref="IPrimeClock"/>.
    /// </summary>
    /// <param name="paramName">
    ///   The name of the invalid argument.
    /// </param>
    /// <exception cref="ArgumentException">
    /// </exception>
#if !NETSTANDARD2_0
    [DoesNotReturn]
#endif
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void ThrowArgumentException_ScheduleZoneRequiresIPrimeClock(string paramName) =>
        throw new ArgumentException(ProductionStrings.Argument_ScheduleZoneRequiresIPrimeClock, paramName);
    //--------------------------------------------------------------------------------
    /// <summary>
    ///   Throws an <see cref="ArgumentException"/> when a <see cref="DateTime"/> value
    ///   does not use <see cref="DateTimeKind.Utc"/>.
    /// </summary>
    /// <param name="paramName">
    ///   The name of the invalid argument.
    /// </param>
    /// <exception cref="ArgumentException">
    /// </exception>
#if !NETSTANDARD2_0
    [DoesNotReturn]
#endif
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void ThrowArgumentException_UtcDateTimeMustBeUtcKind(string paramName) =>
        throw new ArgumentException(ProductionStrings.Argument_UtcDateTimeMustBeUtcKind, paramName);

    #endregion
}
//################################################################################
