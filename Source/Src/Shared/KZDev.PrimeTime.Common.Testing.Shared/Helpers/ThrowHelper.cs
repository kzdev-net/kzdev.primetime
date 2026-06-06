// Copyright (c) Kevin Zehrer
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;

#if SYSTEMCLOCK
namespace KZDev.SystemClock.PrimeTime.Testing.Helpers;
#else
namespace KZDev.PrimeTime.Testing.Helpers;
#endif

//################################################################################
/// <summary>
///   Centralizes throwing common invalid-operation, overflow, and argument exceptions using
///   messages from <see cref="KZDev.PrimeTime.Testing.Resources.TestingStrings"/>.
/// </summary>
[ExcludeFromCodeCoverage]
internal static partial class ThrowHelper
{
    #region InvalidOperation Errors

    #endregion

    #region Overflow Errors

    #endregion

    #region Argument Errors

    #endregion

    #region ArgumentOutOfRange Errors

    #endregion
}
