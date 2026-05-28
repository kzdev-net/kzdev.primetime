// Copyright (c) Kevin Zehrer
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

using KZDev.PrimeTime;
using KZDev.PrimeTime.Testing.Examples.Infrastructure;

using NodaTime;

namespace KZDev.PrimeTime.Testing.Examples;

/// <summary>
/// Demonstrates <see cref="IPrimeTestClock"/> virtual control APIs used in deterministic tests.
/// </summary>
public sealed class UsingPrimeTestClockControlExamples
{
    #region Snippet
    /// <summary>
    /// Verifies <see cref="IPrimeTestClock.SetInstant"/> moves the virtual UTC timeline used by
    /// <see cref="IPrimeClock.NowInstant"/>.
    /// </summary>
    [Fact]
    public void TestClock_SetInstant_Advance_UpdatesNowInstant ()
    {
        Instant start = Instant.FromUtc(2025, 1, 1, 12, 0, 0);
        IPrimeTestClock clock = new PrimeTestClock(start);
        clock.SetInstant(Instant.FromUtc(2025, 6, 1, 8, 0, 0));
        clock.Advance(Duration.FromHours(3));
        clock.NowInstant.Should().Be(Instant.FromUtc(2025, 6, 1, 11, 0, 0));
    }

    /// <summary>
    /// Verifies <see cref="IPrimeTestClock.SetLocalTime"/> resolves wall-clock times in the clock's zone,
    /// including lenient handling when the wall clock falls in a spring-forward gap.
    /// </summary>
    [Fact]
    public void TestClock_SetLocalTime_LenientSpringGap_ResolvesToValidZonedTime ()
    {
        DateTimeZone eastern = NodaDstScenarioFixture.UsEastern;
        LocalDateTime gapWall = new LocalDateTime(2024, 3, 10, 2, 30);
        ZonedDateTime lenient = eastern.AtLeniently(gapWall);
        IPrimeTestClock clock = new PrimeTestClock(lenient.ToInstant(), eastern);
        clock.SetLocalTime(gapWall);
        clock.LocalZonedNowInstant.LocalDateTime.Should().Be(lenient.LocalDateTime);
    }

    /// <summary>
    /// Verifies <see cref="IPrimeTestClock.RunFor"/> starts a bounded automatic run and
    /// returns immediately, with the caller waiting for completion by observing
    /// <see cref="IPrimeTestClock.ClockEvents"/> for <see cref="PrimeTestClockEventType.ClockStopped"/>.
    /// </summary>
    [Fact]
    public void TestClock_RunFor_Duration_WaitsForBoundedCompletion ()
    {
        Instant start = Instant.FromUtc(2025, 2, 1, 0, 0, 0);
        IPrimeTestClock clock = new PrimeTestClock(start);
        Duration step = Duration.FromMinutes(40);
        using ManualResetEventSlim stoppedSignal = new(initialState: false);
        clock.ClockEvents += OnClockEvent;
        try
        {
            bool started = clock.RunFor(step, Duration.FromHours(1));
            started.Should().BeTrue();
            bool stopped = stoppedSignal.Wait(TimeSpan.FromSeconds(5));
            stopped.Should().BeTrue();
        }
        finally
        {
            clock.ClockEvents -= OnClockEvent;
        }

        clock.NowInstant.Should().Be(start + step);

        void OnClockEvent (object? _, PrimeTestClockEvent clockEvent)
        {
            if (clockEvent.EventType == PrimeTestClockEventType.ClockStopped)
                stoppedSignal.Set();
        }
    }

    /// <summary>
    /// Verifies <see cref="IPrimeTestClock.Start(NodaTime.Duration?)"/> toggles
    /// <see cref="IPrimeTestTime.IsRunning"/> and <see cref="IPrimeTestClock.Stop"/> stops automatic advancement.
    /// </summary>
    [Fact]
    public void TestClock_StartStop_TogglesIsRunning ()
    {
        IPrimeTestClock clock = new PrimeTestClock(Instant.FromUtc(2025, 1, 1, 0, 0, 0));
        clock.IsRunning.Should().BeFalse();
        clock.Start(Duration.FromSeconds(1));
        clock.IsRunning.Should().BeTrue();
        clock.Stop().Should().BeTrue();
        clock.IsRunning.Should().BeFalse();
    }
    #endregion Snippet
}
