// Copyright (c) Kevin Zehrer
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;

using AwesomeAssertions;

using KZDev.PrimeTime.Tests;

namespace KZDev.SystemClock.PrimeTime.UnitTests;

//################################################################################
/// <summary>
///   Unit tests for public convenience overloads in <see cref="PrimeClockTimerExtensions"/>.
/// </summary>
[ExcludeFromCodeCoverage]
public class UsingPrimeClockTimerExtensions : UnitTestBase
{
    #region Constructors/Finalizers

    /// <summary>
    ///   Initializes a new instance of the <see cref="UsingPrimeClockTimerExtensions"/> class.
    /// </summary>
    /// <param name="xUnitTestOutputHelper">
    ///   The xUnit test output helper that can be used to output test messages.
    /// </param>
    public UsingPrimeClockTimerExtensions (ITestOutputHelper xUnitTestOutputHelper)
        : base(xUnitTestOutputHelper)
    {
    }

    #endregion Constructors/Finalizers

    /// <summary>
    ///   Verifies that the repeating convenience overload maps <c>repeat</c> to a repeating interval.
    /// </summary>
    [Fact]
    public void RegisterTimer_ActionRepeatTrue_FiresOnEachInterval ()
    {
        IPrimeTestClock clock = new PrimeTestClock(new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero));
        int fired = 0;

        using IClockIntervalTimer timer = clock.RegisterTimer(TimeSpan.FromMinutes(5), () => fired++, CancellationToken.None, repeat: true);

        clock.Advance(TimeSpan.FromMinutes(4));
        fired.Should().Be(0);
        clock.Advance(TimeSpan.FromMinutes(1));
        fired.Should().Be(1);
        clock.Advance(TimeSpan.FromMinutes(5));
        fired.Should().Be(2);
        timer.IsRepeating.Should().BeTrue();
    }

    /// <summary>
    ///   Verifies that the context/token overload forwards callback state, registration, and token.
    /// </summary>
    [Fact]
    public void RegisterTimer_ContextAndTokenOverload_ForwardsStateRegistrationAndToken ()
    {
        IPrimeTestClock clock = new PrimeTestClock(new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero));
        object state = new();
        object? receivedState = null;
        IClockIntervalTimer? receivedTimer = null;
        using CancellationTokenSource cts = new();
        CancellationToken? receivedToken = null;

        using IClockIntervalTimer timer = clock.RegisterTimer(TimeSpan.FromMinutes(5), (context, ct) =>
        {
            receivedState = context.CallbackState;
            receivedTimer = (IClockIntervalTimer)context.Registration;
            receivedToken = ct;
        }, cts.Token, state);

        clock.Advance(TimeSpan.FromMinutes(5));

        receivedState.Should().BeSameAs(state);
        receivedTimer.Should().BeSameAs(timer);
        receivedToken.Should().NotBeNull();
        receivedToken!.Value.Should().Be(cts.Token);
    }

    /// <summary>
    ///   Verifies that the token-only async overload forwards the registration token and repeats.
    /// </summary>
    [Fact]
    public void RegisterAsyncTimer_TokenOnlyRepeatTrue_ForwardsCancellationTokenAndRepeats ()
    {
        IPrimeTestClock clock = new PrimeTestClock(new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero));
        using CancellationTokenSource cts = new();
        CancellationToken? receivedToken = null;
        int fired = 0;

        using IClockIntervalTimer timer = clock.RegisterAsyncTimer(TimeSpan.FromMinutes(5), ct =>
        {
            receivedToken = ct;
            fired++;
            return default;
        }, cts.Token, repeat: true);

        clock.Advance(TimeSpan.FromMinutes(5));
        clock.Advance(TimeSpan.FromMinutes(5));

        receivedToken.Should().NotBeNull();
        receivedToken!.Value.Should().Be(cts.Token);
        fired.Should().Be(2);
        timer.IsRepeating.Should().BeTrue();
    }

    /// <summary>
    ///   Verifies that the explicit-repeat convenience overload preserves distinct initial and repeat intervals.
    /// </summary>
    [Fact]
    public void RegisterTimer_ExplicitRepeatIntervalActionOverload_UsesSeparateRepeatInterval ()
    {
        IPrimeTestClock clock = new PrimeTestClock(new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero));
        int fired = 0;

        using IClockIntervalTimer timer = clock.RegisterTimer(TimeSpan.FromMinutes(2), TimeSpan.FromMinutes(3), () => fired++,
            CancellationToken.None);

        clock.Advance(TimeSpan.FromMinutes(2));
        fired.Should().Be(1);
        clock.Advance(TimeSpan.FromMinutes(2));
        fired.Should().Be(1);
        clock.Advance(TimeSpan.FromMinutes(1));
        fired.Should().Be(2);
        timer.IsRepeating.Should().BeTrue();
    }

#if NET
    /// <summary>
    ///   Verifies that the local-time action convenience overload fires at the requested local time.
    /// </summary>
    [Fact]
    public void RegisterTimeOfDay_LocalActionOverload_FiresAtRequestedLocalTime ()
    {
        IPrimeTestClock clock = new PrimeTestClock(new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero));
        LocalTimeOfDay target = new(TimeOnly.FromDateTime(clock.LocalNowOffset.DateTime.AddMinutes(10)));
        int fired = 0;

        using IClockDayTimeTimer timer = clock.RegisterTimeOfDay(target, () => fired++, CancellationToken.None);

        clock.Advance(TimeSpan.FromMinutes(9));
        fired.Should().Be(0);
        clock.Advance(TimeSpan.FromMinutes(1));
        fired.Should().Be(1);
        timer.IsLocalTimeRepresentation.Should().BeTrue();
    }

    /// <summary>
    ///   Verifies that the local-time context/token convenience overload forwards state, registration, and token.
    /// </summary>
    [Fact]
    public void RegisterTimeOfDay_LocalContextAndTokenOverload_ForwardsStateRegistrationAndToken ()
    {
        IPrimeTestClock clock = new PrimeTestClock(new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero));
        LocalTimeOfDay target = new(TimeOnly.FromDateTime(clock.LocalNowOffset.DateTime.AddMinutes(10)));
        object state = new();
        object? receivedState = null;
        IClockDayTimeTimer? receivedTimer = null;
        using CancellationTokenSource cts = new();
        CancellationToken? receivedToken = null;

        using IClockDayTimeTimer timer = clock.RegisterTimeOfDay(target, (context, ct) =>
        {
            receivedState = context.CallbackState;
            receivedTimer = (IClockDayTimeTimer)context.Registration;
            receivedToken = ct;
        }, cts.Token, state);

        clock.Advance(TimeSpan.FromMinutes(10));

        receivedState.Should().BeSameAs(state);
        receivedTimer.Should().BeSameAs(timer);
        receivedToken.Should().NotBeNull();
        receivedToken!.Value.Should().Be(cts.Token);
    }

    /// <summary>
    ///   Verifies that the local-time async convenience overload forwards the registration token.
    /// </summary>
    [Fact]
    public void RegisterAsyncTimeOfDay_LocalTokenOverload_ForwardsCancellationToken ()
    {
        IPrimeTestClock clock = new PrimeTestClock(new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero));
        LocalTimeOfDay target = new(TimeOnly.FromDateTime(clock.LocalNowOffset.DateTime.AddMinutes(10)));
        using CancellationTokenSource cts = new();
        CancellationToken? receivedToken = null;

        using IClockDayTimeTimer timer = clock.RegisterAsyncTimeOfDay(target, ct =>
        {
            receivedToken = ct;
            return default;
        }, cts.Token);

        clock.Advance(TimeSpan.FromMinutes(10));

        receivedToken.Should().NotBeNull();
        receivedToken!.Value.Should().Be(cts.Token);
        timer.IsLocalTimeRepresentation.Should().BeTrue();
    }

    /// <summary>
    ///   Verifies that the UTC action convenience overload fires at the requested UTC time.
    /// </summary>
    [Fact]
    public void RegisterTimeOfDay_UtcActionOverload_FiresAtRequestedUtcTime ()
    {
        IPrimeTestClock clock = new PrimeTestClock(new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero));
        UtcTimeOfDay target = new(TimeOnly.FromDateTime(clock.UtcNowOffset.UtcDateTime.AddMinutes(10)));
        int fired = 0;

        using IClockDayTimeTimer timer = clock.RegisterTimeOfDay(target, () => fired++, CancellationToken.None);

        clock.Advance(TimeSpan.FromMinutes(9));
        fired.Should().Be(0);
        clock.Advance(TimeSpan.FromMinutes(1));
        fired.Should().Be(1);
        timer.IsLocalTimeRepresentation.Should().BeFalse();
    }

    /// <summary>
    ///   Verifies that the UTC context/token convenience overload forwards state, registration, and token.
    /// </summary>
    [Fact]
    public void RegisterTimeOfDay_UtcContextAndTokenOverload_ForwardsStateRegistrationAndToken ()
    {
        IPrimeTestClock clock = new PrimeTestClock(new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero));
        UtcTimeOfDay target = new(TimeOnly.FromDateTime(clock.UtcNowOffset.UtcDateTime.AddMinutes(10)));
        object state = new();
        object? receivedState = null;
        IClockDayTimeTimer? receivedTimer = null;
        using CancellationTokenSource cts = new();
        CancellationToken? receivedToken = null;

        using IClockDayTimeTimer timer = clock.RegisterTimeOfDay(target, (context, ct) =>
        {
            receivedState = context.CallbackState;
            receivedTimer = (IClockDayTimeTimer)context.Registration;
            receivedToken = ct;
        }, cts.Token, state);

        clock.Advance(TimeSpan.FromMinutes(10));

        receivedState.Should().BeSameAs(state);
        receivedTimer.Should().BeSameAs(timer);
        receivedToken.Should().NotBeNull();
        receivedToken!.Value.Should().Be(cts.Token);
    }

    /// <summary>
    ///   Verifies that the UTC async convenience overload forwards the registration token.
    /// </summary>
    [Fact]
    public void RegisterAsyncTimeOfDay_UtcTokenOverload_ForwardsCancellationToken ()
    {
        IPrimeTestClock clock = new PrimeTestClock(new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero));
        UtcTimeOfDay target = new(TimeOnly.FromDateTime(clock.UtcNowOffset.UtcDateTime.AddMinutes(10)));
        using CancellationTokenSource cts = new();
        CancellationToken? receivedToken = null;

        using IClockDayTimeTimer timer = clock.RegisterAsyncTimeOfDay(target, ct =>
        {
            receivedToken = ct;
            return default;
        }, cts.Token);

        clock.Advance(TimeSpan.FromMinutes(10));

        receivedToken.Should().NotBeNull();
        receivedToken!.Value.Should().Be(cts.Token);
        timer.IsLocalTimeRepresentation.Should().BeFalse();
    }
#endif
}
//################################################################################
