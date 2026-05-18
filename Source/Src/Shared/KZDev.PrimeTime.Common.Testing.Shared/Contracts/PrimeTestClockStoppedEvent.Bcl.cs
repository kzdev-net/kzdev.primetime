#if !NODATIME

using System.Diagnostics;

#if SYSTEMCLOCK
namespace KZDev.SystemClock.PrimeTime.Testing;
#else
namespace KZDev.PrimeTime.Testing;
#endif

//################################################################################
public sealed partial class PrimeTestClockStoppedEvent
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
    ///   Initializes a new instance of the <see cref="PrimeTestClockStoppedEvent"/> class.
    /// </summary>
    /// <param name="clockTime">
    ///   The final committed virtual UTC time after the runner stopped.
    /// </param>
    /// <param name="runRateTimeSpan">
    ///   The virtual time advanced per real second that was active before the runner stopped, or
    ///   <see langword="null"/> if no rate was configured.
    /// </param>
    internal PrimeTestClockStoppedEvent (
        DateTimeOffset clockTime,
        TimeSpan? runRateTimeSpan)
        : base(PrimeTestClockEventType.ClockStopped)
    {
        _clockTime = clockTime;
        _runRateTimeSpan = runRateTimeSpan;
    }
    //----------------------------------------------------------------------------

    #endregion Constructors/Finalizers

    //----------------------------------------------------------------------------
    /// <summary>
    ///   Gets the final committed virtual UTC time after the runner stopped.
    /// </summary>
    public DateTimeOffset ClockTime { [DebuggerStepThrough] get => _clockTime; }
    //----------------------------------------------------------------------------

    //----------------------------------------------------------------------------
    /// <summary>
    ///   Gets the virtual time advanced per real second that was active before the runner stopped, or
    ///   <see langword="null"/> if no rate was configured.
    /// </summary>
    public TimeSpan? RunRateTimeSpan { [DebuggerStepThrough] get => _runRateTimeSpan; }
    //----------------------------------------------------------------------------
}
//################################################################################

#endif
