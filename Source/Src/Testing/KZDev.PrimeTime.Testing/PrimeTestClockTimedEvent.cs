// Copyright (c) Kevin Zehrer
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
using System.Diagnostics;
using NodaTime;

namespace KZDev.PrimeTime.Testing;

//################################################################################
public abstract partial class PrimeTestClockTimedEvent
{
    #region Fields

    //----------------------------------------------------------------------------
    /// <summary>
    ///   Virtual instant (UTC) at the time represented by this event.
    /// </summary>
    private readonly Instant _clockInstant;

    /// <summary>
    ///   Virtual time advanced per real second when the event was raised, or <see langword="null"/> when the
    ///   clock was stopped.
    /// </summary>
    private readonly Duration? _runRateDuration;
    //----------------------------------------------------------------------------

    #endregion Fields

    #region Constructors/Finalizers

    //----------------------------------------------------------------------------
    /// <summary>
    ///   Initializes a new instance of the <see cref="PrimeTestClockTimedEvent"/> class.
    /// </summary>
    /// <param name="eventType">
    ///   The kind of clock event.
    /// </param>
    /// <param name="clockInstant">
    ///   The virtual instant (UTC) carried by this event.
    /// </param>
    /// <param name="runRateDuration">
    ///   The virtual time advanced per real second while the clock is running, or
    ///   <see langword="null"/> when the clock is stopped.
    /// </param>
    protected internal PrimeTestClockTimedEvent (
        PrimeTestClockEventType eventType,
        Instant clockInstant,
        Duration? runRateDuration)
        : base(eventType)
    {
        _clockInstant = clockInstant;
        _runRateDuration = runRateDuration;
    }
    //----------------------------------------------------------------------------

    #endregion Constructors/Finalizers

    #region Properties

    //----------------------------------------------------------------------------
    /// <summary>
    ///   Gets the virtual instant (UTC) carried by this event.
    /// </summary>
    public Instant ClockInstant { [DebuggerStepThrough] get => _clockInstant; }
    //----------------------------------------------------------------------------

    //----------------------------------------------------------------------------
    /// <summary>
    ///   Gets the virtual UTC time carried by this event.
    /// </summary>
    public DateTimeOffset ClockTime { [DebuggerStepThrough] get => PrimeTestClockEventNodaConversions.ToClockTime(_clockInstant); }
    //----------------------------------------------------------------------------

    //----------------------------------------------------------------------------
    /// <summary>
    ///   Gets the virtual time advanced per real second while the clock is running, or
    ///   <see langword="null"/> when the clock is stopped.
    /// </summary>
    public Duration? RunRateDuration { [DebuggerStepThrough] get => _runRateDuration; }
    //----------------------------------------------------------------------------

    //----------------------------------------------------------------------------
    /// <summary>
    ///   Gets the virtual time advanced per real second while the clock is running, or
    ///   <see langword="null"/> when the clock is stopped.
    /// </summary>
    public TimeSpan? RunRateTimeSpan { [DebuggerStepThrough] get => PrimeTestClockEventNodaConversions.ToRunRateTimeSpan(_runRateDuration); }
    //----------------------------------------------------------------------------

    #endregion Properties
}
//################################################################################
