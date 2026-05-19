namespace KZDev.SystemClock.PrimeTime.Testing;

//################################################################################
public sealed partial class PrimeTestClockStoppedEvent
{
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
        : base(PrimeTestClockEventType.ClockStopped, clockTime, runRateTimeSpan)
    {
    }
    //----------------------------------------------------------------------------

    #endregion Constructors/Finalizers
}
//################################################################################
