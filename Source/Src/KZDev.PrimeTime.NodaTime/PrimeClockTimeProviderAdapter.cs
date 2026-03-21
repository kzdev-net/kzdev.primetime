// Copyright (c) Kevin Zehrer
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

// Phase 14: TimeProvider adapter for IPrimeClock so code using TimeProvider gets
// time and timers from the PrimeTime NodaTime clock (production or test).

using System.Threading;
using System.Threading.Tasks;
using NodaTime;

namespace KZDev.PrimeTime;

/// <summary>
///   Adapts an <see cref="IPrimeClock"/> to <see cref="TimeProvider"/> so that
///   components depending on <see cref="TimeProvider"/> (e.g. <see cref="TimeProvider.GetUtcNow"/>,
///   <see cref="TimeProvider.CreateTimer"/>) use the PrimeTime NodaTime clock. When the clock
///   is a test clock (<see cref="IPrimeTestClock"/>), time and timers are driven by
///   <see cref="IPrimeTestClock.SetInstant"/>, <see cref="IPrimeTestClock.Advance"/>,
///   and <see cref="IPrimeTestClock.RunFor"/> for deterministic tests.
/// </summary>
internal sealed class PrimeClockTimeProviderAdapter : TimeProvider
{
    private readonly IPrimeClock _clock;

    /// <summary>
    ///   Initializes a new instance of the <see cref="PrimeClockTimeProviderAdapter"/> class.
    /// </summary>
    /// <param name="clock">
    ///   The PrimeTime NodaTime clock to use for time and timer operations.
    /// </param>
    /// <exception cref="ArgumentNullException">
    ///   <paramref name="clock"/> is <c>null</c>.
    /// </exception>
    public PrimeClockTimeProviderAdapter (IPrimeClock clock)
    {
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    /// <inheritdoc />
    public override DateTimeOffset GetUtcNow () => _clock.UtcNow.ToDateTimeOffset();

    /// <inheritdoc />
    public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Local;

    /// <inheritdoc />
    public override ITimer CreateTimer (TimerCallback callback,
        object? state,
        TimeSpan dueTime,
        TimeSpan period)
    {
        if (callback == null)
            throw new ArgumentNullException(nameof(callback));

        Duration due = Duration.FromTimeSpan(dueTime);
        Duration repeatInterval = (period == Timeout.InfiniteTimeSpan || period < TimeSpan.Zero)
            ? Duration.FromTimeSpan(Timeout.InfiniteTimeSpan)
            : Duration.FromTimeSpan(period);

        IPrimeClockTimerRegistration registration = _clock.RegisterTimer(due,
            repeatInterval,
            () => callback(state),
            CancellationToken.None,
            timerOptions: null);

        return new PrimeClockTimerToITimerAdapter(registration);
    }

    private sealed class PrimeClockTimerToITimerAdapter : ITimer
    {
        private readonly IPrimeClockTimerRegistration _registration;

        internal PrimeClockTimerToITimerAdapter (IPrimeClockTimerRegistration registration)
        {
            _registration = registration;
        }

        public bool Change (TimeSpan dueTime, TimeSpan period)
        {
            Duration next = Duration.FromTimeSpan(dueTime);
            Duration repeat = (period == Timeout.InfiniteTimeSpan || period < TimeSpan.Zero)
                ? Duration.FromTimeSpan(Timeout.InfiniteTimeSpan)
                : Duration.FromTimeSpan(period);
            return _registration.Change(next, repeat);
        }

        public void Dispose () => _registration.Dispose();

        public ValueTask DisposeAsync ()
        {
            _registration.Dispose();
            return default;
        }
    }
}
