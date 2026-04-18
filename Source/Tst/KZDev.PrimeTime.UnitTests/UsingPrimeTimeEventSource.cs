// Copyright (c) Kevin Zehrer
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Tracing;

using AwesomeAssertions;
using KZDev.PrimeTime.Observability;
using KZDev.PrimeTime.Tests;

namespace KZDev.PrimeTime.UnitTests;

//################################################################################
/// <summary>
///   Unit tests for <see cref="PrimeTimeEventSource"/> ETW emission on critical paths.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed class UsingPrimeTimeEventSource : UnitTestBase
{
    #region Nested types

    //----------------------------------------------------------------------------
    /// <summary>
    ///   Enables a single PrimeTime provider and counts <see cref="PrimeTimeEventSource.ClockTimerUseAfterDispose"/> occurrences.
    /// </summary>
    private sealed class PrimeTimeTestEventListener : EventListener
    {
        private readonly string _providerName;

        /// <summary>
        ///   Number of <c>ClockTimerUseAfterDispose</c> events observed.
        /// </summary>
        public int ClockTimerUseAfterDisposeCount;

        /// <summary>
        ///   Initializes a new instance of the <see cref="PrimeTimeTestEventListener"/> class.
        /// </summary>
        /// <param name="providerName">The ETW provider name to enable (for example, <c>KZDev.PrimeTime</c>).</param>
        public PrimeTimeTestEventListener (string providerName)
        {
            _providerName = providerName;
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
            if (eventData.EventName == "ClockTimerUseAfterDispose")
            {
                Interlocked.Increment(ref ClockTimerUseAfterDisposeCount);
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
    [Fact]
    public void DisposedIntervalTimer_SetEnabled_RecordsClockTimerUseAfterDisposeEvent ()
    {
        using PrimeTimeTestEventListener listener = new("KZDev.PrimeTime");
        IPrimeClock clock = new PrimeClock();
        IClockIntervalTimer registration = clock.RegisterTimer(
            TimeSpan.FromHours(1),
            Timeout.InfiniteTimeSpan,
            static _ => { },
            CancellationToken.None);
        registration.Dispose();
        Action act = () => registration.Enabled = false;
        act.Should().ThrowExactly<ObjectDisposedException>();
        listener.ClockTimerUseAfterDisposeCount.Should().Be(1);
    }
    //----------------------------------------------------------------------------

    #endregion Tests
}
