// Copyright (c) Kevin Zehrer
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;

using AwesomeAssertions;

using KZDev.PrimeTime.Tests;

using NodaTime;

namespace KZDev.PrimeTime.UnitTests;

//################################################################################
/// <summary>
///   Unit tests for public convenience overloads in <see cref="PrimeClockNodaTimerExtensions"/>.
/// </summary>
[ExcludeFromCodeCoverage]
public class UsingPrimeClockNodaTimerExtensions : UnitTestBase
{
    #region Constructors/Finalizers

    /// <summary>
    ///   Initializes a new instance of the <see cref="UsingPrimeClockNodaTimerExtensions"/> class.
    /// </summary>
    /// <param name="xUnitTestOutputHelper">
    ///   The xUnit test output helper that can be used to output test messages.
    /// </param>
    public UsingPrimeClockNodaTimerExtensions (ITestOutputHelper xUnitTestOutputHelper)
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

        using IClockIntervalTimer timer = clock.RegisterTimer(Duration.FromMinutes(5), () => fired++, CancellationToken.None, repeat: true);

        clock.Advance(Duration.FromMinutes(4));
        fired.Should().Be(0);
        clock.Advance(Duration.FromMinutes(1));
        fired.Should().Be(1);
        clock.Advance(Duration.FromMinutes(5));
        fired.Should().Be(2);
        timer.IsRepeating.Should().BeTrue();
    }

    /// <summary>
    ///   Verifies that the context overload forwards callback state and registration.
    /// </summary>
    [Fact]
    public void RegisterTimer_ContextOverload_ForwardsStateAndRegistration ()
    {
        IPrimeTestClock clock = new PrimeTestClock(Instant.FromUtc(2025, 1, 1, 0, 0, 0));
        object state = new();
        object? receivedState = null;
        IClockIntervalTimer? receivedTimer = null;

        using IClockIntervalTimer timer = clock.RegisterTimer(Duration.FromMinutes(5), context =>
        {
            receivedState = context.CallbackState;
            receivedTimer = (IClockIntervalTimer)context.Registration;
        }, CancellationToken.None, state);

        clock.Advance(Duration.FromMinutes(5));

        receivedState.Should().BeSameAs(state);
        receivedTimer.Should().BeSameAs(timer);
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

        using IClockIntervalTimer timer = clock.RegisterAsyncTimer(Duration.FromMinutes(5), ct =>
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
    ///   Verifies that the context-only sync overload honors repeat=true and forwards callback state.
    /// </summary>
    [Fact]
    public void RegisterTimer_ContextOnlyRepeatTrue_ForwardsStateAndRepeats ()
    {
        IPrimeTestClock clock = new PrimeTestClock(Instant.FromUtc(2025, 1, 1, 0, 0, 0));
        object state = new();
        int fired = 0;
        object? receivedState = null;
        IClockTimer? receivedRegistration = null;

        using IClockIntervalTimer timer = clock.RegisterTimer(Duration.FromMinutes(5), context =>
            {
                fired++;
                receivedState = context.CallbackState;
                receivedRegistration = context.Registration;
            },
            CancellationToken.None,
            state,
            repeat: true);

        clock.Advance(Duration.FromMinutes(5));
        fired.Should().Be(1);
        clock.Advance(Duration.FromMinutes(5));
        fired.Should().Be(2);
        receivedState.Should().BeSameAs(state);
        receivedRegistration.Should().BeSameAs(timer);
        timer.IsRepeating.Should().BeTrue();
    }

    /// <summary>
    ///   Verifies that the context+token sync overload honors repeat=true and forwards cancellation token.
    /// </summary>
    [Fact]
    public void RegisterTimer_ContextAndTokenRepeatTrue_ForwardsCancellationTokenAndRepeats ()
    {
        IPrimeTestClock clock = new PrimeTestClock(Instant.FromUtc(2025, 1, 1, 0, 0, 0));
        using CancellationTokenSource cts = new();
        CancellationToken? receivedToken = null;
        int fired = 0;

        using IClockIntervalTimer timer = clock.RegisterTimer(Duration.FromMinutes(5), (context, ct) =>
            {
                fired++;
                receivedToken = ct;
            },
            cts.Token,
            state: null,
            repeat: true);

        clock.Advance(Duration.FromMinutes(5));
        fired.Should().Be(1);
        clock.Advance(Duration.FromMinutes(5));
        fired.Should().Be(2);
        receivedToken.Should().NotBeNull();
        receivedToken!.Value.Should().Be(cts.Token);
        timer.IsRepeating.Should().BeTrue();
    }

    /// <summary>
    ///   Verifies that cancelling the registration token stops subsequent callbacks for the repeating
    ///   context+token sync overload.
    /// </summary>
    [Fact]
    public void RegisterTimer_ContextAndTokenRepeatTrue_WhenTokenCancelled_StopsSubsequentCallbacks ()
    {
        IPrimeTestClock clock = new PrimeTestClock(Instant.FromUtc(2025, 1, 1, 0, 0, 0));
        using CancellationTokenSource cts = new();
        int fired = 0;

        using IClockIntervalTimer timer = clock.RegisterTimer(Duration.FromMinutes(5), (_, ct) => fired++, cts.Token,
            state: null, repeat: true);

        clock.Advance(Duration.FromMinutes(5));
        fired.Should().Be(1);

        cts.Cancel();
        clock.Advance(Duration.FromMinutes(20));

        fired.Should().Be(1);
        timer.IsCancelled.Should().BeTrue();
    }

    /// <summary>
    ///   Verifies that the explicit-repeat convenience overload preserves distinct initial and repeat intervals.
    /// </summary>
    [Fact]
    public void RegisterTimer_ExplicitRepeatIntervalActionOverload_UsesSeparateRepeatInterval ()
    {
        IPrimeTestClock clock = new PrimeTestClock(Instant.FromUtc(2025, 1, 1, 0, 0, 0));
        int fired = 0;

        using IClockIntervalTimer timer = clock.RegisterTimer(Duration.FromMinutes(2), Duration.FromMinutes(3), () => fired++,
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
    ///   Verifies that the explicit-repeat context+token sync overload uses separate initial and repeat intervals.
    /// </summary>
    [Fact]
    public void RegisterTimer_ExplicitRepeatIntervalContextAndTokenOverload_UsesSeparateRepeatInterval ()
    {
        IPrimeTestClock clock = new PrimeTestClock(Instant.FromUtc(2025, 1, 1, 0, 0, 0));
        int fired = 0;

        using IClockIntervalTimer timer = clock.RegisterTimer(Duration.FromMinutes(2), Duration.FromMinutes(3),
            (context, ct) => fired++,
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
    ///   Verifies that the explicit-repeat async token-only overload uses separate initial and repeat intervals.
    /// </summary>
    [Fact]
    public void RegisterAsyncTimer_ExplicitRepeatIntervalTokenOnlyOverload_UsesSeparateRepeatInterval ()
    {
        IPrimeTestClock clock = new PrimeTestClock(Instant.FromUtc(2025, 1, 1, 0, 0, 0));
        int fired = 0;

        using IClockIntervalTimer timer = clock.RegisterAsyncTimer(Duration.FromMinutes(2), Duration.FromMinutes(3),
            ct =>
            {
                fired++;
                return new ValueTask();
            },
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
    ///   Verifies that the local-time action convenience overload fires at the requested local time.
    /// </summary>
    [Fact]
    public void RegisterTimeOfDay_ActionOverload_FiresAtRequestedLocalTime ()
    {
        IPrimeTestClock clock = new PrimeTestClock(Instant.FromUtc(2025, 1, 1, 0, 0, 0), DateTimeZone.Utc);
        LocalTime target = clock.LocalNowInstant.PlusMinutes(10).TimeOfDay;
        int fired = 0;

        using IClockDayTimeTimer timer = clock.RegisterTimeOfDay(target, () => fired++, CancellationToken.None);

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
    public void RegisterTimeOfDay_ContextAndTokenOverload_ForwardsStateRegistrationAndToken ()
    {
        IPrimeTestClock clock = new PrimeTestClock(Instant.FromUtc(2025, 1, 1, 0, 0, 0), DateTimeZone.Utc);
        LocalTime target = clock.LocalNowInstant.PlusMinutes(10).TimeOfDay;
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
    public void RegisterAsyncTimeOfDay_TokenOnlyOverload_ForwardsCancellationToken ()
    {
        IPrimeTestClock clock = new PrimeTestClock(Instant.FromUtc(2025, 1, 1, 0, 0, 0), DateTimeZone.Utc);
        LocalTime target = clock.LocalNowInstant.PlusMinutes(10).TimeOfDay;
        using CancellationTokenSource cts = new();
        CancellationToken? receivedToken = null;
        int fired = 0;

        using IClockDayTimeTimer timer = clock.RegisterAsyncTimeOfDay(target, ct =>
        {
            receivedToken = ct;
            fired++;
            return default;
        }, cts.Token);

        clock.Advance(Duration.FromMinutes(10));

        receivedToken.Should().NotBeNull();
        receivedToken!.Value.Should().Be(cts.Token);
        fired.Should().Be(1);
        timer.IsRepeating.Should().BeTrue();
    }

#if NET
    /// <summary>
    ///   Verifies that the <see cref="LocalTimeOfDay"/> context overload converts and forwards state and registration.
    /// </summary>
    [Fact]
    public void RegisterTimeOfDay_LocalTimeOfDayContextOverload_ForwardsStateAndRegistration ()
    {
        IPrimeTestClock clock = new PrimeTestClock(Instant.FromUtc(2025, 1, 1, 0, 0, 0), DateTimeZone.Utc);
        LocalDateTime targetLocalTime = clock.LocalNowInstant.PlusMinutes(10);
        LocalTimeOfDay target = new(new TimeOnly(targetLocalTime.Hour, targetLocalTime.Minute, targetLocalTime.Second));
        object state = new();
        object? receivedState = null;
        IClockDayTimeTimer? receivedTimer = null;

        using IClockDayTimeTimer timer = PrimeClockNodaTimerExtensions.RegisterTimeOfDay(clock, target, context =>
        {
            receivedState = context.CallbackState;
            receivedTimer = (IClockDayTimeTimer)context.Registration;
        }, CancellationToken.None, state);

        clock.Advance(Duration.FromMinutes(10));

        receivedState.Should().BeSameAs(state);
        receivedTimer.Should().BeSameAs(timer);
    }

    /// <summary>
    ///   Verifies that the <see cref="LocalTimeOfDay"/> async context overload forwards state, registration, and token.
    /// </summary>
    [Fact]
    public void RegisterAsyncTimeOfDay_LocalTimeOfDayContextOverload_ForwardsStateRegistrationAndToken ()
    {
        IPrimeTestClock clock = new PrimeTestClock(Instant.FromUtc(2025, 1, 1, 0, 0, 0), DateTimeZone.Utc);
        LocalDateTime targetLocalTime = clock.LocalNowInstant.PlusMinutes(10);
        LocalTimeOfDay target = new(new TimeOnly(targetLocalTime.Hour, targetLocalTime.Minute, targetLocalTime.Second));
        object state = new();
        object? receivedState = null;
        IClockDayTimeTimer? receivedTimer = null;
        using CancellationTokenSource cts = new();
        CancellationToken? receivedToken = null;

        using IClockDayTimeTimer timer = PrimeClockNodaTimerExtensions.RegisterAsyncTimeOfDay(clock, target,
            (context, ct) =>
            {
                receivedState = context.CallbackState;
                receivedTimer = (IClockDayTimeTimer)context.Registration;
                receivedToken = ct;
                return default;
            },
            cts.Token,
            state);

        clock.Advance(Duration.FromMinutes(10));

        receivedState.Should().BeSameAs(state);
        receivedTimer.Should().BeSameAs(timer);
        receivedToken.Should().NotBeNull();
        receivedToken!.Value.Should().Be(cts.Token);
    }
#endif
}
//################################################################################
