// Copyright (c) Kevin Zehrer
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;

using AwesomeAssertions;

using KZDev.PrimeTime.Tests;

using NodaTime;
using NodaTime.TimeZones;

namespace KZDev.PrimeTime.Testing.UnitTests;

//################################################################################
/// <summary>
///   Validates <see cref="IPrimeClock.LocalScheduleDateTimeZone"/> on <see cref="PrimeTestClock"/> against
///   <see cref="IPrimeClock.LocalScheduleTimeZone"/> mapping rules.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed class UsingIPrimeClockLocalScheduleDateTimeZone : UnitTestBase
{
    #region Constructors/Finalizers

    //----------------------------------------------------------------------------
    /// <summary>
    ///   Initializes a new instance of the <see cref="UsingIPrimeClockLocalScheduleDateTimeZone"/> class.
    /// </summary>
    /// <param name="xUnitTestOutputHelper">
    ///   The xUnit test output helper that can be used to output test messages.
    /// </param>
    public UsingIPrimeClockLocalScheduleDateTimeZone (ITestOutputHelper xUnitTestOutputHelper)
        : base(xUnitTestOutputHelper)
    {
    }
    //----------------------------------------------------------------------------

    #endregion Constructors/Finalizers

    //----------------------------------------------------------------------------
    /// <summary>
    ///   Resolves a US Eastern zone by id, trying IANA id first then Windows id.
    /// </summary>
    /// <returns>A non-null <see cref="TimeZoneInfo"/>.</returns>
    private static TimeZoneInfo ResolveUsEasternTimeZone ()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("America/New_York");
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
        }
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies <see cref="PrimeTestClock.LocalScheduleDateTimeZone"/> is the configured Noda zone and maps to
    ///   <see cref="IPrimeClock.LocalScheduleTimeZone"/> via <see cref="NodaDateTimeZoneBclConversion"/>.
    /// </summary>
    [Fact]
    public void PrimeTestClock_WithBclZone_LocalScheduleDateTimeZone_MapsToLocalScheduleTimeZone ()
    {
        TimeZoneInfo synthetic = ResolveUsEasternTimeZone();
        DateTimeZone zone = BclDateTimeZone.FromTimeZoneInfo(synthetic);
        IPrimeClock clock = new PrimeTestClock(Instant.FromUtc(2024, 6, 1, 12, 0, 0), zone);
        clock.LocalScheduleDateTimeZone.Should().BeSameAs(zone);
        NodaDateTimeZoneBclConversion.GetLocalScheduleTimeZoneInfo(clock.LocalScheduleDateTimeZone).Should().BeSameAs(
            synthetic);
        clock.LocalScheduleTimeZone.Should().BeSameAs(synthetic);
    }
    //----------------------------------------------------------------------------
}
//################################################################################
