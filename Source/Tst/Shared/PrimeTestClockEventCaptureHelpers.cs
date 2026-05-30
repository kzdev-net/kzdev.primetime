// Copyright (c) Kevin Zehrer
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

using AwesomeAssertions;

#if SYSTEMCLOCK
using KZDev.SystemClock.PrimeTime.Testing;
// ReSharper disable ChangeFieldTypeToSystemThreadingLock
#else
using KZDev.PrimeTime.Testing;

using NodaTime;
// ReSharper disable ChangeFieldTypeToSystemThreadingLock
#endif

namespace KZDev.PrimeTime.Tests;

//################################################################################
/// <summary>
///   Subscribes to <see cref="IPrimeTestClock.ClockEvents"/> and collects every raised event type.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed class ClockEventTypeCollector : IDisposable
{
    //----------------------------------------------------------------------------
    private readonly IPrimeTestClock _clock;
    private readonly PrimeTestClockEventHandler _handler;
    private readonly object _sync = new();
    private bool _disposed;
    private readonly List<PrimeTestClockEventType> _eventTypes = [];
    //----------------------------------------------------------------------------

    //----------------------------------------------------------------------------
    /// <summary>
    ///   Subscribes to <paramref name="clock"/> and records <see cref="PrimeTestClockEvent.EventType"/> values.
    /// </summary>
    /// <param name="clock">The test clock whose events are collected.</param>
    public ClockEventTypeCollector (IPrimeTestClock clock)
    {
        _clock = clock;
        _handler = OnClockEvent;
        _clock.ClockEvents += _handler;
    }
    //----------------------------------------------------------------------------

    //----------------------------------------------------------------------------
    /// <summary>
    ///   Returns a snapshot of event types collected so far.
    /// </summary>
    /// <returns>A copy of recorded event types in raise order.</returns>
    public List<PrimeTestClockEventType> Snapshot ()
    {
        lock (_sync)
        {
            return [.. _eventTypes];
        }
    }
    //----------------------------------------------------------------------------

    //----------------------------------------------------------------------------
    /// <summary>
    ///   Returns whether any recorded event type equals <paramref name="eventType"/>.
    /// </summary>
    /// <param name="eventType">The event type to locate.</param>
    /// <returns>
    ///   <see langword="true"/> when <paramref name="eventType"/> has been recorded; otherwise <see langword="false"/>.
    /// </returns>
    public bool ContainsEventType (PrimeTestClockEventType eventType)
    {
        lock (_sync)
        {
            return _eventTypes.Contains(eventType);
        }
    }
    //----------------------------------------------------------------------------

    #region IDisposable Implementation

    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public void Dispose ()
    {
        lock (_sync)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
        }

        _clock.ClockEvents -= _handler;
    }
    //----------------------------------------------------------------------------

    #endregion IDisposable Implementation

    //----------------------------------------------------------------------------
    private void OnClockEvent (object? sender, PrimeTestClockEvent e)
    {
        lock (_sync)
        {
            if (_disposed)
            {
                return;
            }

            _eventTypes.Add(e.EventType);
        }
    }
    //----------------------------------------------------------------------------
}
//################################################################################

//################################################################################
/// <summary>
///   Subscribes to <see cref="IPrimeTestClock.ClockEvents"/> and collects every raised event.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed class ClockEventCollector : IDisposable
{
    //----------------------------------------------------------------------------
    private readonly IPrimeTestClock _clock;
    private readonly PrimeTestClockEventHandler _handler;
    private readonly object _sync = new();
    private readonly SemaphoreSlim _eventSignal = new(0, 1);
    private bool _disposed;
    private readonly List<PrimeTestClockEvent> _received = [];
    //----------------------------------------------------------------------------

    //----------------------------------------------------------------------------
    /// <summary>
    ///   Subscribes to <paramref name="clock"/> and records all raised events.
    /// </summary>
    /// <param name="clock">The test clock whose events are collected.</param>
    public ClockEventCollector (IPrimeTestClock clock)
    {
        _clock = clock;
        _handler = OnClockEvent;
        _clock.ClockEvents += _handler;
    }
    //----------------------------------------------------------------------------

    //----------------------------------------------------------------------------
    /// <summary>
    ///   Clears events collected so far.
    /// </summary>
    public void Clear ()
    {
        lock (_sync)
        {
            _received.Clear();
        }
    }
    //----------------------------------------------------------------------------

    //----------------------------------------------------------------------------
    /// <summary>
    ///   Returns a snapshot of events collected so far.
    /// </summary>
    /// <returns>A copy of recorded events in raise order.</returns>
    public List<PrimeTestClockEvent> Snapshot ()
    {
        lock (_sync)
        {
            return [.. _received];
        }
    }
    //----------------------------------------------------------------------------

    //----------------------------------------------------------------------------
    /// <summary>
    ///   Returns whether any recorded event has type <paramref name="eventType"/>.
    /// </summary>
    /// <param name="eventType">The event type to locate.</param>
    /// <returns>
    ///   <see langword="true"/> when an event of <paramref name="eventType"/> has been recorded; otherwise
    ///   <see langword="false"/>.
    /// </returns>
    public bool ContainsEventType (PrimeTestClockEventType eventType)
    {
        lock (_sync)
        {
            foreach (PrimeTestClockEvent received in _received)
            {
                if (received.EventType == eventType)
                {
                    return true;
                }
            }

            return false;
        }
    }
    //----------------------------------------------------------------------------

    //----------------------------------------------------------------------------
    /// <summary>
    ///   Waits until any event arrives or the timeout elapses.
    /// </summary>
    /// <param name="timeout">Maximum real time to wait.</param>
    /// <param name="cancellationToken">Cancellation token for the wait.</param>
    /// <returns>
    ///   <see langword="true"/> when an event arrives before timeout; otherwise <see langword="false"/>.
    /// </returns>
    public bool WaitForEvent (TimeSpan timeout, CancellationToken cancellationToken)
    {
        try
        {
            return _eventSignal.Wait(timeout, cancellationToken);
        }
        catch (ObjectDisposedException)
        {
            // Collector disposal can race with in-flight waits during teardown.
            return false;
        }
    }
    //----------------------------------------------------------------------------

    #region IDisposable Implementation

    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public void Dispose ()
    {
        lock (_sync)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
        }

        _clock.ClockEvents -= _handler;
        _eventSignal.Dispose();
    }
    //----------------------------------------------------------------------------

    #endregion IDisposable Implementation

    //----------------------------------------------------------------------------
    private void OnClockEvent (object? sender, PrimeTestClockEvent e)
    {
        lock (_sync)
        {
            if (_disposed)
            {
                return;
            }

            _received.Add(e);
        }

        try
        {
            _eventSignal.Release();
        }
        catch (SemaphoreFullException)
        {
            // Keep event signaling bounded to "at least one event arrived".
        }
        catch (ObjectDisposedException)
        {
            // Dispose can race with in-flight event handlers during teardown.
        }
    }
    //----------------------------------------------------------------------------
}
//################################################################################

//################################################################################
/// <summary>
///   Subscribes to <see cref="IPrimeTestClock.ClockEvents"/> and collects all events of
///   <typeparamref name="TEvent"/>.
/// </summary>
/// <typeparam name="TEvent">Derived <see cref="PrimeTestClockEvent"/> type to capture.</typeparam>
[ExcludeFromCodeCoverage]
public sealed class ClockEventListCapture<TEvent> : IDisposable where TEvent : PrimeTestClockEvent
{
    //----------------------------------------------------------------------------
    private readonly IPrimeTestClock _clock;
    private readonly PrimeTestClockEventHandler _handler;
    private readonly object _sync = new();
    private bool _disposed;
    private readonly List<TEvent> _received = [];
    //----------------------------------------------------------------------------

    //----------------------------------------------------------------------------
    /// <summary>
    ///   Subscribes to <paramref name="clock"/> and records events assignable to <typeparamref name="TEvent"/>.
    /// </summary>
    /// <param name="clock">The test clock whose events are collected.</param>
    public ClockEventListCapture (IPrimeTestClock clock)
    {
        _clock = clock;
        _handler = OnClockEvent;
        _clock.ClockEvents += _handler;
    }
    //----------------------------------------------------------------------------

    //----------------------------------------------------------------------------
    /// <summary>
    ///   Returns a snapshot of matching events collected so far.
    /// </summary>
    /// <returns>A copy of recorded events in raise order.</returns>
    public List<TEvent> Snapshot ()
    {
        lock (_sync)
        {
            return [.. _received];
        }
    }
    //----------------------------------------------------------------------------

    //----------------------------------------------------------------------------
    /// <summary>
    ///   Returns whether any recorded event satisfies <paramref name="predicate"/>.
    /// </summary>
    /// <param name="predicate">Condition evaluated for each recorded event.</param>
    /// <returns>
    ///   <see langword="true"/> when at least one recorded event matches; otherwise <see langword="false"/>.
    /// </returns>
    public bool Any (Func<TEvent, bool> predicate)
    {
        lock (_sync)
        {
            foreach (TEvent received in _received)
            {
                if (predicate(received))
                {
                    return true;
                }
            }

            return false;
        }
    }
    //----------------------------------------------------------------------------

    #region IDisposable Implementation

    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public void Dispose ()
    {
        lock (_sync)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
        }

        _clock.ClockEvents -= _handler;
    }
    //----------------------------------------------------------------------------

    #endregion IDisposable Implementation

    //----------------------------------------------------------------------------
    private void OnClockEvent (object? sender, PrimeTestClockEvent e)
    {
        if (e is not TEvent typed)
        {
            return;
        }

        lock (_sync)
        {
            if (_disposed)
            {
                return;
            }

            _received.Add(typed);
        }
    }
    //----------------------------------------------------------------------------
}
//################################################################################

//################################################################################
/// <summary>
///   Shared wait and assertion helpers for bounded <see cref="IPrimeTestClock.RunFor"/> lifecycle tests.
/// </summary>
[ExcludeFromCodeCoverage]
public static class PrimeTestClockBoundedRunAssertionHelpers
{
    private const string DefaultClockStoppedWaitMessage =
        "Timed out waiting for ClockStopped after bounded run completion.";
    //----------------------------------------------------------------------------

    //----------------------------------------------------------------------------
    /// <summary>
    ///   Waits for <see cref="PrimeTestClockEventType.ClockStopped"/> in <paramref name="collector"/>, then asserts
    ///   the clock is stopped at <paramref name="expectedCommittedUtc"/> with exactly one
    ///   <see cref="PrimeTestClockEventType.ClockStopped"/> at <paramref name="expectedRunForStopUtc"/>.
    /// </summary>
    /// <param name="clock">The test clock under assertion.</param>
    /// <param name="collector">Event collector subscribed to <paramref name="clock"/>.</param>
    /// <param name="expectedCommittedUtc">Expected committed virtual UTC after the bounded run completes.</param>
    /// <param name="expectedRunForStopUtc">Expected virtual UTC instant on the single <c>ClockStopped</c> event.</param>
    /// <param name="waitTimeout">Maximum real time to wait for <c>ClockStopped</c>.</param>
    /// <param name="cancellationToken">Cancellation token for the test run.</param>
    /// <param name="clockStoppedWaitMessage">Message for the thrown <see cref="TimeoutException"/>.</param>
    public static void AssertBoundedRunCompletedWithSingleClockStoppedAtHorizon (
        IPrimeTestClock clock,
        ClockEventCollector collector,
        DateTimeOffset expectedCommittedUtc,
        DateTimeOffset expectedRunForStopUtc,
        TimeSpan waitTimeout,
        CancellationToken cancellationToken,
        string clockStoppedWaitMessage = DefaultClockStoppedWaitMessage)
    {
        WaitUntilCollectorContainsEventType(
            collector,
            PrimeTestClockEventType.ClockStopped,
            waitTimeout,
            cancellationToken,
            clockStoppedWaitMessage);
        clock.IsRunning.Should().BeFalse();
        clock.UtcNowDateTimeOffset.Should().Be(expectedCommittedUtc);
        List<PrimeTestClockEvent> snapshot = collector.Snapshot();
        snapshot.Count(e => e.EventType == PrimeTestClockEventType.ClockStopped).Should().Be(1);
        PrimeTestClockStoppedEvent stopped = snapshot
            .OfType<PrimeTestClockStoppedEvent>()
            .Should()
            .ContainSingle()
            .Subject;
        stopped.ClockTime.Should().Be(expectedRunForStopUtc);
    }
    //----------------------------------------------------------------------------

#if !SYSTEMCLOCK
    //----------------------------------------------------------------------------
    /// <summary>
    ///   Waits for <see cref="PrimeTestClockEventType.ClockStopped"/> in <paramref name="collector"/>, then asserts
    ///   the clock is stopped at <paramref name="expectedCommittedInstant"/> with exactly one
    ///   <see cref="PrimeTestClockEventType.ClockStopped"/> at <paramref name="expectedRunForStopInstant"/>.
    /// </summary>
    /// <param name="clock">The test clock under assertion.</param>
    /// <param name="collector">Event collector subscribed to <paramref name="clock"/>.</param>
    /// <param name="expectedCommittedInstant">Expected committed virtual instant after the bounded run completes.</param>
    /// <param name="expectedRunForStopInstant">Expected instant on the single <c>ClockStopped</c> event.</param>
    /// <param name="waitTimeout">Maximum real time to wait for <c>ClockStopped</c>.</param>
    /// <param name="cancellationToken">Cancellation token for the test run.</param>
    /// <param name="clockStoppedWaitMessage">Message for the thrown <see cref="TimeoutException"/>.</param>
    public static void AssertBoundedRunCompletedWithSingleClockStoppedAtHorizon (
        IPrimeTestClock clock,
        ClockEventCollector collector,
        Instant expectedCommittedInstant,
        Instant expectedRunForStopInstant,
        TimeSpan waitTimeout,
        CancellationToken cancellationToken,
        string clockStoppedWaitMessage = DefaultClockStoppedWaitMessage)
    {
        WaitUntilCollectorContainsEventType(
            collector,
            PrimeTestClockEventType.ClockStopped,
            waitTimeout,
            cancellationToken,
            clockStoppedWaitMessage);
        clock.IsRunning.Should().BeFalse();
        clock.NowInstant.Should().Be(expectedCommittedInstant);
        List<PrimeTestClockEvent> snapshot = collector.Snapshot();
        snapshot.Count(e => e.EventType == PrimeTestClockEventType.ClockStopped).Should().Be(1);
        PrimeTestClockStoppedEvent stopped = snapshot
            .OfType<PrimeTestClockStoppedEvent>()
            .Should()
            .ContainSingle()
            .Subject;
        stopped.ClockInstant.Should().Be(expectedRunForStopInstant);
    }
    //----------------------------------------------------------------------------
#endif

    //----------------------------------------------------------------------------
    /// <summary>
    ///   Polls until <paramref name="collector"/> records an event of <paramref name="eventType"/> or the timeout
    ///   elapses.
    /// </summary>
    /// <param name="collector">Event collector to inspect.</param>
    /// <param name="eventType">Required event type.</param>
    /// <param name="timeout">Maximum real time to wait.</param>
    /// <param name="cancellationToken">Cancellation token for the test run.</param>
    /// <param name="timeoutMessage">Message for the thrown <see cref="TimeoutException"/>.</param>
    /// <exception cref="TimeoutException">
    ///   Thrown when <paramref name="eventType"/> is not recorded before the timeout elapses.
    /// </exception>
    private static void WaitUntilCollectorContainsEventType (ClockEventCollector collector,
        PrimeTestClockEventType eventType, TimeSpan timeout, CancellationToken cancellationToken,
        string timeoutMessage)
    {
        Stopwatch elapsed = Stopwatch.StartNew();
        while (!collector.ContainsEventType(eventType))
        {
            cancellationToken.ThrowIfCancellationRequested();
            TimeSpan remaining = timeout - elapsed.Elapsed;
            if (remaining <= TimeSpan.Zero)
            {
                throw new TimeoutException(timeoutMessage);
            }
            if (!collector.WaitForEvent(remaining, cancellationToken))
            {
                throw new TimeoutException(timeoutMessage);
            }
        }
    }
    //----------------------------------------------------------------------------
}
//################################################################################
