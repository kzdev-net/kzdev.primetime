// Copyright (c) Kevin Zehrer
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

using KZDev.PrimeTime;
using KZDev.PrimeTime.Testing.Examples.Infrastructure;

using NodaTime;

namespace KZDev.PrimeTime.Testing.Examples;

/// <summary>
/// Demonstrates DST-aware behavior using a fixed TZDB zone so results do not depend on the host's
/// <see cref="TimeZoneInfo.Local"/> configuration.
/// </summary>
public sealed class UsingPrimeTestClockDstExamples
{
    #region Snippet
    /// <summary>
    /// Verifies advancing virtual time across a US Eastern spring-forward transition changes the
    /// resolved UTC offset for local zoned "now".
    /// </summary>
    [Fact]
    public void DstSpring_Advance_ChangesLocalUtcOffset ()
    {
        Instant before = Instant.FromUtc(2024, 3, 10, 6, 30, 0);
        IPrimeTestClock clock = NodaDstScenarioFixture.CreateClock(before);
        Offset offsetBefore = clock.LocalZonedNowInstant.Offset;
        clock.Advance(Duration.FromHours(2));
        Offset offsetAfter = clock.LocalZonedNowInstant.Offset;
        offsetAfter.Should().NotBe(offsetBefore);
    }

    /// <summary>
    /// Verifies a fall-back ambiguous local wall-clock time maps leniently to a single virtual instant
    /// on the clock's configured zone.
    /// </summary>
    [Fact]
    public void DstFallAmbiguous_SetLocalTime_LenientMappingIsStable ()
    {
        DateTimeZone eastern = NodaDstScenarioFixture.UsEastern;
        LocalDateTime ambiguousWall = new(2024, 11, 3, 1, 30);
        ZonedDateTime lenient = eastern.AtLeniently(ambiguousWall);
        IPrimeTestClock clock = new PrimeTestClock(lenient.ToInstant(), eastern);
        clock.SetLocalTime(ambiguousWall);
        clock.LocalZonedNowInstant.LocalDateTime.Should().Be(lenient.LocalDateTime);
    }

    /// <summary>
    /// When the machine exposes a spring-forward gap example, documents that the BCL marks that
    /// wall-clock value as invalid for <see cref="TimeZoneInfo.Local"/>.
    /// </summary>
    [Fact]
    public void MachineProbe_InvalidExample_WhenPresent_IsInvalidLocalTime ()
    {
        TestEnvironmentDescriptor descriptor = TestEnvironmentDescriptor.FromLocalMachine();
        if (descriptor.InvalidLocalWallClockExample is not DateTime invalid)
        {
            return;
        }

        TimeZoneInfo.Local.IsInvalidTime(invalid).Should().BeTrue();
    }
    #endregion Snippet
}
