namespace KZDev.SystemClock.PrimeTime;

/// <summary>
///   BCL virtual-time storage and event args for <see cref="PrimeTestClock"/>.
/// </summary>
public sealed partial class PrimeTestClock
{
    private DateTimeOffset _utcNow;

    #region Constructors/Finalizers

    /// <summary>
    ///   Initializes a new instance with virtual UTC time set to <see cref="DateTimeOffset.UtcNow"/>.
    /// </summary>
    public PrimeTestClock ()
    {
        _utcNow = DateTimeOffset.UtcNow;
    }

    /// <summary>
    ///   Initializes a new instance with the specified initial virtual UTC time.
    /// </summary>
    /// <param name="initialUtcTime">The initial virtual UTC time.</param>
    public PrimeTestClock (DateTimeOffset initialUtcTime)
    {
        _utcNow = initialUtcTime;
    }

    #endregion Constructors/Finalizers

    private partial DateTimeOffset ToLocalOffset (DateTimeOffset utcNowOffset) =>
        TimeZoneInfo.ConvertTime(utcNowOffset, TimeZoneInfo.Local);

    private partial TimeSpan GetLocalWallClockUtcOffset (DateTime localUnspecified) =>
        TimeZoneInfo.Local.GetUtcOffset(DateTime.SpecifyKind(localUnspecified, DateTimeKind.Unspecified));

    private partial void SetVirtualUtcNowLocked (DateTimeOffset utcNowOffset) => _utcNow = utcNowOffset;

    private partial DateTimeOffset ReadVirtualUtcNowLocked () => _utcNow;

    private partial void AddVirtualTimeLocked (TimeSpan duration) => _utcNow += duration;

    private partial void RaiseClockEventsAfterVirtualUtcChange (DateTimeOffset utcNowOffset) =>
        ClockEvents?.Invoke(this, new ClockTimeChangedEventArgs(utcNowOffset));
}
