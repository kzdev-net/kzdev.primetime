// Copyright (c) Kevin Zehrer
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;

#if SYSTEMCLOCK
namespace KZDev.SystemClock.PrimeTime.Helpers;
#else
namespace KZDev.PrimeTime.Helpers;
#endif

//################################################################################
/// <summary>
///   Centralizes throwing common invalid-operation and argument exceptions using
///   messages from <see cref="KZDev.PrimeTime.Resources.ProductionStrings"/>.
/// </summary>
[ExcludeFromCodeCoverage]
internal static partial class ThrowHelper
{
}
