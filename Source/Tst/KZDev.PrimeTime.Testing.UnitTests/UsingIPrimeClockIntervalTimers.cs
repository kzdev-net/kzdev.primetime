// Copyright (c) Kevin Zehrer
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;

using AwesomeAssertions;

using KZDev.PrimeTime.Tests;

using NodaTime;

namespace KZDev.PrimeTime.Testing.UnitTests;

//################################################################################
/// <summary>
///   Testing-surface interval timer regression tests.
/// </summary>
[ExcludeFromCodeCoverage]
public class UsingIPrimeClockIntervalTimers : UnitTestBase
{
    #region Constructors/Finalizers

    //----------------------------------------------------------------------------
    /// <summary>
    ///   Initializes a new instance of the <see cref="UsingIPrimeClockIntervalTimers"/> class.
    /// </summary>
    /// <param name="xUnitTestOutputHelper">
    ///   The xUnit test output helper for diagnostic output.
    /// </param>
    public UsingIPrimeClockIntervalTimers (ITestOutputHelper xUnitTestOutputHelper)
        : base(xUnitTestOutputHelper)
    {
    }
    //----------------------------------------------------------------------------

    #endregion Constructors/Finalizers

    #region Regression (repeat spacing after first virtual tick)

    /// <summary>
    ///   Regression (virtual clock): after the first interval callback, <see cref="IClockIntervalTimer.TimeUntilNextCallback"/>
    ///   must reflect the configured repeat interval so repeat spacing cannot silently regress.
    /// </summary>
    [Fact]
    public void RegisterTimer_Repeating_AfterFirstVirtualTick_TimeUntilNextReflectsRepeat ()
    {
        IPrimeTestClock clock = new PrimeTestClock(Instant.FromUtc(2025, 6, 15, 10, 0, 0));
        TimeSpan repeat = TimeSpan.FromHours(2);
        using IClockIntervalTimer timer = clock.RegisterTimer(TimeSpan.FromHours(24), repeat, () => { },
            CancellationToken.None);
        clock.Advance(Duration.FromHours(24));
        long msUntilNext = timer.TimeUntilNextCallback;
        long minExpected = (long)TimeSpan.FromHours(2).Subtract(TimeSpan.FromMinutes(5)).TotalMilliseconds;
        long maxExpected = (long)TimeSpan.FromHours(2).Add(TimeSpan.FromMinutes(5)).TotalMilliseconds;
        msUntilNext.Should().BeInRange(minExpected, maxExpected);
    }

    #endregion Regression (repeat spacing after first virtual tick)
}
//################################################################################
