#if !NODATIME

using System.Diagnostics;

#if SYSTEMCLOCK
namespace KZDev.SystemClock.PrimeTime.Testing;
#else
namespace KZDev.PrimeTime.Testing;
#endif

//################################################################################
public sealed partial class PrimeTestClockNewTimeEvent
{
    #region Fields

    //----------------------------------------------------------------------------
    private readonly DateTimeOffset _clockTime;
    private readonly TimeSpan? _runRateTimeSpan;
    //----------------------------------------------------------------------------

    #endregion Fields

    #region Constructors/Finalizers

    //----------------------------------------------------------------------------
    /// <summary>
    ///   Initializes a new instance of the <see cref="PrimeTestClockNewTimeEvent"/> class.
    /// </summary>
    /// <param name="clockTime">
    ///   The virtual UTC time at which this event was raised.
    /// </param>
    /// <param name="runRateTimeSpan">
    ///   The virtual time advanced per real second while the clock is running, or
    ///   <see langword="null"/> when the clock is stopped.
    /// </param>
    internal PrimeTestClockNewTimeEvent (
        DateTimeOffset clockTime,
        TimeSpan? runRateTimeSpan)
        : base(PrimeTestClockEventType.NewTime)
    {
        _clockTime = clockTime;
        _runRateTimeSpan = runRateTimeSpan;
    }
    //----------------------------------------------------------------------------

    #endregion Constructors/Finalizers

    //----------------------------------------------------------------------------
    /// <summary>
    ///   Gets the virtual UTC time at which this event was raised.
    /// </summary>
    public DateTimeOffset ClockTime { [DebuggerStepThrough] get => _clockTime; }
    //----------------------------------------------------------------------------

    //----------------------------------------------------------------------------
    /// <summary>
    ///   Gets the virtual time advanced per real second while the clock is running, or
    ///   <see langword="null"/> when the clock is stopped.
    /// </summary>
    public TimeSpan? RunRateTimeSpan { [DebuggerStepThrough] get => _runRateTimeSpan; }
    //----------------------------------------------------------------------------
}
//################################################################################

#endif
