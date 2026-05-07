// Copyright (c) Kevin Zehrer
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

#if NET

using System.Threading;
using System.Threading.Tasks;

using AwesomeAssertions;

using KZDev.PrimeTime.Tests;
using KZDev.SystemClock.PrimeTime.Observability;
// ReSharper disable AccessToDisposedClosure

namespace KZDev.SystemClock.PrimeTime.UnitTests;

//################################################################################
/// <content>
///   <see cref="UsingPrimeTimeEventSource"/> tests that require .NET (day-time timer surface on
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
    ///   unique to this invocation so parallel tests (or duplicate deliveries) do not accumulate unrelated matches on
    ///   the same counter.
    /// </remarks>
    [Fact]
    public void DayTimeTimer_AsyncCallbackFaultsAsync_RecordsClockTimerCallbackExceptionEvent ()
    {
        string exceptionMarker =
            $"DayTimeTimer_AsyncCallbackFaultsAsync_RecordsClockTimerCallbackExceptionEvent SystemClock {Guid.NewGuid():N}";
        using ManualResetEventSlim callbackEntered = new(false);
        using PrimeTimeTestEventListener listener = new("KZDev.SystemClock.PrimeTime", "DayTime",
            clockTimerCallbackExceptionDetailContains: exceptionMarker);
        int baseline = Volatile.Read(ref listener.ClockTimerCallbackExceptionCount);
        IPrimeClock clock = new PrimeClock(TimeProvider.System);
        DateTime localFire = clock.LocalNowDateTimeOffset.LocalDateTime.AddSeconds(10);
        LocalTimeOfDay target = new(TimeOnly.FromDateTime(localFire));
        using IClockDayTimeTimer registration = clock.RegisterAsyncTimeOfDay(target,
            async (_, _) =>
            {
                callbackEntered.Set();
                await Task.Yield();
                throw new InvalidOperationException(exceptionMarker);
            },
            TestContext.Current.CancellationToken);
        callbackEntered.Wait(TimeSpan.FromSeconds(45), TestContext.Current.CancellationToken).Should().BeTrue();
        SpinWait.SpinUntil(() => Volatile.Read(ref listener.ClockTimerCallbackExceptionCount) > baseline,
            TimeSpan.FromSeconds(30)).Should().BeTrue();
        (Volatile.Read(ref listener.ClockTimerCallbackExceptionCount) - baseline).Should().BeGreaterThanOrEqualTo(1);
    }
    //----------------------------------------------------------------------------
}
//################################################################################

#endif
