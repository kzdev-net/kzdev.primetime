// Copyright (c) Kevin Zehrer
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

#if SYSTEMCLOCK
namespace KZDev.SystemClock.PrimeTime.Testing;
#else
namespace KZDev.PrimeTime.Testing;
#endif

//################################################################################
/// <summary>
///   <see cref="PrimeTestClockTimedEvent"/> raised when virtual time advances to a new instant.
/// </summary>
public sealed partial class PrimeTestClockNewTimeEvent : PrimeTestClockTimedEvent
{
}
//################################################################################
