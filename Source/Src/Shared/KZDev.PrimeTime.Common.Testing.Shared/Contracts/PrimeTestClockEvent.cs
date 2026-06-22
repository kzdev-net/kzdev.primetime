// Copyright (c) Kevin Zehrer
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
using System.Diagnostics;

#if SYSTEMCLOCK
namespace KZDev.SystemClock.PrimeTime.Testing;
#else
namespace KZDev.PrimeTime.Testing;
#endif

//################################################################################
/// <summary>
///   Base type for observability events raised on <see cref="IPrimeTestClock.ClockEvents"/>.
/// </summary>
/// <remarks>
///   Timed payloads inherit <see cref="PrimeTestClockTimedEvent"/>. Lifecycle kinds are sealed derivatives that
///   differ only by <see cref="EventType"/>.
/// </remarks>
public abstract class PrimeTestClockEvent
{
    #region Constructors/Finalizers

    //----------------------------------------------------------------------------
    /// <summary>
    ///   Initializes a new instance of the <see cref="PrimeTestClockEvent"/> class.
    /// </summary>
    /// <param name="eventType">
    ///   The kind of clock event.
    /// </param>
    protected internal PrimeTestClockEvent (PrimeTestClockEventType eventType)
    {
        EventType = eventType;
    }
    //----------------------------------------------------------------------------

    #endregion Constructors/Finalizers

    //----------------------------------------------------------------------------
    /// <summary>
    ///   Gets the kind of clock event represented by this instance.
    /// </summary>
    public PrimeTestClockEventType EventType { [DebuggerStepThrough] get; }
    //----------------------------------------------------------------------------
}
//################################################################################
