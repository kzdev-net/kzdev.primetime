// Copyright (c) Kevin Zehrer
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

#if SYSTEMCLOCK
namespace KZDev.SystemClock.PrimeTime.Testing;
#else
namespace KZDev.PrimeTime.Testing;
#endif

//################################################################################
/// <summary>
///   Represents the method that handles <see cref="IPrimeTestClock.ClockEvents"/>.
/// </summary>
/// <param name="sender">
///   The clock that raised the event.
/// </param>
/// <param name="clockEvent">
///   The discriminated clock event payload.
/// </param>
public delegate void PrimeTestClockEventHandler (object? sender, PrimeTestClockEvent clockEvent);
//################################################################################
