// Copyright (c) Kevin Zehrer
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

using NodaTime;
// ReSharper disable AccessToDisposedClosure

namespace KZDev.PrimeTime.UnitTests;

//################################################################################
/// <content>
///   <see cref="UsingPrimeTimeEventSource"/> day-time async callback ETW coverage (Noda
///   <see cref="PrimeClock"/>).
/// </content>
public sealed partial class UsingPrimeTimeEventSource
{
    //----------------------------------------------------------------------------
    /// <summary>
    ///   Verifies that an async local day-time callback that faults after yielding emits the ETW fault event.
    /// </summary>
    /// <remarks>
    ///   <see cref="System.Diagnostics.Tracing.EventListener"/> is process-wide; the exception detail filter must be
    ///   unique to this invocation so parallel tests do not accumulate unrelated matches on the same counter.
    ///   Production logs once per fault; the listener may deliver the same event more than once, so the observed
    ///   count is asserted in a narrow range rather than exactly one.
    /// </remarks>
    [Fact]
    public void DayTimeTimer_AsyncCallbackFaultsAsync_RecordsClockTimerCallbackExceptionEvent ()
    {
        string exceptionMarker =
            $"DayTimeTimer_AsyncCallbackFaultsAsync_RecordsClockTimerCallbackExceptionEvent PrimeTime {Guid.NewGuid():N}";
        using ManualResetEventSlim callbackEntered = new(false);
        using PrimeTimeTestEventListener listener = new("KZDev.PrimeTime", "DayTime",
            clockTimerCallbackExceptionDetailContains: exceptionMarker);
        int baseline = Volatile.Read(ref listener.ClockTimerCallbackExceptionCount);
        IPrimeClock clock = new PrimeClock();
        LocalTime targetTime = clock.LocalNowTime.PlusSeconds(10);
        using (IClockDayTimeTimer registration = clock.RegisterAsyncTimeOfDay(targetTime,
                   async (_, _) =>
                   {
                       callbackEntered.Set();
                       await Task.Yield();
                       throw new InvalidOperationException(exceptionMarker);
                   },
                   TestContext.Current.CancellationToken))
        {
            callbackEntered.Wait(TimeSpan.FromSeconds(45), TestContext.Current.CancellationToken).Should().BeTrue();
            SpinWait.SpinUntil(() => Volatile.Read(ref listener.ClockTimerCallbackExceptionCount) > baseline,
                TimeSpan.FromSeconds(30)).Should().BeTrue();
            int observedCount = Volatile.Read(ref listener.ClockTimerCallbackExceptionCount) - baseline;
            observedCount.Should().BeInRange(1, 2,
                "production should log the fault at least once; EventListener may duplicate delivery, but runaway counts indicate cross-test leakage or extra emissions");
        }
    }
    //----------------------------------------------------------------------------
}
//################################################################################
