namespace KZDev.PrimeTime
{
    //################################################################################
    /// <summary>
    /// Extends <see cref="IPrimeTime"/> with test-controllable time support. Used by both
    /// test clock implementations (system and NodaTime test clocks in their respective assemblies).
    /// </summary>
    public interface IPrimeTestTime : IPrimeTime
    {
        //--------------------------------------------------------------------------------
        /// <summary>
        /// Gets whether the test clock is currently running (e.g. time is advancing under
        /// RunFor or Start). When <c>false</c>, time is frozen and SetTime/Advance control
        /// the current time.
        /// </summary>
        bool IsRunning { get; }
        //--------------------------------------------------------------------------------
    }
    //################################################################################
}
