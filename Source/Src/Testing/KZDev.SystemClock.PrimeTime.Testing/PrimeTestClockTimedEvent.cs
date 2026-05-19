using System.Diagnostics;

namespace KZDev.SystemClock.PrimeTime.Testing;

//################################################################################
public abstract partial class PrimeTestClockTimedEvent
{
    #region Fields

    //----------------------------------------------------------------------------
    /// <summary>
    ///   Virtual UTC time at the instant represented by this event.
    /// </summary>
    private readonly DateTimeOffset _clockTime;

    /// <summary>
    ///   Virtual time advanced per real second when the event was raised, or <see langword="null"/> when the
    ///   clock was stopped.
    /// </summary>
    private readonly TimeSpan? _runRateTimeSpan;
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
    /// <param name="clockTime">
    ///   The virtual UTC time carried by this event. Non-zero offsets are normalized to UTC (zero offset).
    /// </param>
    /// <param name="runRateTimeSpan">
    ///   The virtual time advanced per real second while the clock is running, or
    ///   <see langword="null"/> when the clock is stopped.
    /// </param>
    protected internal PrimeTestClockTimedEvent (PrimeTestClockEventType eventType,
        DateTimeOffset clockTime, TimeSpan? runRateTimeSpan)
        : base(eventType)
    {
        _clockTime = new DateTimeOffset(clockTime.UtcDateTime, TimeSpan.Zero);
        _runRateTimeSpan = runRateTimeSpan;
    }
    //----------------------------------------------------------------------------

    #endregion Constructors/Finalizers

    #region Properties

    //----------------------------------------------------------------------------
    /// <summary>
    ///   Gets the virtual UTC time carried by this event.
    /// </summary>
    public DateTimeOffset ClockTime { [DebuggerStepThrough] get => _clockTime; }
    //----------------------------------------------------------------------------
    /// <summary>
    ///   Gets the virtual time advanced per real second while the clock is running, or
    ///   <see langword="null"/> when the clock is stopped.
    /// </summary>
    public TimeSpan? RunRateTimeSpan { [DebuggerStepThrough] get => _runRateTimeSpan; }
    //----------------------------------------------------------------------------

    #endregion Properties
}
//################################################################################
