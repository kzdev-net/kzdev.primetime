// Copyright (c) Kevin Zehrer
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

using KZDev.SystemClock.PrimeTime;

namespace KZDev.SystemClock.PrimeTime.Testing.Examples;

/// <summary>
/// Demonstrates <see cref="IPrimeTestClock"/> virtual control APIs used in deterministic tests.
/// </summary>
public sealed class UsingPrimeTestClockControlExamples
{
    /// <summary>
    /// Verifies <see cref="IPrimeTestClock.SetTime(System.DateTimeOffset)"/> moves the virtual UTC
    /// timeline used by <see cref="IPrimeClock.UtcNowDateTimeOffset"/>.
    /// </summary>
    [Fact]
    public void TestClock_SetTime_Advance_UpdatesUtcNow ()
    {
        DateTimeOffset start = new DateTimeOffset(2025, 1, 1, 12, 0, 0, TimeSpan.Zero);
        IPrimeTestClock clock = new PrimeTestClock(start);
        clock.SetTime(new DateTimeOffset(2025, 6, 1, 8, 0, 0, TimeSpan.Zero));
        clock.Advance(TimeSpan.FromHours(3));
        clock.UtcNowDateTimeOffset.Should().Be(new DateTimeOffset(2025, 6, 1, 11, 0, 0, TimeSpan.Zero));
    }

    /// <summary>
    /// Verifies <see cref="IPrimeTestClock.RunFor(System.TimeSpan)"/> advances virtual time the same
    /// way as a single <see cref="IPrimeTestClock.Advance(System.TimeSpan)"/> step of the same length.
    /// </summary>
    [Fact]
    public void TestClock_RunFor_TimeSpan_MatchesAdvanceStep ()
    {
        DateTimeOffset start = new DateTimeOffset(2025, 2, 1, 0, 0, 0, TimeSpan.Zero);
        IPrimeTestClock clockA = new PrimeTestClock(start);
        IPrimeTestClock clockB = new PrimeTestClock(start);
        TimeSpan step = TimeSpan.FromMinutes(40);
        clockA.Advance(step);
        clockB.RunFor(step);
        clockA.UtcNowDateTimeOffset.Should().Be(clockB.UtcNowDateTimeOffset);
    }

    /// <summary>
    /// Verifies <see cref="IPrimeTestClock.Start(System.TimeSpan?)"/> toggles
    /// <see cref="IPrimeTestTime.IsRunning"/> and <see cref="IPrimeTestClock.Stop"/> stops automatic advancement.
    /// </summary>
    [Fact]
    public void TestClock_StartStop_TogglesIsRunning ()
    {
        IPrimeTestClock clock = new PrimeTestClock(new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero));
        clock.IsRunning.Should().BeFalse();
        clock.Start(TimeSpan.FromSeconds(1));
        clock.IsRunning.Should().BeTrue();
        clock.Stop().Should().BeTrue();
        clock.IsRunning.Should().BeFalse();
    }
}
