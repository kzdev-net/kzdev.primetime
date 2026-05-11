// Copyright (c) Kevin Zehrer
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace KZDev.SystemClock.PrimeTime.Testing;

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
    internal DateTimeOffset UtcNow { [DebuggerStepThrough] get; [DebuggerStepThrough] set; }
    //----------------------------------------------------------------------------

    #region Constructors/Finalizers

    //----------------------------------------------------------------------------
    /// <summary>
    ///   Initializes a new instance with virtual UTC time set to <see cref="DateTimeOffset.UtcNow"/>.
    /// </summary>
    internal PrimeTestTimeBase ()
    {
        UtcNow = DateTimeOffset.UtcNow;
    }
    //----------------------------------------------------------------------------
    /// <summary>
    ///   Initializes a new instance with the specified initial virtual UTC time.
    /// </summary>
    /// <param name="initialUtcTime">The initial virtual UTC time.</param>
    internal PrimeTestTimeBase (DateTimeOffset initialUtcTime)
    {
        UtcNow = initialUtcTime;
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
    internal partial DateTimeOffset ReadVirtualUtcNowLocked () => UtcNow;
    //----------------------------------------------------------------------------
}
//################################################################################
