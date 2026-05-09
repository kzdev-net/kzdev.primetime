// Copyright (c) Kevin Zehrer
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

namespace KZDev.SystemClock.PrimeTime.Testing;

//################################################################################
/// <summary>
///   BCL virtual-time storage and event args for <see cref="PrimeTestClock"/>.
/// </summary>
public sealed partial class PrimeTestClock
{
    #region Constructors/Finalizers

    //----------------------------------------------------------------------------
    /// <summary>
    ///   Initializes a new instance with virtual UTC time set to <see cref="DateTimeOffset.UtcNow"/>.
    /// </summary>
    public PrimeTestClock ()
    {
    }
    //----------------------------------------------------------------------------
    /// <summary>
    ///   Initializes a new instance with the specified initial virtual UTC time.
    /// </summary>
    /// <param name="initialUtcTime">The initial virtual UTC time.</param>
    public PrimeTestClock (DateTimeOffset initialUtcTime) : base(initialUtcTime)
    {
    }
    //----------------------------------------------------------------------------

    #endregion Constructors/Finalizers

    //----------------------------------------------------------------------------
    /// <summary>
    ///   Converts virtual UTC to a local <see cref="DateTimeOffset"/> using <see cref="TimeZoneInfo.Local"/>.
    /// </summary>
    /// <param name="utcNowOffset">The virtual instant in UTC.</param>
    /// <returns>
    ///   The same instant represented in the local time zone.
    /// </returns>
    private partial DateTimeOffset ToLocalOffset (DateTimeOffset utcNowOffset) =>
        TimeZoneInfo.ConvertTime(utcNowOffset, TimeZoneInfo.Local);
    //----------------------------------------------------------------------------
    /// <summary>
    ///   Gets the UTC offset for an unspecified-kind local wall-clock value in <see cref="TimeZoneInfo.Local"/>.
    /// </summary>
    /// <param name="localUnspecified">The local date and time without a <see cref="DateTime.Kind"/>.</param>
    /// <returns>
    ///   The offset the local zone applies to <paramref name="localUnspecified"/>.
    /// </returns>
    private partial TimeSpan GetLocalWallClockUtcOffset (DateTime localUnspecified) =>
        TimeZoneInfo.Local.GetUtcOffset(DateTime.SpecifyKind(localUnspecified, DateTimeKind.Unspecified));
    //----------------------------------------------------------------------------
    /// <summary>
    ///   Assigns the virtual UTC time. The caller must hold the shared gate lock.
    /// </summary>
    /// <param name="utcNowOffset">The new virtual UTC time.</param>
    private partial void SetVirtualUtcNowLocked (DateTimeOffset utcNowOffset) => UtcNow = utcNowOffset;
    //----------------------------------------------------------------------------
    /// <summary>
    ///   Advances virtual time by the given amount. The caller must hold the shared gate lock.
    /// </summary>
    /// <param name="duration">The virtual elapsed time to add.</param>
    private partial void AddVirtualTimeLocked (TimeSpan duration) => UtcNow += duration;
    //----------------------------------------------------------------------------
    /// <summary>
    ///   Raises <see cref="IPrimeTestClock.ClockEvents"/> after virtual UTC time changed.
    /// </summary>
    /// <param name="utcNowDateTimeOffset">The virtual UTC time after the change.</param>
    private partial void RaiseClockEventsAfterVirtualUtcChange (DateTimeOffset utcNowDateTimeOffset) =>
        ClockEvents?.Invoke(this, new ClockTimeChangedEventArgs(utcNowDateTimeOffset));
    //----------------------------------------------------------------------------

    #region IPrimeClock Implementation — Local schedule zone

    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public TimeZoneInfo LocalScheduleTimeZone => TimeZoneInfo.Local;
    //----------------------------------------------------------------------------

    #endregion IPrimeClock Implementation — Local schedule zone
}
//################################################################################
