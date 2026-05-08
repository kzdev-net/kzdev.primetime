// Copyright (c) Kevin Zehrer
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;

using AwesomeAssertions;

using KZDev.PrimeTime.Tests;

using NodaTime;

namespace KZDev.PrimeTime.Testing.UnitTests;

//################################################################################
/// <summary>
///   Unit tests for public convenience overloads in <see cref="PrimeClockTimerExtensions"/> on
///   <see cref="KZDev.PrimeTime.IPrimeClock"/> (same shared source as SystemClock; coverage is per-assembly).
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
        IPrimeTestClock clock = new PrimeTestClock(Instant.FromUtc(2025, 1, 1, 0, 0, 0));
        int fired = 0;

        using IClockIntervalTimer timer = clock.RegisterTimer(TimeSpan.FromMinutes(5), () => fired++, CancellationToken.None, repeat: true);

        clock.Advance(Duration.FromMinutes(4));
        fired.Should().Be(0);
        clock.Advance(Duration.FromMinutes(1));
        fired.Should().Be(1);
        clock.Advance(Duration.FromMinutes(5));
        fired.Should().Be(2);
        timer.IsRepeating.Should().BeTrue();
    }

    /// <summary>
    ///   Verifies that the context/token overload forwards callback state, registration, and token.
    /// </summary>
    [Fact]
    public void RegisterTimer_ContextAndTokenOverload_ForwardsStateRegistrationAndToken ()
    {
        IPrimeTestClock clock = new PrimeTestClock(Instant.FromUtc(2025, 1, 1, 0, 0, 0));
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

        clock.Advance(Duration.FromMinutes(5));

        receivedState.Should().BeSameAs(state);
        receivedTimer.Should().BeSameAs(timer);
        receivedToken.Should().NotBeNull();
        receivedToken!.Value.Should().Be(cts.Token);
    }

    /// <summary>
    ///   Verifies that the context-only convenience overload forwards callback state and registration.
    /// </summary>
    [Fact]
    public void RegisterTimer_ContextOverload_ForwardsStateAndRegistration ()
    {
        IPrimeTestClock clock = new PrimeTestClock(Instant.FromUtc(2025, 1, 1, 0, 0, 0));
        object state = new();
        object? receivedState = null;
        IClockIntervalTimer? receivedTimer = null;

        using IClockIntervalTimer timer = clock.RegisterTimer(TimeSpan.FromMinutes(5), context =>
        {
            receivedState = context.CallbackState;
            receivedTimer = (IClockIntervalTimer)context.Registration;
        }, CancellationToken.None, state);

        clock.Advance(Duration.FromMinutes(5));

        receivedState.Should().BeSameAs(state);
        receivedTimer.Should().BeSameAs(timer);
        timer.IsRepeating.Should().BeFalse();
    }

    /// <summary>
    ///   Verifies that the context-only convenience overload with <c>repeat: true</c> maps to a repeating interval.
    /// </summary>
    [Fact]
    public void RegisterTimer_ContextOverloadRepeatTrue_FiresOnEachInterval ()
    {
        IPrimeTestClock clock = new PrimeTestClock(Instant.FromUtc(2025, 1, 1, 0, 0, 0));
        int fired = 0;

        using IClockIntervalTimer timer = clock.RegisterTimer(TimeSpan.FromMinutes(5), _ => fired++,
            CancellationToken.None, repeat: true);

        clock.Advance(Duration.FromMinutes(5));
        clock.Advance(Duration.FromMinutes(5));

        fired.Should().Be(2);
        timer.IsRepeating.Should().BeTrue();
    }

    /// <summary>
    ///   Verifies that the token-only async overload forwards the registration token and repeats.
    /// </summary>
    [Fact]
    public void RegisterAsyncTimer_TokenOnlyRepeatTrue_ForwardsCancellationTokenAndRepeats ()
    {
        IPrimeTestClock clock = new PrimeTestClock(Instant.FromUtc(2025, 1, 1, 0, 0, 0));
        using CancellationTokenSource cts = new();
        CancellationToken? receivedToken = null;
        int fired = 0;

        using IClockIntervalTimer timer = clock.RegisterAsyncTimer(TimeSpan.FromMinutes(5), ct =>
        {
            receivedToken = ct;
            fired++;
            return default;
        }, cts.Token, repeat: true);

        clock.Advance(Duration.FromMinutes(5));
        clock.Advance(Duration.FromMinutes(5));

        receivedToken.Should().NotBeNull();
        receivedToken!.Value.Should().Be(cts.Token);
        fired.Should().Be(2);
        timer.IsRepeating.Should().BeTrue();
    }

    /// <summary>
    ///   Verifies that the async overload with context and cancellation token forwards state, registration, and token.
    /// </summary>
    [Fact]
    public void RegisterAsyncTimer_ContextAndTokenOverload_ForwardsStateRegistrationAndToken ()
    {
        IPrimeTestClock clock = new PrimeTestClock(Instant.FromUtc(2025, 1, 1, 0, 0, 0));
        object state = new();
        object? receivedState = null;
        IClockIntervalTimer? receivedTimer = null;
        using CancellationTokenSource cts = new();
        CancellationToken? receivedToken = null;

        using IClockIntervalTimer timer = clock.RegisterAsyncTimer(TimeSpan.FromMinutes(5), (context, ct) =>
        {
            receivedState = context.CallbackState;
            receivedTimer = (IClockIntervalTimer)context.Registration;
            receivedToken = ct;
            return default;
        }, cts.Token, state);

        clock.Advance(Duration.FromMinutes(5));

        receivedState.Should().BeSameAs(state);
        receivedTimer.Should().BeSameAs(timer);
        receivedToken.Should().NotBeNull();
        receivedToken!.Value.Should().Be(cts.Token);
    }

    /// <summary>
    ///   Verifies that the async overload with context and token repeats when <c>repeat: true</c>.
    /// </summary>
    [Fact]
    public void RegisterAsyncTimer_ContextAndTokenOverloadRepeatTrue_Repeats ()
    {
        IPrimeTestClock clock = new PrimeTestClock(Instant.FromUtc(2025, 1, 1, 0, 0, 0));
        using CancellationTokenSource cts = new();
        int fired = 0;

        using IClockIntervalTimer timer = clock.RegisterAsyncTimer(TimeSpan.FromMinutes(5), (_, _) =>
        {
            fired++;
            return default;
        }, cts.Token, repeat: true);

        clock.Advance(Duration.FromMinutes(5));
        clock.Advance(Duration.FromMinutes(5));

        fired.Should().Be(2);
        timer.IsRepeating.Should().BeTrue();
    }

    /// <summary>
    ///   Verifies that the explicit-repeat convenience overload preserves distinct initial and repeat intervals.
    /// </summary>
    [Fact]
    public void RegisterTimer_ExplicitRepeatIntervalActionOverload_UsesSeparateRepeatInterval ()
    {
        IPrimeTestClock clock = new PrimeTestClock(Instant.FromUtc(2025, 1, 1, 0, 0, 0));
        int fired = 0;

        using IClockIntervalTimer timer = clock.RegisterTimer(TimeSpan.FromMinutes(2), TimeSpan.FromMinutes(3), () => fired++,
            CancellationToken.None);

        clock.Advance(Duration.FromMinutes(2));
        fired.Should().Be(1);
        clock.Advance(Duration.FromMinutes(2));
        fired.Should().Be(1);
        clock.Advance(Duration.FromMinutes(1));
        fired.Should().Be(2);
        timer.IsRepeating.Should().BeTrue();
    }

    /// <summary>
    ///   Verifies that the explicit initial and repeat interval overload with context and token forwards state and token.
    /// </summary>
    [Fact]
    public void RegisterTimer_ExplicitRepeatIntervalContextAndTokenOverload_ForwardsStateAndToken ()
    {
        IPrimeTestClock clock = new PrimeTestClock(Instant.FromUtc(2025, 1, 1, 0, 0, 0));
        object state = new();
        object? receivedState = null;
        using CancellationTokenSource cts = new();
        CancellationToken? receivedToken = null;
        int fired = 0;

        using IClockIntervalTimer timer = clock.RegisterTimer(TimeSpan.FromMinutes(2), TimeSpan.FromMinutes(3),
            (context, ct) =>
            {
                receivedState = context.CallbackState;
                receivedToken = ct;
                fired++;
            }, cts.Token, state);

        clock.Advance(Duration.FromMinutes(2));
        fired.Should().Be(1);
        receivedState.Should().BeSameAs(state);
        receivedToken.Should().NotBeNull();
        receivedToken!.Value.Should().Be(cts.Token);

        clock.Advance(Duration.FromMinutes(2));
        fired.Should().Be(1);

        clock.Advance(Duration.FromMinutes(1));
        fired.Should().Be(2);
        timer.IsRepeating.Should().BeTrue();
    }

    /// <summary>
    ///   Verifies that the explicit initial and repeat interval async token-only overload forwards the token and honors both intervals.
    /// </summary>
    [Fact]
    public void RegisterAsyncTimer_ExplicitRepeatIntervalTokenOnlyOverload_ForwardsTokenAndUsesIntervals ()
    {
        IPrimeTestClock clock = new PrimeTestClock(Instant.FromUtc(2025, 1, 1, 0, 0, 0));
        using CancellationTokenSource cts = new();
        CancellationToken? firstToken = null;
        CancellationToken? secondToken = null;
        int fired = 0;

        using IClockIntervalTimer timer = clock.RegisterAsyncTimer(TimeSpan.FromMinutes(2), TimeSpan.FromMinutes(3), ct =>
        {
            fired++;
            if (fired == 1)
            {
                firstToken = ct;
            }
            else
            {
                secondToken = ct;
            }

            return default;
        }, cts.Token);

        clock.Advance(Duration.FromMinutes(2));
        fired.Should().Be(1);
        firstToken.Should().NotBeNull();
        firstToken!.Value.Should().Be(cts.Token);

        clock.Advance(Duration.FromMinutes(2));
        fired.Should().Be(1);

        clock.Advance(Duration.FromMinutes(1));
        fired.Should().Be(2);
        secondToken.Should().NotBeNull();
        secondToken!.Value.Should().Be(cts.Token);
        timer.IsRepeating.Should().BeTrue();
    }

#if NET
    /// <summary>
    ///   Verifies that the local-time action convenience overload fires at the requested local time.
    /// </summary>
    [Fact]
    public void RegisterTimeOfDay_LocalActionOverload_FiresAtRequestedLocalTime ()
    {
        IPrimeTestClock clock = new PrimeTestClock(Instant.FromUtc(2025, 1, 1, 0, 0, 0));
        LocalTimeOfDay target = new(TimeOnly.FromDateTime(clock.LocalNowDateTimeOffset.DateTime.AddMinutes(10)));
        int fired = 0;

        using IClockDayTimeTimer timer = PrimeClockTimerExtensions.RegisterTimeOfDay(clock, target, () => fired++, CancellationToken.None);

        clock.Advance(Duration.FromMinutes(9));
        fired.Should().Be(0);
        clock.Advance(Duration.FromMinutes(1));
        fired.Should().Be(1);
        timer.IsLocalTimeRepresentation.Should().BeTrue();
    }

    /// <summary>
    ///   Verifies that the local-time context/token convenience overload forwards state, registration, and token.
    /// </summary>
    [Fact]
    public void RegisterTimeOfDay_LocalContextAndTokenOverload_ForwardsStateRegistrationAndToken ()
    {
        IPrimeTestClock clock = new PrimeTestClock(Instant.FromUtc(2025, 1, 1, 0, 0, 0));
        LocalTimeOfDay target = new(TimeOnly.FromDateTime(clock.LocalNowDateTimeOffset.DateTime.AddMinutes(10)));
        object state = new();
        object? receivedState = null;
        IClockDayTimeTimer? receivedTimer = null;
        using CancellationTokenSource cts = new();
        CancellationToken? receivedToken = null;

        using IClockDayTimeTimer timer = PrimeClockTimerExtensions.RegisterTimeOfDay(clock, target, (context, ct) =>
        {
            receivedState = context.CallbackState;
            receivedTimer = (IClockDayTimeTimer)context.Registration;
            receivedToken = ct;
        }, cts.Token, state);

        clock.Advance(Duration.FromMinutes(10));

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
        IPrimeTestClock clock = new PrimeTestClock(Instant.FromUtc(2025, 1, 1, 0, 0, 0));
        LocalTimeOfDay target = new(TimeOnly.FromDateTime(clock.LocalNowDateTimeOffset.DateTime.AddMinutes(10)));
        using CancellationTokenSource cts = new();
        CancellationToken? receivedToken = null;

        using IClockDayTimeTimer timer = PrimeClockTimerExtensions.RegisterAsyncTimeOfDay(clock, target, ct =>
        {
            receivedToken = ct;
            return default;
        }, cts.Token);

        clock.Advance(Duration.FromMinutes(10));

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
        IPrimeTestClock clock = new PrimeTestClock(Instant.FromUtc(2025, 1, 1, 0, 0, 0));
        UtcTimeOfDay target = new(TimeOnly.FromDateTime(clock.UtcNowDateTimeOffset.UtcDateTime.AddMinutes(10)));
        int fired = 0;

        using IClockDayTimeTimer timer = PrimeClockTimerExtensions.RegisterTimeOfDay(clock, target, () => fired++, CancellationToken.None);

        clock.Advance(Duration.FromMinutes(9));
        fired.Should().Be(0);
        clock.Advance(Duration.FromMinutes(1));
        fired.Should().Be(1);
        timer.IsLocalTimeRepresentation.Should().BeFalse();
    }

    /// <summary>
    ///   Verifies that the UTC context/token convenience overload forwards state, registration, and token.
    /// </summary>
    [Fact]
    public void RegisterTimeOfDay_UtcContextAndTokenOverload_ForwardsStateRegistrationAndToken ()
    {
        IPrimeTestClock clock = new PrimeTestClock(Instant.FromUtc(2025, 1, 1, 0, 0, 0));
        UtcTimeOfDay target = new(TimeOnly.FromDateTime(clock.UtcNowDateTimeOffset.UtcDateTime.AddMinutes(10)));
        object state = new();
        object? receivedState = null;
        IClockDayTimeTimer? receivedTimer = null;
        using CancellationTokenSource cts = new();
        CancellationToken? receivedToken = null;

        using IClockDayTimeTimer timer = PrimeClockTimerExtensions.RegisterTimeOfDay(clock, target, (context, ct) =>
        {
            receivedState = context.CallbackState;
            receivedTimer = (IClockDayTimeTimer)context.Registration;
            receivedToken = ct;
        }, cts.Token, state);

        clock.Advance(Duration.FromMinutes(10));

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
        IPrimeTestClock clock = new PrimeTestClock(Instant.FromUtc(2025, 1, 1, 0, 0, 0));
        UtcTimeOfDay target = new(TimeOnly.FromDateTime(clock.UtcNowDateTimeOffset.UtcDateTime.AddMinutes(10)));
        using CancellationTokenSource cts = new();
        CancellationToken? receivedToken = null;

        using IClockDayTimeTimer timer = PrimeClockTimerExtensions.RegisterAsyncTimeOfDay(clock, target, ct =>
        {
            receivedToken = ct;
            return default;
        }, cts.Token);

        clock.Advance(Duration.FromMinutes(10));

        receivedToken.Should().NotBeNull();
        receivedToken!.Value.Should().Be(cts.Token);
        timer.IsLocalTimeRepresentation.Should().BeFalse();
    }
#endif
}
//################################################################################

