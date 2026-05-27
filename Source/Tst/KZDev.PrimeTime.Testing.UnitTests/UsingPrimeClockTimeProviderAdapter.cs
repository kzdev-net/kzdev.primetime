// Copyright (c) Kevin Zehrer
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

// Unit tests for TimeProvider adapter.
// Verifies that ToTimeProvider() returns a TimeProvider driven by the PrimeTime NodaTime clock,
// and that GetUtcNow and CreateTimer are controllable via the test clock.

using System.Diagnostics.CodeAnalysis;

using AwesomeAssertions;

using KZDev.PrimeTime.Tests;

using NodaTime;

namespace KZDev.PrimeTime.Testing.UnitTests;

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
        Instant initial = Instant.FromUtc(2020, 6, 15, 12, 0, 0);
        IPrimeTestClock clock = new PrimeTestClock(initial);
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
    ///   equal to the clock's instant after SetInstant.
    /// </summary>
    [Fact]
    public void GetUtcNow_AfterSetInstant_ReturnsCorrespondingDateTimeOffset ()
    {
        Instant setInstant = Instant.FromUtc(2025, 1, 10, 14, 30, 0);
        IPrimeTestClock clock = new PrimeTestClock();
        clock.SetInstant(setInstant);
        TimeProvider provider = clock.ToTimeProvider();
        provider.GetUtcNow().Should().Be(setInstant.InUtc().ToDateTimeOffset());
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies that the TimeProvider's GetUtcNow advances when the test clock is advanced.
    /// </summary>
    [Fact]
    public void GetUtcNow_AfterAdvance_ReturnsAdvancedTime ()
    {
        Instant initial = Instant.FromUtc(2025, 1, 1, 0, 0, 0);
        IPrimeTestClock clock = new PrimeTestClock(initial);
        TimeProvider provider = clock.ToTimeProvider();
        provider.GetUtcNow().Should().Be(initial.InUtc().ToDateTimeOffset());
        clock.Advance(Duration.FromHours(2));
        provider.GetUtcNow().Should().Be(initial.Plus(Duration.FromHours(2)).InUtc().ToDateTimeOffset());
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies that the TimeProvider's GetUtcNow advances when RunFor is called.
    /// </summary>
    [Fact]
    public void GetUtcNow_AfterRunFor_ReturnsAdvancedTime ()
    {
        Instant initial = Instant.FromUtc(2025, 1, 1, 0, 0, 0);
        Duration runDuration = Duration.FromMinutes(30);
        IPrimeTestClock clock = new PrimeTestClock(initial);
        TimeProvider provider = clock.ToTimeProvider();
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        clock.RunFor(runDuration, Duration.FromTimeSpan(RunForTestFastPerSecondRate)).Should().BeTrue();
        WaitUntilClockStopped(() => clock.IsRunning, RunForTestWaitTimeout, cancellationToken);
        provider.GetUtcNow().Should().Be(initial.Plus(runDuration).InUtc().ToDateTimeOffset());
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies that the adapter reports local time consistently with its exposed
    ///   <see cref="TimeProvider.LocalTimeZone"/>.
    /// </summary>
    [Fact]
    public void GetLocalNow_UsesProviderLocalTimeZone ()
    {
        Instant initial = Instant.FromUtc(2025, 1, 1, 12, 0, 0);
        IPrimeTestClock clock = new PrimeTestClock(initial);
        TimeProvider provider = clock.ToTimeProvider();

        provider.GetLocalNow().Should().Be(TimeZoneInfo.ConvertTime(provider.GetUtcNow(), provider.LocalTimeZone));
        provider.LocalTimeZone.Should().Be(TimeZoneInfo.Local);
    }
    //----------------------------------------------------------------------------

    #endregion GetUtcNow driven by test clock

    #region CreateTimer driven by test clock

    /// <summary>
    ///   Verifies that <see cref="TimeProvider.CreateTimer(TimerCallback, object?, TimeSpan, TimeSpan)"/>
    ///   throws <see cref="ArgumentNullException"/> when the callback is null.
    /// </summary>
    [Fact]
    public void CreateTimer_WithNullCallback_ThrowsArgumentNullException ()
    {
        IPrimeTestClock clock = new PrimeTestClock();
        TimeProvider provider = clock.ToTimeProvider();

        Action act = () => provider.CreateTimer(null!, null, TimeSpan.Zero, Timeout.InfiniteTimeSpan);

        act.Should().Throw<ArgumentNullException>().WithParameterName("callback");
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies that a timer created via the adapter fires when the test clock is advanced
    ///   past the due time.
    /// </summary>
    [Fact]
    public void CreateTimer_WithDueTime_AfterAdvance_FiresCallback ()
    {
        Instant initial = Instant.FromUtc(2025, 1, 1, 0, 0, 0);
        IPrimeTestClock clock = new PrimeTestClock(initial);
        TimeProvider provider = clock.ToTimeProvider();
        int fired = 0;
        using ITimer timer = provider.CreateTimer(_ => { fired++; }, null, TimeSpan.FromMinutes(10), Timeout.InfiniteTimeSpan);
        fired.Should().Be(0);
        clock.Advance(Duration.FromMinutes(5));
        fired.Should().Be(0);
        clock.Advance(Duration.FromMinutes(10));
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
        Instant initial = Instant.FromUtc(2025, 1, 1, 0, 0, 0);
        IPrimeTestClock clock = new PrimeTestClock(initial);
        TimeProvider provider = clock.ToTimeProvider();
        int fired = 0;
        using ITimer timer = provider.CreateTimer(_ => { fired++; }, null, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));
        clock.Advance(Duration.FromMinutes(5));
        fired.Should().Be(1);
        clock.Advance(Duration.FromMinutes(5));
        fired.Should().Be(2);
        clock.Advance(Duration.FromMinutes(5));
        fired.Should().Be(3);
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies that a negative period is treated as one-shot by the adapter.
    /// </summary>
    [Fact]
    public void CreateTimer_WithNegativePeriod_FiresOnlyOnce ()
    {
        Instant initial = Instant.FromUtc(2025, 1, 1, 0, 0, 0);
        IPrimeTestClock clock = new PrimeTestClock(initial);
        TimeProvider provider = clock.ToTimeProvider();
        int fired = 0;

        using ITimer timer = provider.CreateTimer(_ => fired++, null, TimeSpan.FromMinutes(5), TimeSpan.FromSeconds(-1));

        clock.Advance(Duration.FromMinutes(5));
        clock.Advance(Duration.FromMinutes(10));

        fired.Should().Be(1);
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies that changing a created timer reschedules the next fire using the adapter.
    /// </summary>
    [Fact]
    public void CreateTimer_Change_ReschedulesNextFire ()
    {
        Instant initial = Instant.FromUtc(2025, 1, 1, 0, 0, 0);
        IPrimeTestClock clock = new PrimeTestClock(initial);
        TimeProvider provider = clock.ToTimeProvider();
        int fired = 0;

        using ITimer timer = provider.CreateTimer(_ => fired++, null, TimeSpan.FromMinutes(10), Timeout.InfiniteTimeSpan);

        clock.Advance(Duration.FromMinutes(5));
        timer.Change(TimeSpan.FromMinutes(2), Timeout.InfiniteTimeSpan).Should().BeTrue();
        clock.Advance(Duration.FromMinutes(1));
        fired.Should().Be(0);
        clock.Advance(Duration.FromMinutes(1));
        fired.Should().Be(1);
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies that disposing a created timer asynchronously prevents future callbacks.
    /// </summary>
    [Fact]
    public async Task CreateTimer_DisposeAsync_PreventsFutureCallbacks ()
    {
        Instant initial = Instant.FromUtc(2025, 1, 1, 0, 0, 0);
        IPrimeTestClock clock = new PrimeTestClock(initial);
        TimeProvider provider = clock.ToTimeProvider();
        int fired = 0;

        ITimer timer = provider.CreateTimer(_ => fired++, null, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));

        clock.Advance(Duration.FromMinutes(5));
        fired.Should().Be(1);

        await timer.DisposeAsync();
        clock.Advance(Duration.FromMinutes(10));

        fired.Should().Be(1);
    }
    //----------------------------------------------------------------------------

    #endregion CreateTimer driven by test clock
}
//################################################################################

