// Copyright (c) Kevin Zehrer
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

#if NET
namespace KZDev.SystemClock.PrimeTime.Testing.UnitTests;

//################################################################################
public partial class UsingIPrimeClock
{
    /// <summary>
    ///   Verifies that <see cref="PrimeTestClock"/> exposes <see cref="IPrimeClock.LocalScheduleTimeZone"/>
    ///   consistent with its local-time mapping (<see cref="TimeZoneInfo.Local"/>).
    /// </summary>
    [Fact]
    public void PrimeTestClock_LocalScheduleTimeZone_MatchesTimeZoneInfoLocal ()
    {
        IPrimeClock clock = new PrimeTestClock();
        clock.LocalScheduleTimeZone.Should().Be(TimeZoneInfo.Local);
    }
}
//################################################################################
#endif
