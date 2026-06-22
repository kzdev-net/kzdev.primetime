// Copyright (c) Kevin Zehrer
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

#if SYSTEMCLOCK
namespace KZDev.SystemClock.PrimeTime.Testing;
#else
namespace KZDev.PrimeTime.Testing;
#endif

//################################################################################
/// <summary>
///   <see cref="PrimeTestClockEvent"/> that carries virtual clock time and an optional runner rate.
/// </summary>
/// <remarks>
///   Sealed derivatives (<see cref="PrimeTestClockNewTimeEvent"/>,
///   <see cref="PrimeTestClockStartedEvent"/>, <see cref="PrimeTestClockStoppedEvent"/>) differ only by
///   <see cref="PrimeTestClockEvent.EventType"/>. Use <see cref="ClockTime"/> and <see cref="RunRateTimeSpan"/>
///   for virtual time and runner rate at the moment the event was raised.
/// </remarks>
public abstract partial class PrimeTestClockTimedEvent : PrimeTestClockEvent
{
    #region Constructors/Finalizers

    //----------------------------------------------------------------------------
    /// <summary>
    ///   Initializes a new instance of the <see cref="PrimeTestClockTimedEvent"/> class.
    /// </summary>
    /// <param name="eventType">
    ///   The kind of clock event.
    /// </param>
    protected internal PrimeTestClockTimedEvent (PrimeTestClockEventType eventType)
        : base(eventType)
    {
    }
    //----------------------------------------------------------------------------

    #endregion Constructors/Finalizers
}
//################################################################################
