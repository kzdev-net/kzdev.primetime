// Copyright (c) Kevin Zehrer
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;

#if SYSTEMCLOCK
using KZDev.SystemClock.PrimeTime.Testing;
#else
using KZDev.PrimeTime.Testing;
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

            _received.Add(e);
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
