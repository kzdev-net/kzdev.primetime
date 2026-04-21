// Copyright (c) Kevin Zehrer
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

#if NET

using System.Diagnostics.CodeAnalysis;
using System.Reflection;

using AwesomeAssertions;

using KZDev.PrimeTime.Tests;

using NodaTime;
using NodaTime.Testing;

namespace KZDev.PrimeTime.UnitTests;

/// <summary>
///   Unit tests for Noda <see cref="ClockDayTimeTimerRegistration"/> behavior exposed through
///   <see cref="IClockDayTimeTimer"/> (elapsed / time-until-next metrics and dynamic
///   <see cref="IClockDayTimeTimer.Change(LocalTime)"/>).
/// </summary>
[ExcludeFromCodeCoverage]
public class UsingClockDayTimeTimerRegistration : UnitTestBase
{
    /// <summary>
    ///   Initializes a new instance of the <see cref="UsingClockDayTimeTimerRegistration"/> class.
    /// </summary>
    /// <param name="xUnitTestOutputHelper">The xUnit test output helper.</param>
    public UsingClockDayTimeTimerRegistration (ITestOutputHelper xUnitTestOutputHelper)
        : base(xUnitTestOutputHelper)
    {
    }

    /// <summary>
    ///   Verifies <see cref="IDayTimeTimer.ElapsedTime"/> is <c>-1</c> before any callback has started.
    /// </summary>
    [Fact]
    public void RegisterTimeOfDay_Local_BeforeFirstFire_ElapsedTime_IsNegativeOne ()
    {
        Instant utcNow = Instant.FromUtc(2025, 6, 15, 10, 0, 0);
        IPrimeClock clock = new PrimeClock(new FakeClock(utcNow), DateTimeZone.Utc);
        LocalTime target = new(18, 0, 0);
        using IClockDayTimeTimer registration = clock.RegisterTimeOfDay(target,
            static _ => { },
            TestContext.Current.CancellationToken);
        registration.ElapsedTime.Should().Be(-1L);
    }

    /// <summary>
    ///   Verifies <see cref="IDayTimeTimer.ElapsedTime"/> is <c>0</c> while a synchronous callback is executing.
    /// </summary>
    [Fact]
    public void RegisterTimeOfDay_Local_InsideSyncCallback_ElapsedTime_IsZero ()
    {
        Instant utcNow = Instant.FromUtc(2025, 6, 15, 10, 0, 0);
        FakeClock fake = new(utcNow);
        IPrimeClock clock = new PrimeClock(fake, DateTimeZone.Utc);
        LocalTime target = new(18, 0, 0);
        long observedInsideCallback = -2;
        using IClockDayTimeTimer registration = clock.RegisterTimeOfDay(target,
            context =>
            {
                IClockDayTimeTimer dayTimer = (IClockDayTimeTimer)context.Registration;
                observedInsideCallback = dayTimer.ElapsedTime;
            },
            TestContext.Current.CancellationToken);
        MethodInfo onTimerTick = ClockTimerRegistrationTestReflection.GetDayTimeOnTimerTickMethod(
            typeof(ClockDayTimeTimerRegistration));
        fake.Advance(Duration.FromHours(8));
        onTimerTick.Invoke(registration, new object?[] { null });
        observedInsideCallback.Should().Be(0L);
    }

    /// <summary>
    ///   Verifies <see cref="IDayTimeTimer.ElapsedTime"/> reflects milliseconds since the last callback start after
    ///   the virtual clock advances.
    /// </summary>
    [Fact]
    public void RegisterTimeOfDay_Local_AfterCallbackAndClockAdvance_ElapsedTime_IsPositive ()
    {
        Instant utcNow = Instant.FromUtc(2025, 6, 15, 10, 0, 0);
        FakeClock fake = new(utcNow);
        IPrimeClock clock = new PrimeClock(fake, DateTimeZone.Utc);
        LocalTime target = new(18, 0, 0);
        using IClockDayTimeTimer registration = clock.RegisterTimeOfDay(target,
            static _ => { },
            TestContext.Current.CancellationToken);
        MethodInfo onTimerTick = ClockTimerRegistrationTestReflection.GetDayTimeOnTimerTickMethod(
            typeof(ClockDayTimeTimerRegistration));
        fake.Advance(Duration.FromHours(8));
        onTimerTick.Invoke(registration, new object?[] { null });
        fake.Advance(Duration.FromMilliseconds(150));
        registration.ElapsedTime.Should().BeGreaterThanOrEqualTo(100L).And.BeLessThanOrEqualTo(300L);
    }

    /// <summary>
    ///   Verifies <see cref="IDayTimeTimer.TimeUntilNextCallback"/> is <c>0</c> when the stored next-fire instant is
    ///   not after the current virtual instant (no timer tick required).
    /// </summary>
    [Fact]
    public void RegisterTimeOfDay_Local_NextFireInstantInPast_TimeUntilNext_IsZero ()
    {
        Instant utcNow = Instant.FromUtc(2025, 6, 15, 10, 0, 0);
        FakeClock fake = new(utcNow);
        IPrimeClock clock = new PrimeClock(fake, DateTimeZone.Utc);
        LocalTime target = new(14, 0, 0);
        using IClockDayTimeTimer registration = clock.RegisterTimeOfDay(target,
            static _ => { },
            TestContext.Current.CancellationToken);
        registration.TimeUntilNextCallback.Should().BeGreaterThan(0L);
        fake.Advance(Duration.FromHours(6));
        registration.TimeUntilNextCallback.Should().Be(0L);
    }

    /// <summary>
    ///   Verifies <see cref="IClockDayTimeTimer.Change(LocalTime)"/> while the registration is enabled reschedules so
    ///   <see cref="IDayTimeTimer.TimeUntilNextCallback"/> reflects the new target.
    /// </summary>
    [Fact]
    public void Change_LocalTimeOfDayWhileEnabled_TimeUntilNextReflectsNewTarget ()
    {
        Instant utcNow = Instant.FromUtc(2025, 6, 15, 10, 0, 0);
        IPrimeClock clock = new PrimeClock(new FakeClock(utcNow), DateTimeZone.Utc);
        LocalTime initialTarget = new(18, 0, 0);
        using IClockDayTimeTimer registration = clock.RegisterTimeOfDay(initialTarget,
            static _ => { },
            TestContext.Current.CancellationToken);
        long beforeChange = registration.TimeUntilNextCallback;
        LocalTime newTarget = new(20, 0, 0);
        bool changed = registration.Change(newTarget);
        changed.Should().BeTrue();
        long afterChange = registration.TimeUntilNextCallback;
        afterChange.Should().BeGreaterThan(beforeChange);
    }

    /// <summary>
    ///   Verifies <see cref="IClockDayTimeTimer.Change(LocalTime)"/> returns <c>false</c> after the registration is
    ///   disposed.
    /// </summary>
    [Fact]
    public void Change_AfterDispose_ReturnsFalse ()
    {
        Instant utcNow = Instant.FromUtc(2025, 6, 15, 10, 0, 0);
        IPrimeClock clock = new PrimeClock(new FakeClock(utcNow), DateTimeZone.Utc);
        LocalTime target = new(18, 0, 0);
        IClockDayTimeTimer registration = clock.RegisterTimeOfDay(target,
            static _ => { },
            TestContext.Current.CancellationToken);
        registration.Dispose();
        bool changed = registration.Change(new LocalTime(19, 0, 0));
        changed.Should().BeFalse();
    }

    /// <summary>
    ///   Verifies <see cref="IClockDayTimeTimer.Change(LocalTime)"/> returns <c>false</c> after
    ///   <see cref="IClockTimer.Cancel"/> is called.
    /// </summary>
    [Fact]
    public void Change_AfterCancel_ReturnsFalse ()
    {
        Instant utcNow = Instant.FromUtc(2025, 6, 15, 10, 0, 0);
        IPrimeClock clock = new PrimeClock(new FakeClock(utcNow), DateTimeZone.Utc);
        LocalTime target = new(18, 0, 0);
        IClockDayTimeTimer registration = clock.RegisterTimeOfDay(target,
            static _ => { },
            TestContext.Current.CancellationToken);
        registration.Cancel();
        bool changed = registration.Change(new LocalTime(19, 0, 0));
        changed.Should().BeFalse();
    }
}

#endif
