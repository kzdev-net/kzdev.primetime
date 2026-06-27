// Copyright (c) Kevin Zehrer
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;

using AwesomeAssertions;

using KZDev.PrimeTime.Tests;

namespace KZDev.SystemClock.PrimeTime.Testing.UnitTests;

//################################################################################
/// <summary>
///   Testing-surface interval timer extension coverage.
/// </summary>
[ExcludeFromCodeCoverage]
public class UsingIPrimeClockIntervalTimers : UnitTestBase
{
    //----------------------------------------------------------------------------
    /// <summary>
    ///   Short initial delay used by extension overload tests (milliseconds).
    /// </summary>
    private static readonly TimeSpan ShortDelay = TimeSpan.FromMilliseconds(80);
    /// <summary>
    ///   Extra wait time beyond expected delay to avoid flaky failures.
    /// </summary>
    private static readonly TimeSpan WaitMargin = TimeSpan.FromMilliseconds(400);

    /// <summary>
    ///   Wall-clock wait budget for a one-shot callback registered with
    ///   <see cref="ShortDelay"/>: <see cref="ShortDelay"/> + <see cref="WaitMargin"/>.
    /// </summary>
    private static readonly TimeSpan ShortDelayCallbackWaitTimeout = ShortDelay + WaitMargin;

    /// <summary>
    ///   Allowed tolerance when asserting callback timing.
    /// </summary>
    private static readonly TimeSpan TimingTolerance = TimeSpan.FromMilliseconds(150);
    //----------------------------------------------------------------------------

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

    /// <summary>
    ///   Verifies the <see cref="PrimeClockTimerExtensions.RegisterTimer(IPrimeClock, TimeSpan, Action{ClockTimerCallbackContext, CancellationToken}, CancellationToken, object?, bool, IntervalTimerOptions?)"/>
    ///   overload passes the registration <see cref="CancellationToken"/> through to the callback.
    /// </summary>
    [Fact]
    public void RegisterTimer_OneShot_ExtensionWithContextAndToken_CallbackReceivesRegistrationCancellationToken ()
    {
        IPrimeClock clock = new PrimeClock();
        ManualResetEventSlim signal = new(false);
        using CancellationTokenSource cts = new();
        CancellationToken? receivedToken = null;
        DateTimeOffset? firedAt = null;
        using IClockIntervalTimer timer = clock.RegisterTimer(ShortDelay,
            (ClockTimerCallbackContext _, CancellationToken ct) =>
            {
                receivedToken = ct;
                firedAt = clock.UtcNowDateTimeOffset;
                signal.Set();
            },
            cts.Token);
        DateTimeOffset start = clock.UtcNowDateTimeOffset;
        signal.Wait(ShortDelayCallbackWaitTimeout, TestContext.Current.CancellationToken).Should().BeTrue();
        firedAt.Should().NotBeNull();
        (firedAt!.Value - start).Should().BeGreaterThanOrEqualTo(ShortDelay - TimingTolerance);
        receivedToken.Should().NotBeNull();
        receivedToken!.Value.Should().Be(cts.Token);
    }
    //----------------------------------------------------------------------------
}
//################################################################################
