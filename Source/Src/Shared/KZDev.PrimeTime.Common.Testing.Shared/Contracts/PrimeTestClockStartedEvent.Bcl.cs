#if !NODATIME

using System.Diagnostics;

#if SYSTEMCLOCK
namespace KZDev.SystemClock.PrimeTime.Testing;
#else
namespace KZDev.PrimeTime.Testing;
#endif

//################################################################################
public sealed partial class PrimeTestClockStartedEvent
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
    ///   Initializes a new instance of the <see cref="PrimeTestClockStartedEvent"/> class.
    /// </summary>
    /// <param name="clockTime">
    ///   The committed virtual UTC time when the runner started.
    /// </param>
    /// <param name="runRateTimeSpan">
    ///   The virtual time advanced per real second for the newly started runner.
    /// </param>
    internal PrimeTestClockStartedEvent (
        DateTimeOffset clockTime,
        TimeSpan? runRateTimeSpan)
        : base(PrimeTestClockEventType.ClockStarted)
    {
        _clockTime = clockTime;
        _runRateTimeSpan = runRateTimeSpan;
    }
    //----------------------------------------------------------------------------

    #endregion Constructors/Finalizers

    //----------------------------------------------------------------------------
    /// <summary>
    ///   Gets the committed virtual UTC time when the runner started.
    /// </summary>
    public DateTimeOffset ClockTime { [DebuggerStepThrough] get => _clockTime; }
    //----------------------------------------------------------------------------

    //----------------------------------------------------------------------------
    /// <summary>
    ///   Gets the virtual time advanced per real second for the newly started runner.
    /// </summary>
    public TimeSpan? RunRateTimeSpan { [DebuggerStepThrough] get => _runRateTimeSpan; }
    //----------------------------------------------------------------------------
}
//################################################################################

#endif
