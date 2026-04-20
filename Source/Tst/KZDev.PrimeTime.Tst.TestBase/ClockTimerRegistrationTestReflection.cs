// Copyright (c) Kevin Zehrer
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace KZDev.PrimeTime.Tests;

//################################################################################
/// <summary>
///   Reflection helpers for internal <c>Timer</c> entry points on clock timer registrations, used when tests must
///   simulate ticks that are hard to reach through public APIs alone.
/// </summary>
[ExcludeFromCodeCoverage]
public static class ClockTimerRegistrationTestReflection
{
    //----------------------------------------------------------------------------
    /// <summary>
    ///   Resolves the non-public instance method <c>OnTimerTick</c> on a timer registration type.
    /// </summary>
    /// <param name="registrationType">The concrete registration type from the product assembly under test.</param>
    /// <returns>
    ///   The <see cref="MethodInfo"/> for <c>OnTimerTick</c>.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    ///   No matching <c>OnTimerTick</c> method was found on <paramref name="registrationType"/>.
    /// </exception>
    private static MethodInfo GetOnTimerTickMethod (Type registrationType)
    {
        MethodInfo? method = registrationType.GetMethod("OnTimerTick",
            BindingFlags.Instance | BindingFlags.NonPublic);
        if (method is null)
        {
            throw new InvalidOperationException(
                $"{registrationType.FullName}.OnTimerTick was not found.");
        }

        return method;
    }
    //----------------------------------------------------------------------------

    //----------------------------------------------------------------------------
    /// <summary>
    ///   Resolves the non-public instance method <c>OnTimerTick</c> on a day-time timer registration type.
    /// </summary>
    /// <param name="clockDayTimeTimerRegistrationType">
    ///   The concrete <c>ClockDayTimeTimerRegistration</c> type from the product assembly under test
    ///   (e.g. <c>typeof(KZDev.PrimeTime.ClockDayTimeTimerRegistration)</c> or the SystemClock assembly equivalent).
    /// </param>
    /// <returns>The <see cref="MethodInfo"/> for <c>OnTimerTick</c>.</returns>
    /// <exception cref="ArgumentNullException">
    ///   <paramref name="clockDayTimeTimerRegistrationType"/> is <c>null</c>.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    ///   No matching <c>OnTimerTick</c> method was found on <paramref name="clockDayTimeTimerRegistrationType"/>.
    /// </exception>
    public static MethodInfo GetDayTimeOnTimerTickMethod (Type clockDayTimeTimerRegistrationType)
    {
        if (clockDayTimeTimerRegistrationType is null)
        {
            throw new ArgumentNullException(nameof(clockDayTimeTimerRegistrationType));
        }

        return GetOnTimerTickMethod(clockDayTimeTimerRegistrationType);
    }
    //----------------------------------------------------------------------------

    //----------------------------------------------------------------------------
    /// <summary>
    ///   Resolves the non-public instance method <c>OnTimerTick</c> on an interval timer registration type.
    /// </summary>
    /// <param name="clockIntervalTimerRegistrationType">
    ///   The concrete <c>ClockIntervalTimerRegistration</c> type from the product assembly under test
    ///   (e.g. <c>typeof(KZDev.PrimeTime.ClockIntervalTimerRegistration)</c> or the SystemClock assembly equivalent).
    /// </param>
    /// <returns>The <see cref="MethodInfo"/> for <c>OnTimerTick</c>.</returns>
    /// <exception cref="ArgumentNullException">
    ///   <paramref name="clockIntervalTimerRegistrationType"/> is <c>null</c>.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    ///   No matching <c>OnTimerTick</c> method was found on <paramref name="clockIntervalTimerRegistrationType"/>.
    /// </exception>
    public static MethodInfo GetIntervalOnTimerTickMethod (Type clockIntervalTimerRegistrationType)
    {
        if (clockIntervalTimerRegistrationType is null)
        {
            throw new ArgumentNullException(nameof(clockIntervalTimerRegistrationType));
        }

        return GetOnTimerTickMethod(clockIntervalTimerRegistrationType);
    }
    //----------------------------------------------------------------------------
}
//################################################################################
