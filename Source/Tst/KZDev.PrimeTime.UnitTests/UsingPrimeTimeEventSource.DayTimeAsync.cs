// Copyright (c) Kevin Zehrer
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Threading;
using System.Threading.Tasks;

using AwesomeAssertions;
using KZDev.PrimeTime.Observability;
using KZDev.PrimeTime.Tests;

using NodaTime;

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
    [Fact]
    public void DayTimeTimer_AsyncCallbackFaultsAsync_RecordsClockTimerCallbackExceptionEvent ()
    {
        using ManualResetEventSlim callbackEntered = new(false);
        using PrimeTimeTestEventListener listener = new("KZDev.PrimeTime", "DayTime");
        IPrimeClock clock = new PrimeClock();
        LocalTime targetTime = clock.LocalNowTime.PlusSeconds(10);
        using (IClockDayTimeTimer registration = clock.RegisterAsyncTimeOfDay(targetTime,
                   async (_, _) =>
                   {
                       callbackEntered.Set();
                       await Task.Yield();
                       throw new InvalidOperationException("async day-time fault");
                   },
                   TestContext.Current.CancellationToken))
        {
            callbackEntered.Wait(TimeSpan.FromSeconds(45), TestContext.Current.CancellationToken).Should().BeTrue();
            SpinWait.SpinUntil(() => listener.ClockTimerCallbackExceptionCount > 0, TimeSpan.FromSeconds(30)).Should().BeTrue();
            listener.ClockTimerCallbackExceptionCount.Should().Be(1);
        }
    }
    //----------------------------------------------------------------------------
}
//################################################################################
