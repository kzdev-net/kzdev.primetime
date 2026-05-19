namespace KZDev.SystemClock.PrimeTime.Testing;

//################################################################################
public sealed partial class PrimeTestClockNewTimeEvent
{
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
        : base(PrimeTestClockEventType.NewTime, clockTime, runRateTimeSpan)
    {
    }
    //----------------------------------------------------------------------------

    #endregion Constructors/Finalizers
}
//################################################################################
