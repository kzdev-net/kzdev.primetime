namespace KZDev.PrimeTime;

//################################################################################
/// <summary>
///   Registration for an interval timer created on an <see cref="IPrimeSystemClock"/>,
///   supporting change of due time and repeat interval.
/// </summary>
public interface IClockIntervalTimer : IIntervalTimer, IClockTimer
{
    //--------------------------------------------------------------------------------
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
    ///   intervals. Use <see cref="Change(TimeSpan, TimeSpan)"/> to set next and repeat
    ///   intervals separately.
    /// </remarks>
    bool Change (TimeSpan interval);
    //--------------------------------------------------------------------------------
    /// <summary>
    ///   Changes the next callback time and, for repeating timers, the repeat interval.
    /// </summary>
    /// <param name="nextInterval">
    ///   The time until the next callback.
    /// </param>
    /// <param name="repeatInterval">
    ///   The interval for subsequent callbacks. Use <see cref="Timeout.InfiniteTimeSpan"/>
    ///   for a one-shot timer (no repeat).
    /// </param>
    /// <returns>
    ///   <c>true</c> if the change was applied; <c>false</c> if the registration was
    ///   cancelled, disposed, or otherwise invalid.
    /// </returns>
    /// <remarks>
    ///   For a currently non-repeating timer, passing a finite <paramref name="repeatInterval"/>
    ///   may be disallowed by the implementation (e.g. throw <see cref="InvalidOperationException"/>).
    /// </remarks>
    bool Change (TimeSpan nextInterval, TimeSpan repeatInterval);
    //--------------------------------------------------------------------------------
}
//################################################################################
