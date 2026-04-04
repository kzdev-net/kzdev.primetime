using System.Diagnostics;
using NodaTime;

namespace KZDev.PrimeTime;
//################################################################################

/// <summary>
///   Provides data for the <see cref="IPrimeTestClock.ClockEvents"/> event, including the
///   current instant after the change.
/// </summary>
[DebuggerStepThrough]
public sealed class NodaClockTimeChangedEventArgs : ClockTimeChangedEventArgs
{
    #region Constructors/Finalizers

    //----------------------------------------------------------------------------
    /// <summary>
    ///   Initializes a new instance of the <see cref="NodaClockTimeChangedEventArgs"/> class.
    /// </summary>
    /// <param name="instant">
    ///   The new current instant (UTC) of the clock after the change.
    /// </param>
    public NodaClockTimeChangedEventArgs (Instant instant)
        : base(new DateTimeOffset(instant.ToDateTimeUtc(), TimeSpan.Zero))
    {
        Instant = instant;
    }
    //----------------------------------------------------------------------------

    #endregion Constructors/Finalizers

    //----------------------------------------------------------------------------
    /// <summary>
    ///   Gets the current instant (UTC) of the clock after the time change.
    /// </summary>
    public Instant Instant { get; }
    //----------------------------------------------------------------------------
}
//################################################################################
