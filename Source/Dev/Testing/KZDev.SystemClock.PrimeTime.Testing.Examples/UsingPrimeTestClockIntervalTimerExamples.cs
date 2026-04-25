// Copyright (c) Kevin Zehrer
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

using KZDev.SystemClock.PrimeTime;

namespace KZDev.SystemClock.PrimeTime.Testing.Examples;

/// <summary>
/// Demonstrates interval timers under a <see cref="PrimeTestClock"/> where callbacks fire only when
/// virtual time is advanced.
/// </summary>
public sealed class UsingPrimeTestClockIntervalTimerExamples
{
    /// <summary>
    /// Verifies a one-shot interval timer fires after virtual time advances past the initial delay.
    /// </summary>
    [Fact]
    public void IntervalTimer_AdvanceOneShot_FiresOnce ()
    {
        IPrimeTestClock clock = new PrimeTestClock(new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero));
        int fired = 0;
        using IClockIntervalTimer registration = clock.RegisterTimer(
            TimeSpan.FromMinutes(5),
            Timeout.InfiniteTimeSpan,
            _ => fired++,
            CancellationToken.None);

        clock.Advance(TimeSpan.FromMinutes(4));
        fired.Should().Be(0);
        clock.Advance(TimeSpan.FromMinutes(1));
        fired.Should().Be(1);
        registration.IsRepeating.Should().BeFalse();
    }

    /// <summary>
    /// Verifies a repeating interval timer advances its schedule deterministically under
    /// <see cref="IPrimeTestClock.Advance(System.TimeSpan)"/>.
    /// </summary>
    [Fact]
    public void IntervalTimer_AdvanceRepeating_FiresOnEachInterval ()
    {
        IPrimeTestClock clock = new PrimeTestClock(new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero));
        int fired = 0;
        using IClockIntervalTimer registration = clock.RegisterTimer(
            TimeSpan.FromMinutes(2),
            TimeSpan.FromMinutes(3),
            _ => fired++,
            CancellationToken.None);

        clock.Advance(TimeSpan.FromMinutes(2));
        fired.Should().Be(1);
        clock.Advance(TimeSpan.FromMinutes(3));
        fired.Should().Be(2);
        registration.IsRepeating.Should().BeTrue();
    }

    /// <summary>
    /// Verifies an asynchronous interval callback completes when virtual time reaches the due instant.
    /// </summary>
    [Fact]
    public void IntervalTimer_RegisterAsyncTimer_Advance_CompletesCallback ()
    {
        IPrimeTestClock clock = new PrimeTestClock(new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero));
        int fired = 0;
        using IClockIntervalTimer registration = clock.RegisterAsyncTimer(
            TimeSpan.FromSeconds(1),
            Timeout.InfiniteTimeSpan,
            async (_, cancellationToken) =>
            {
                fired++;
                await Task.Delay(0, cancellationToken).ConfigureAwait(false);
            },
            CancellationToken.None);

        clock.Advance(TimeSpan.FromSeconds(1));
        fired.Should().Be(1);
        registration.State.Should().Be(TimerState.Completed);
    }
}
