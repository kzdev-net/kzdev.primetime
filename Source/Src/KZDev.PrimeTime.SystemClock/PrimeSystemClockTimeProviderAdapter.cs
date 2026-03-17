// Copyright (c) Kevin Zehrer
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

// Phase 14: TimeProvider adapter for IPrimeSystemClock so code using TimeProvider gets
// time and timers from the PrimeTime clock (production or test).

using System.Threading;
using System.Threading.Tasks;

namespace KZDev.PrimeTime;

/// <summary>
///   Adapts an <see cref="IPrimeSystemClock"/> to <see cref="TimeProvider"/> so that
///   components depending on <see cref="TimeProvider"/> (e.g. <see cref="TimeProvider.GetUtcNow"/>,
///   <see cref="TimeProvider.CreateTimer"/>) use the PrimeTime clock. When the clock is a
///   test clock (<see cref="IPrimeTestSystemClock"/>), time and timers are driven by
///   <see cref="IPrimeTestSystemClock.SetTime"/>, <see cref="IPrimeTestSystemClock.Advance"/>,
///   and <see cref="IPrimeTestSystemClock.RunFor"/> for deterministic tests.
/// </summary>
internal sealed class PrimeSystemClockTimeProviderAdapter : TimeProvider
{
    private readonly IPrimeSystemClock _clock;

    /// <summary>
    ///   Initializes a new instance of the <see cref="PrimeSystemClockTimeProviderAdapter"/> class.
    /// </summary>
    /// <param name="clock">
    ///   The PrimeTime system clock to use for time and timer operations.
    /// </param>
    /// <exception cref="ArgumentNullException">
    ///   <paramref name="clock"/> is <c>null</c>.
    /// </exception>
    public PrimeSystemClockTimeProviderAdapter (IPrimeSystemClock clock)
    {
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    /// <inheritdoc />
    public override DateTimeOffset GetUtcNow () => _clock.UtcNow;

    /// <inheritdoc />
    public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Local;

    /// <inheritdoc />
    public override ITimer CreateTimer (
        TimerCallback callback,
        object? state,
        TimeSpan dueTime,
        TimeSpan period)
    {
        if (callback == null)
            throw new ArgumentNullException(nameof(callback));

        TimeSpan repeatInterval = (period == Timeout.InfiniteTimeSpan || period < TimeSpan.Zero)
            ? Timeout.InfiniteTimeSpan
            : period;

        IClockIntervalTimer registration = _clock.RegisterTimer(
            dueTime,
            repeatInterval,
            () => callback(state),
            timerOptions: null,
            CancellationToken.None);

        return new ClockIntervalTimerToITimerAdapter(registration);
    }

    private sealed class ClockIntervalTimerToITimerAdapter : ITimer
    {
        private readonly IClockIntervalTimer _registration;

        internal ClockIntervalTimerToITimerAdapter (IClockIntervalTimer registration)
        {
            _registration = registration;
        }

        public bool Change (TimeSpan dueTime, TimeSpan period)
        {
            TimeSpan repeat = (period == Timeout.InfiniteTimeSpan || period < TimeSpan.Zero)
                ? Timeout.InfiniteTimeSpan
                : period;
            return _registration.Change(dueTime, repeat);
        }

        public void Dispose () => _registration.Dispose();

        public ValueTask DisposeAsync ()
        {
            _registration.Dispose();
            return default;
        }
    }
}
