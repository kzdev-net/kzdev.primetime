namespace KZDev.PrimeTime;

//################################################################################
/// <summary>
///   Provides data for the <see cref="IPrimeTestSystemClock.ClockEvents"/> event.
/// </summary>
public sealed class ClockTimeChangedEventArgs : EventArgs
{
    #region Constructors/Finalizers

    /// <summary>
    ///   Initializes a new instance of the <see cref="ClockTimeChangedEventArgs"/> class.
    /// </summary>
    /// <param name="utcNow">
    ///   The new current UTC time of the clock after the change.
    /// </param>
    public ClockTimeChangedEventArgs (DateTimeOffset utcNow)
    {
        UtcNow = utcNow;
    }

    #endregion Constructors/Finalizers

    /// <summary>
    ///   Gets the current UTC time of the clock after the time change.
    /// </summary>
    public DateTimeOffset UtcNow { get; }
}
//################################################################################
