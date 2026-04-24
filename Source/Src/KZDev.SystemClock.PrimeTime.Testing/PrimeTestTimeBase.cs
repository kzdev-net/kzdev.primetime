namespace KZDev.SystemClock.PrimeTime;

//################################################################################
/// <summary>
///   BCL virtual-time storage and event args for <see cref="PrimeTestTimeBase"/>.
/// </summary>
public abstract partial class PrimeTestTimeBase
{
    //----------------------------------------------------------------------------
    /// <summary>
    ///   The current virtual UTC time; read and updated under the shared gate lock in the main partial.
    /// </summary>
    protected DateTimeOffset _utcNow;
    //----------------------------------------------------------------------------

    #region Constructors/Finalizers

    //----------------------------------------------------------------------------
    /// <summary>
    ///   Initializes a new instance with virtual UTC time set to <see cref="DateTimeOffset.UtcNow"/>.
    /// </summary>
    protected PrimeTestTimeBase ()
    {
        _utcNow = DateTimeOffset.UtcNow;
    }
    //----------------------------------------------------------------------------

    //----------------------------------------------------------------------------
    /// <summary>
    ///   Initializes a new instance with the specified initial virtual UTC time.
    /// </summary>
    /// <param name="initialUtcTime">The initial virtual UTC time.</param>
    protected PrimeTestTimeBase (DateTimeOffset initialUtcTime)
    {
        _utcNow = initialUtcTime;
    }
    //----------------------------------------------------------------------------

    #endregion Constructors/Finalizers

    //----------------------------------------------------------------------------
    /// <summary>
    ///   Reads the virtual UTC time. The caller must hold the shared gate lock.
    /// </summary>
    /// <returns>
    ///   The current virtual UTC time.
    /// </returns>
    internal partial DateTimeOffset ReadVirtualUtcNowLocked () => _utcNow;
    //----------------------------------------------------------------------------
}
//################################################################################
