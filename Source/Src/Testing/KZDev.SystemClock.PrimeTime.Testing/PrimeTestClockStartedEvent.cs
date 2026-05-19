namespace KZDev.SystemClock.PrimeTime.Testing;

//################################################################################
public sealed partial class PrimeTestClockStartedEvent
{
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
    internal PrimeTestClockStartedEvent (DateTimeOffset clockTime,
        TimeSpan runRateTimeSpan)
        : base(PrimeTestClockEventType.ClockStarted, clockTime, runRateTimeSpan)
    {
    }
    //----------------------------------------------------------------------------

    #endregion Constructors/Finalizers
}
//################################################################################
