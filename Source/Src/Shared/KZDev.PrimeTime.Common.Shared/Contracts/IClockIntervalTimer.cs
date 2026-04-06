#if SYSTEMCLOCK
namespace KZDev.SystemClock.PrimeTime;
#else
namespace KZDev.PrimeTime;
#endif

//################################################################################
/// <summary>
///   Registration for an interval timer created from a clock's timer registration API;
///   supporting change of due time and repeat interval (BCL <see cref="TimeSpan"/> subset).
/// </summary>
/// <remarks>
///   <para>
///     This interface extends <see cref="System.Threading.ITimer"/> on all target frameworks.
///     On .NET 8+ the type is in the shared framework; on .NET Standard 2.0 (and .NET Framework
///     when using the same compatibility stack) it is supplied by the
///     <c>Microsoft.Bcl.TimeProvider</c> package referenced by this library.
///   </para>
/// </remarks>
public partial interface IClockIntervalTimer : IIntervalTimer, global::System.Threading.ITimer
{
    /// <summary>
    ///   Changes the interval of this registration.
    /// </summary>
    /// <param name="interval">
    ///   The time to delay before the next callback. For a repeating timer, this also
    ///   becomes the repeat interval.
    /// </param>
    /// <returns>
    ///   <c>true</c> if the change was applied; <c>false</c> if the registration was
    ///   cancelled, disposed, or otherwise invalid.
    /// </returns>
    /// <remarks>
    ///   Does not change whether the timer is repeating or one-shot. For a repeating
    ///   timer, <paramref name="interval"/> is used for both the next and subsequent
    ///   intervals. To set the next due time and repeat interval independently, use
    ///   <see cref="System.Threading.ITimer.Change(System.TimeSpan,System.TimeSpan)"/>.
    /// </remarks>
    bool Change (TimeSpan interval);
    //----------------------------------------------------------------------------
}
//################################################################################
