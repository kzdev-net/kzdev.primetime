// Copyright (c) Kevin Zehrer
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

namespace KZDev.SystemClock.PrimeTime.Testing;

//################################################################################
/// <summary>
///   System Clock partial of <see cref="PrimeTestClock"/>: BCL <see cref="DateTimeOffset"/> virtual storage and
///   <see cref="PrimeTestClockEvent"/> payloads for <see cref="IPrimeTestClock.ClockEvents"/>.
/// </summary>
/// <remarks>
///   Shared march, runner, and persist-on-read behavior live in the common partial; see <see cref="IPrimeTestClock"/>.
/// </remarks>
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
    ///   Raises <see cref="PrimeTestClockEventType.NewTime"/> on <see cref="IPrimeTestClock.ClockEvents"/>.
    /// </summary>
    /// <remarks>
    ///   <para>
    ///     The payload is a <see cref="PrimeTestClockNewTimeEvent"/> with
    ///     <see cref="PrimeTestClockTimedEvent.ClockTime"/> set to <paramref name="utcNowDateTimeOffset"/>.
    ///   </para>
    ///   <para>
    ///     <see cref="PrimeTestClockTimedEvent.RunRateTimeSpan"/> is the active runner rate (virtual time per real
    ///     second) when <see cref="IPrimeTestTime.IsRunning"/> is <c>true</c>, and <see langword="null"/> when the clock
    ///     is stopped. Subscribers can use that property to tell whether the raise occurred while the automatic runner
    ///     was advancing virtual time.
    ///   </para>
    /// </remarks>
    /// <param name="utcNowDateTimeOffset">The virtual UTC time after the change.</param>
    private partial void RaiseNewTimeEvent (DateTimeOffset utcNowDateTimeOffset)
    {
        TimeSpan? runRateTimeSpan;
        lock (Gate)
            runRateTimeSpan = InternalIsRunning ? _runRate : null;

        InvokeClockEvents(new PrimeTestClockNewTimeEvent(utcNowDateTimeOffset, runRateTimeSpan));
    }
    //----------------------------------------------------------------------------
    /// <summary>
    ///   Raises <see cref="PrimeTestClockEventType.ClockStarted"/> on <see cref="IPrimeTestClock.ClockEvents"/>.
    /// </summary>
    /// <param name="startUtc">Committed virtual UTC when the runner started.</param>
    /// <param name="runRateTimeSpan">Active runner rate for the new run.</param>
    private partial void RaiseClockStartedEvent (DateTimeOffset startUtc, TimeSpan runRateTimeSpan) =>
        InvokeClockEvents(new PrimeTestClockStartedEvent(startUtc, runRateTimeSpan));
    //----------------------------------------------------------------------------
    /// <summary>
    ///   Raises <see cref="PrimeTestClockEventType.ClockStopped"/> on <see cref="IPrimeTestClock.ClockEvents"/>.
    /// </summary>
    /// <param name="finalUtc">Final committed virtual UTC after the runner stopped.</param>
    /// <param name="runRateTimeSpan">Runner rate that was active before stop.</param>
    private partial void RaiseClockStoppedEvent (DateTimeOffset finalUtc, TimeSpan? runRateTimeSpan) =>
        InvokeClockEvents(new PrimeTestClockStoppedEvent(finalUtc, runRateTimeSpan));
    //----------------------------------------------------------------------------

    #region IPrimeClock Implementation — Local schedule zone

    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public TimeZoneInfo LocalScheduleTimeZone => TimeZoneInfo.Local;
    //----------------------------------------------------------------------------

    #endregion IPrimeClock Implementation — Local schedule zone
}
//################################################################################
