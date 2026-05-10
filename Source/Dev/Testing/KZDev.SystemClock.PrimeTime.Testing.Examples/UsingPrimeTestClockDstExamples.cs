// Copyright (c) Kevin Zehrer
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

using KZDev.SystemClock.PrimeTime;
using KZDev.SystemClock.PrimeTime.Testing.Examples.Infrastructure;

namespace KZDev.SystemClock.PrimeTime.Testing.Examples;

/// <summary>
/// Demonstrates environment-aware daylight-saving probes for <see cref="TimeZoneInfo.Local"/> alongside
/// stable virtual-clock advancement.
/// </summary>
public sealed class UsingPrimeTestClockDstExamples
{
    #region Snippet
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

    /// <summary>
    /// When the machine exposes a fall-back ambiguous example, documents that the BCL reports two
    /// distinct UTC offsets for the same local wall-clock calendar value.
    /// </summary>
    [Fact]
    public void MachineProbe_AmbiguousExample_WhenPresent_HasTwoOffsets ()
    {
        TestEnvironmentDescriptor descriptor = TestEnvironmentDescriptor.FromLocalMachine();
        if (descriptor.AmbiguousLocalWallClockExample is not DateTime ambiguous)
        {
            return;
        }

        TimeZoneInfo.Local.IsAmbiguousTime(ambiguous).Should().BeTrue();
        TimeSpan[] offsets = TimeZoneInfo.Local.GetAmbiguousTimeOffsets(ambiguous);
        offsets.Should().HaveCount(2);
    }

    /// <summary>
    /// Verifies advancing virtual UTC time across a long step does not throw for the default test clock,
    /// providing a stable baseline on hosts without rich DST probe data.
    /// </summary>
    [Fact]
    public void VirtualClock_AdvanceLargeStep_Completes ()
    {
        IPrimeTestClock clock = new PrimeTestClock(new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero));
        Action act = () => clock.Advance(TimeSpan.FromDays(400));
        act.Should().NotThrow();
    }
    #endregion Snippet
}
