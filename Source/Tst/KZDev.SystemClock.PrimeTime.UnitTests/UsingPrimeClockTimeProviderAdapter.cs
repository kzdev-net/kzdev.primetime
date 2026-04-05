// Copyright (c) Kevin Zehrer
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

// Unit tests for TimeProvider adapter.
// Verifies that ToTimeProvider() returns a TimeProvider driven by the PrimeTime clock,
// and that GetUtcNow and CreateTimer are controllable via the test clock.

using System.Diagnostics.CodeAnalysis;

using AwesomeAssertions;
using KZDev.PrimeTime.Tests;

namespace KZDev.SystemClock.PrimeTime.UnitTests;

//################################################################################
/// <summary>
///   Unit tests for <see cref="PrimeClockTimeProviderAdapter"/> and
///   <see cref="PrimeClockTimeProviderExtensions.ToTimeProvider(IPrimeClock)"/>.
/// </summary>
[ExcludeFromCodeCoverage]
public class UsingPrimeClockTimeProviderAdapter : UnitTestBase
{
    #region Constructors/Finalizers

    //----------------------------------------------------------------------------
    /// <summary>
    ///   Initializes a new instance of the <see cref="UsingPrimeClockTimeProviderAdapter"/> class.
    /// </summary>
    /// <param name="xUnitTestOutputHelper">
    ///   The xUnit test output helper that can be used to output test messages.
    /// </param>
    public UsingPrimeClockTimeProviderAdapter (ITestOutputHelper xUnitTestOutputHelper)
        : base(xUnitTestOutputHelper)
    {
    }
    //----------------------------------------------------------------------------

    #endregion Constructors/Finalizers

    #region ToTimeProvider extension

    /// <summary>
    ///   Verifies that <see cref="PrimeClockTimeProviderExtensions.ToTimeProvider(IPrimeClock)"/>
    ///   returns a non-null TimeProvider when given a test clock.
    /// </summary>
    [Fact]
    public void ToTimeProvider_WithPrimeTestClock_ReturnsNonNullTimeProvider ()
    {
        IPrimeTestClock clock = new PrimeTestClock(new DateTimeOffset(2020, 6, 15, 12, 0, 0, TimeSpan.Zero));
        TimeProvider provider = clock.ToTimeProvider();
        provider.Should().NotBeNull();
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies that <see cref="PrimeClockTimeProviderExtensions.ToTimeProvider(IPrimeClock)"/>
    ///   throws <see cref="ArgumentNullException"/> when the clock is null.
    /// </summary>
    [Fact]
    public void ToTimeProvider_WithNullClock_ThrowsArgumentNullException ()
    {
        IPrimeClock? clock = null;
        Action act = () => clock!.ToTimeProvider();
        act.Should().Throw<ArgumentNullException>().WithParameterName("clock");
    }
    //----------------------------------------------------------------------------

    #endregion ToTimeProvider extension

    #region GetUtcNow driven by test clock

    /// <summary>
    ///   Verifies that the TimeProvider returned from the test clock reports GetUtcNow
    ///   equal to the clock's UtcNowOffset after SetTime.
    /// </summary>
    [Fact]
    public void GetUtcNow_AfterSetTime_ReturnsSetTime ()
    {
        DateTimeOffset setTime = new(2025, 1, 10, 14, 30, 0, TimeSpan.Zero);
        IPrimeTestClock clock = new PrimeTestClock();
        clock.SetTime(setTime);
        TimeProvider provider = clock.ToTimeProvider();
        provider.GetUtcNow().Should().Be(setTime);
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies that the TimeProvider's GetUtcNow advances when the test clock is advanced.
    /// </summary>
    [Fact]
    public void GetUtcNow_AfterAdvance_ReturnsAdvancedTime ()
    {
        DateTimeOffset initial = new(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);
        IPrimeTestClock clock = new PrimeTestClock(initial);
        TimeProvider provider = clock.ToTimeProvider();
        provider.GetUtcNow().Should().Be(initial);
        clock.Advance(TimeSpan.FromHours(2));
        provider.GetUtcNow().Should().Be(initial + TimeSpan.FromHours(2));
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies that the TimeProvider's GetUtcNow advances when RunFor is called.
    /// </summary>
    [Fact]
    public void GetUtcNow_AfterRunFor_ReturnsAdvancedTime ()
    {
        DateTimeOffset initial = new(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);
        IPrimeTestClock clock = new PrimeTestClock(initial);
        TimeProvider provider = clock.ToTimeProvider();
        clock.RunFor(TimeSpan.FromMinutes(30));
        provider.GetUtcNow().Should().Be(initial + TimeSpan.FromMinutes(30));
    }
    //----------------------------------------------------------------------------

    #endregion GetUtcNow driven by test clock

    #region CreateTimer driven by test clock

    /// <summary>
    ///   Verifies that a timer created via the adapter fires when the test clock is advanced
    ///   past the due time.
    /// </summary>
    [Fact]
    public void CreateTimer_WithDueTime_AfterAdvance_FiresCallback ()
    {
        DateTimeOffset initial = new(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);
        IPrimeTestClock clock = new PrimeTestClock(initial);
        TimeProvider provider = clock.ToTimeProvider();
        int fired = 0;
        using ITimer timer = provider.CreateTimer(_ => { fired++; }, null, TimeSpan.FromMinutes(10), Timeout.InfiniteTimeSpan);
        fired.Should().Be(0);
        clock.Advance(TimeSpan.FromMinutes(5));
        fired.Should().Be(0);
        clock.Advance(TimeSpan.FromMinutes(10));
        fired.Should().Be(1);
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies that a repeating timer created via the adapter fires multiple times when
    ///   the test clock is advanced.
    /// </summary>
    [Fact]
    public void CreateTimer_Repeating_AfterAdvance_FiresMultipleTimes ()
    {
        DateTimeOffset initial = new(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);
        IPrimeTestClock clock = new PrimeTestClock(initial);
        TimeProvider provider = clock.ToTimeProvider();
        int fired = 0;
        using ITimer timer = provider.CreateTimer(_ => { fired++; }, null, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));
        clock.Advance(TimeSpan.FromMinutes(5));
        fired.Should().Be(1);
        clock.Advance(TimeSpan.FromMinutes(5));
        fired.Should().Be(2);
        clock.Advance(TimeSpan.FromMinutes(5));
        fired.Should().Be(3);
    }
    //----------------------------------------------------------------------------

    #endregion CreateTimer driven by test clock
}
//################################################################################

