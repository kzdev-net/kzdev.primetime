using System.Diagnostics;

#if SYSTEMCLOCK
namespace KZDev.SystemClock.PrimeTime;
#else
namespace KZDev.PrimeTime;
//################################################################################
#endif

/// <summary>
///   Provides data for the <see cref="IPrimeTestClock.ClockEvents"/> event.
/// </summary>
[DebuggerStepThrough]
public class ClockTimeChangedEventArgs : EventArgs
{
    #region Constructors/Finalizers

    //----------------------------------------------------------------------------
    /// <summary>
    ///   Initializes a new instance of the <see cref="ClockTimeChangedEventArgs"/> class.
    /// </summary>
    /// <param name="utcNowOffset">
    ///   The new current UTC time of the clock after the change.
    /// </param>
    public ClockTimeChangedEventArgs (DateTimeOffset utcNowOffset)
    {
        UtcNowOffset = utcNowOffset;
    }
    //----------------------------------------------------------------------------

    #endregion Constructors/Finalizers

    //----------------------------------------------------------------------------
    /// <summary>
    ///   Gets the current UTC time of the clock after the time change.
    /// </summary>
    public DateTimeOffset UtcNowOffset { get; }
    //----------------------------------------------------------------------------
}
//################################################################################
