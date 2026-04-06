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
    /// <param name="utcNowDateTimeOffset">
    ///   The new current UTC time of the clock after the change.
    /// </param>
    public ClockTimeChangedEventArgs (DateTimeOffset utcNowDateTimeOffset)
    {
        UtcNowDateTimeOffset = utcNowDateTimeOffset;
    }
    //----------------------------------------------------------------------------

    #endregion Constructors/Finalizers

    //----------------------------------------------------------------------------
    /// <summary>
    ///   Gets the current UTC time of the clock after the time change.
    /// </summary>
    public DateTimeOffset UtcNowDateTimeOffset { get; }
    //----------------------------------------------------------------------------
}
//################################################################################
