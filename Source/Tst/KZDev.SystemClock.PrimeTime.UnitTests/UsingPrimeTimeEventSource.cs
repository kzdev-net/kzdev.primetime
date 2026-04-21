// Copyright (c) Kevin Zehrer
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Tracing;
using System.Threading;
using System.Threading.Tasks;

using AwesomeAssertions;
using KZDev.PrimeTime.Tests;
using KZDev.SystemClock.PrimeTime.Observability;

using Xunit;

namespace KZDev.SystemClock.PrimeTime.UnitTests;

//################################################################################
/// <summary>
///   Unit tests for <see cref="PrimeTimeEventSource"/> ETW emission on critical paths (SystemClock assembly).
/// </summary>
[ExcludeFromCodeCoverage]
public sealed partial class UsingPrimeTimeEventSource : UnitTestBase
{
    /// <summary>
    ///   Upper bound for waiting on <see cref="System.Threading.Timer"/> / thread-pool timer paths in these tests.
    ///   Parallel test execution with code coverage instrumentation can starve or delay the thread pool enough
    ///   that short timeouts (for example 10 seconds) fail even though behavior is correct.
    /// </summary>
    private static readonly TimeSpan ThreadPoolTimerTestWaitTimeout = TimeSpan.FromSeconds(120);

    #region Nested types

    //----------------------------------------------------------------------------
    /// <summary>
    ///   Enables a PrimeTime <see cref="EventSource"/> provider and counts selected ETW events with optional payload filters.
    /// </summary>
    private sealed class PrimeTimeTestEventListener : EventListener
    {
        private const int EventId_ClockTimerUseAfterDispose = 3;
        private const int EventId_ClockTimerCallbackException = 6;

        private readonly string _providerName;

        private readonly string? _clockTimerCallbackExceptionTimerCategory;

        private readonly string? _clockTimerUseAfterDisposeOperation;

        /// <summary>
        ///   When not <c>null</c>, only <see cref="PrimeTimeEventSource.ClockTimerCallbackException"/> events whose
        ///   third payload argument (exception detail text) contains this substring increment
        ///   <see cref="ClockTimerCallbackExceptionCount"/>. This avoids counting unrelated callbacks when several
        ///   tests in the same process emit the same timer category in parallel.
        /// </summary>
        private readonly string? _clockTimerCallbackExceptionDetailContains;

        /// <summary>
        ///   Number of <see cref="PrimeTimeEventSource.ClockTimerUseAfterDispose"/> events observed.
        /// </summary>
        public int ClockTimerUseAfterDisposeCount;

        /// <summary>
        ///   Number of <c>ClockTimerCallbackException</c> ETW events observed (event id 6).
        /// </summary>
        public int ClockTimerCallbackExceptionCount;

        /// <summary>
        ///   Initializes a new instance of the <see cref="PrimeTimeTestEventListener"/> class.
        /// </summary>
        /// <param name="providerName">The ETW provider name to enable (for example, <c>KZDev.SystemClock.PrimeTime</c>).</param>
        /// <param name="clockTimerCallbackExceptionTimerCategory">
        ///   When not <c>null</c>, only <see cref="PrimeTimeEventSource.ClockTimerCallbackException"/> events whose
        ///   first payload argument matches this value (for example, <c>Interval</c> or <c>DayTime</c>) increment
        ///   <see cref="ClockTimerCallbackExceptionCount"/>. Filtering avoids cross-test interference when the same
        ///   event id is emitted while tests run in parallel.
        /// </param>
        /// <param name="clockTimerUseAfterDisposeOperation">
        ///   When not <c>null</c>, only <see cref="PrimeTimeEventSource.ClockTimerUseAfterDispose"/> events whose
        ///   first payload argument (the logical operation name) matches this value increment
        ///   <see cref="ClockTimerUseAfterDisposeCount"/>. Filtering avoids cross-test interference when parallel
        ///   tests emit use-after-dispose for different operations (for example, <c>Start</c> versus <c>Enabled</c>).
        /// </param>
        /// <param name="clockTimerCallbackExceptionDetailContains">
        ///   When not <c>null</c>, only <see cref="PrimeTimeEventSource.ClockTimerCallbackException"/> events whose
        ///   exception detail payload contains this substring (ordinal) increment <see cref="ClockTimerCallbackExceptionCount"/>.
        /// </param>
        public PrimeTimeTestEventListener (string providerName,
            string? clockTimerCallbackExceptionTimerCategory = null,
            string? clockTimerUseAfterDisposeOperation = null,
            string? clockTimerCallbackExceptionDetailContains = null)
        {
            _providerName = providerName;
            _clockTimerCallbackExceptionTimerCategory = clockTimerCallbackExceptionTimerCategory;
            _clockTimerUseAfterDisposeOperation = clockTimerUseAfterDisposeOperation;
            _clockTimerCallbackExceptionDetailContains = clockTimerCallbackExceptionDetailContains;
            foreach (EventSource existing in EventSource.GetSources())
            {
                if (existing.Name == _providerName)
                {
                    EnableEvents(existing, EventLevel.LogAlways, (EventKeywords)(-1));
                }
            }
        }

        /// <inheritdoc />
        protected override void OnEventSourceCreated (EventSource eventSource)
        {
            if (eventSource.Name == _providerName)
            {
                EnableEvents(eventSource, EventLevel.LogAlways, (EventKeywords)(-1));
            }
        }

        /// <inheritdoc />
        protected override void OnEventWritten (EventWrittenEventArgs eventData)
        {
            if (!string.Equals(eventData.EventSource.Name, _providerName, StringComparison.Ordinal))
            {
                return;
            }

            if (eventData.EventId == EventId_ClockTimerUseAfterDispose)
            {
                if (_clockTimerUseAfterDisposeOperation is not null)
                {
                    if (eventData.Payload is null || eventData.Payload.Count < 1)
                    {
                        return;
                    }

                    if (eventData.Payload[0] is not string operation ||
                        !string.Equals(operation, _clockTimerUseAfterDisposeOperation, StringComparison.Ordinal))
                    {
                        return;
                    }
                }

                Interlocked.Increment(ref ClockTimerUseAfterDisposeCount);
            }
            else if (eventData.EventId == EventId_ClockTimerCallbackException)
            {
                if (_clockTimerCallbackExceptionTimerCategory is not null)
                {
                    if (eventData.Payload is null || eventData.Payload.Count < 1)
                    {
                        return;
                    }

                    if (eventData.Payload[0] is not string category ||
                        !string.Equals(category, _clockTimerCallbackExceptionTimerCategory, StringComparison.Ordinal))
                    {
                        return;
                    }
                }

                if (_clockTimerCallbackExceptionDetailContains is not null)
                {
                    if (eventData.Payload is null || eventData.Payload.Count < 3)
                    {
                        return;
                    }

                    if (eventData.Payload[2] is not string detail ||
                        !detail.Contains(_clockTimerCallbackExceptionDetailContains, StringComparison.Ordinal))
                    {
                        return;
                    }
                }

                Interlocked.Increment(ref ClockTimerCallbackExceptionCount);
            }
        }
    }
    //----------------------------------------------------------------------------

    #endregion Nested types

    #region Constructors/Finalizers

    //----------------------------------------------------------------------------
    /// <summary>
    ///   Initializes a new instance of the <see cref="UsingPrimeTimeEventSource"/> class.
    /// </summary>
    /// <param name="xUnitTestOutputHelper">The xUnit test output helper.</param>
    public UsingPrimeTimeEventSource (ITestOutputHelper xUnitTestOutputHelper)
        : base(xUnitTestOutputHelper)
    {
    }
    //----------------------------------------------------------------------------

    #endregion Constructors/Finalizers

    #region Tests

    //----------------------------------------------------------------------------
    /// <summary>
    ///   Verifies that assigning <see cref="IClockTimer.Enabled"/> after dispose emits the ETW event.
    /// </summary>
    /// <remarks>
    ///   <see cref="EventListener"/> observes the provider process-wide; parallel tests can emit the same
    ///   filtered operation name. Assert on the delta across this test&apos;s disposed <c>Enabled</c> assignment,
    ///   not the absolute count.
    /// </remarks>
    [Fact]
    public void DisposedIntervalTimer_SetEnabled_RecordsClockTimerUseAfterDisposeEvent ()
    {
        using PrimeTimeTestEventListener listener = new("KZDev.SystemClock.PrimeTime",
            clockTimerUseAfterDisposeOperation: nameof(IClockTimer.Enabled));
        IPrimeClock clock = new PrimeClock();
        IClockIntervalTimer registration = clock.RegisterTimer(
            TimeSpan.FromHours(1),
            Timeout.InfiniteTimeSpan,
            static _ => { },
            CancellationToken.None);
        registration.Dispose();
        int useAfterDisposeBefore = Volatile.Read(ref listener.ClockTimerUseAfterDisposeCount);
        Action act = () => registration.Enabled = false;
        act.Should().ThrowExactly<ObjectDisposedException>();
        int useAfterDisposeAfter = Volatile.Read(ref listener.ClockTimerUseAfterDisposeCount);
        (useAfterDisposeAfter - useAfterDisposeBefore).Should().BeGreaterThanOrEqualTo(1);
    }
    //----------------------------------------------------------------------------
    /// <summary>
    ///   Verifies that an interval timer callback that throws emits the ETW fault event.
    /// </summary>
    [Fact]
    public void ClockTimerCallbackException_DirectCall_IsObservedByListener ()
    {
        const string exceptionMarker = "ClockTimerCallbackException_DirectCall_IsObservedByListener probe";
        using PrimeTimeTestEventListener listener = new("KZDev.SystemClock.PrimeTime", "Interval",
            clockTimerCallbackExceptionDetailContains: exceptionMarker);
        PrimeTimeEventSource.Log.ClockTimerCallbackException("Interval", new InvalidOperationException(exceptionMarker));
        listener.ClockTimerCallbackExceptionCount.Should().Be(1);
    }
    //----------------------------------------------------------------------------
    /// <summary>
    ///   Verifies that an interval timer callback that throws emits the ETW fault event.
    /// </summary>
    [Fact]
    public void IntervalTimer_CallbackThrows_RecordsClockTimerCallbackExceptionEvent ()
    {
        const string exceptionMarker = "IntervalTimer_CallbackThrows_RecordsClockTimerCallbackExceptionEvent";
        using ManualResetEventSlim callbackEntered = new(false);
        using PrimeTimeTestEventListener listener = new("KZDev.SystemClock.PrimeTime", "Interval",
            clockTimerCallbackExceptionDetailContains: exceptionMarker);
        IPrimeClock clock = new PrimeClock();
        using (IClockIntervalTimer registration = clock.RegisterTimer(
                   TimeSpan.FromMilliseconds(1),
                   Timeout.InfiniteTimeSpan,
                   _ =>
                   {
                       callbackEntered.Set();
                       throw new InvalidOperationException(exceptionMarker);
                   },
                   TestContext.Current.CancellationToken))
        {
            callbackEntered.Wait(ThreadPoolTimerTestWaitTimeout, TestContext.Current.CancellationToken).Should().BeTrue();
            SpinWait.SpinUntil(() => listener.ClockTimerCallbackExceptionCount > 0, ThreadPoolTimerTestWaitTimeout).Should().BeTrue();
            listener.ClockTimerCallbackExceptionCount.Should().Be(1);
        }
    }
    //----------------------------------------------------------------------------
    /// <summary>
    ///   Verifies that an interval timer async callback that faults after yielding emits the ETW fault event
    ///   (thread-pool continuation path).
    /// </summary>
    [Fact]
    public void IntervalTimer_AsyncCallbackFaultsAsync_RecordsClockTimerCallbackExceptionEvent ()
    {
        const string exceptionMarker = "IntervalTimer_AsyncCallbackFaultsAsync_RecordsClockTimerCallbackExceptionEvent";
        using ManualResetEventSlim callbackEntered = new(false);
        using PrimeTimeTestEventListener listener = new("KZDev.SystemClock.PrimeTime", "Interval",
            clockTimerCallbackExceptionDetailContains: exceptionMarker);
        IPrimeClock clock = new PrimeClock();
        using (IClockIntervalTimer registration = clock.RegisterAsyncTimer(
                   TimeSpan.FromMilliseconds(1),
                   async cancellationToken =>
                   {
                       callbackEntered.Set();
                       await Task.Yield();
                       throw new InvalidOperationException(exceptionMarker);
                   },
                   TestContext.Current.CancellationToken))
        {
            callbackEntered.Wait(ThreadPoolTimerTestWaitTimeout, TestContext.Current.CancellationToken).Should().BeTrue();
            SpinWait.SpinUntil(() => listener.ClockTimerCallbackExceptionCount > 0, ThreadPoolTimerTestWaitTimeout).Should().BeTrue();
            listener.ClockTimerCallbackExceptionCount.Should().Be(1);
        }
    }
    //----------------------------------------------------------------------------

    #endregion Tests
}
