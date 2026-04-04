// Copyright (c) Kevin Zehrer
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Threading;
using System.Threading.Tasks;

namespace KZDev.SystemClock.PrimeTime;

/// <summary>
///   Adapts an <see cref="IPrimeClock"/> to <see cref="TimeProvider"/> so that
///   components depending on <see cref="TimeProvider"/> (e.g. <see cref="TimeProvider.GetUtcNow"/>,
///   <see cref="TimeProvider.CreateTimer"/>) use the PrimeTime clock. When the clock is a
///   test clock (<see cref="IPrimeTestClock"/>), time and timers are driven by
///   <see cref="IPrimeTestClock.SetTime"/>, <see cref="IPrimeTestClock.Advance"/>,
///   and <see cref="IPrimeTestClock.RunFor"/> for deterministic tests.
/// </summary>
internal sealed class PrimeClockTimeProviderAdapter : TimeProvider
{
    #region Nested types

    private sealed class ClockIntervalTimerToITimerAdapter : ITimer
    {
        private readonly IClockIntervalTimer _registration;

        #region Constructors/Finalizers

        /// <summary>
        ///   Initializes a new instance of the <see cref="ClockIntervalTimerToITimerAdapter"/> class.
        /// </summary>
        /// <param name="registration">
        ///   The underlying PrimeTime interval timer registration.
        /// </param>
        /// <exception cref="ArgumentNullException">
        ///   <paramref name="registration"/> is <c>null</c>.
        /// </exception>
        internal ClockIntervalTimerToITimerAdapter (IClockIntervalTimer registration)
        {
            _registration = registration ?? throw new ArgumentNullException(nameof(registration));
        }

        #endregion Constructors/Finalizers

        #region Interface Implementations

        /// <inheritdoc />
        public bool Change (TimeSpan dueTime, TimeSpan period)
        {
            TimeSpan repeat = (period == Timeout.InfiniteTimeSpan || period < TimeSpan.Zero)
                ? Timeout.InfiniteTimeSpan
                : period;
            return _registration.Change(dueTime, repeat);
        }

        /// <inheritdoc />
        public void Dispose () => _registration.Dispose();

        /// <inheritdoc />
        public ValueTask DisposeAsync ()
        {
            _registration.Dispose();
            return default;
        }

        #endregion Interface Implementations
    }

    #endregion Nested types

    private readonly IPrimeClock _clock;

    #region Constructors/Finalizers

    /// <summary>
    ///   Initializes a new instance of the <see cref="PrimeClockTimeProviderAdapter"/> class.
    /// </summary>
    /// <param name="clock">
    ///   The PrimeTime BCL clock to use for time and timer operations.
    /// </param>
    /// <exception cref="ArgumentNullException">
    ///   <paramref name="clock"/> is <c>null</c>.
    /// </exception>
    public PrimeClockTimeProviderAdapter (IPrimeClock clock)
    {
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    #endregion Constructors/Finalizers

    #region Overrides

    /// <inheritdoc />
    public override DateTimeOffset GetUtcNow () => _clock.UtcNowOffset;

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

        TimeSpan repeatInterval = (period == Timeout.InfiniteTimeSpan || period < TimeSpan.Zero)
            ? Timeout.InfiniteTimeSpan
            : period;

        IClockIntervalTimer registration = _clock.RegisterTimer(dueTime,
            repeatInterval,
            () => callback(state),
            CancellationToken.None,
            timerOptions: null);

        return new ClockIntervalTimerToITimerAdapter(registration);
    }

    #endregion Overrides
}
