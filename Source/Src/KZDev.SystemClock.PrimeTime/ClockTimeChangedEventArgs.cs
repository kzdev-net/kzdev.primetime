using System.Diagnostics;

namespace KZDev.SystemClock.PrimeTime;

//################################################################################
/// <summary>
///   Provides data for the <see cref="IPrimeTestClock.ClockEvents"/> event.
/// </summary>
[DebuggerStepThrough]
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
