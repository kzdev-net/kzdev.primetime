// Copyright (c) Kevin Zehrer. All rights reserved.
// This file is part of the PrimeTime project.

using NodaTime;

namespace KZDev.PrimeTime;

//################################################################################
/// <summary>
///   Adapts an <see cref="IPrimeClock"/> to <see cref="TimeProvider"/> so that
///   components depending on <see cref="TimeProvider"/> (e.g. <see cref="TimeProvider.GetUtcNow"/>,
///   <see cref="TimeProvider.CreateTimer"/>) use the PrimeTime NodaTime clock. When the clock
///   is a test clock (<see cref="IPrimeTestClock"/>), time and timers are driven by
///   <see cref="IPrimeTestClock.SetInstant"/>, <see cref="IPrimeTestClock.Advance(NodaTime.Duration)"/>,
///   and <see cref="IPrimeTestClock.RunFor(NodaTime.Duration)"/> for deterministic tests.
/// </summary>
internal sealed class PrimeClockTimeProviderAdapter : TimeProvider
{
    #region Nested types

    //============================================================================
    private sealed class PrimeClockTimerToITimerAdapter : ITimer
    {
        //------------------------------------------------------------------------
        private readonly IClockIntervalTimer _registration;
        //------------------------------------------------------------------------

        #region Constructors/Finalizers

        //------------------------------------------------------------------------
        /// <summary>
        ///   Initializes a new instance of the <see cref="PrimeClockTimerToITimerAdapter"/> class.
        /// </summary>
        /// <param name="registration">
        ///   The underlying PrimeTime interval timer registration.
        /// </param>
        internal PrimeClockTimerToITimerAdapter (IClockIntervalTimer registration)
        {
            _registration = registration;
        }
        //------------------------------------------------------------------------

        #endregion Constructors/Finalizers

        #region Interface Implementations

        //------------------------------------------------------------------------
        /// <inheritdoc />
        /// <exception cref="InvalidOperationException">
        ///   The underlying <see cref="IClockIntervalTimer"/> rejects the change (for example, converting a
        ///   one-shot registration to repeating).
        /// </exception>
        public bool Change (TimeSpan dueTime, TimeSpan period)
        {
            Duration next = Duration.FromTimeSpan(dueTime);
            Duration repeat = (period == Timeout.InfiniteTimeSpan || period < TimeSpan.Zero)
                ? Duration.FromTimeSpan(Timeout.InfiniteTimeSpan)
                : Duration.FromTimeSpan(period);
            return _registration.Change(next, repeat);
        }
        //------------------------------------------------------------------------

        //------------------------------------------------------------------------
        /// <inheritdoc />
        public void Dispose () => _registration.Dispose();
        //------------------------------------------------------------------------

        //------------------------------------------------------------------------
        /// <inheritdoc />
        public ValueTask DisposeAsync ()
        {
            _registration.Dispose();
            return default;
        }
        //------------------------------------------------------------------------

        #endregion Interface Implementations
    }
    //============================================================================

    #endregion Nested types

    //----------------------------------------------------------------------------
    private readonly IPrimeClock _clock;
    //----------------------------------------------------------------------------

    #region Constructors/Finalizers

    //----------------------------------------------------------------------------
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
    //----------------------------------------------------------------------------

    #endregion Constructors/Finalizers

    #region Overrides

    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public override DateTimeOffset GetUtcNow () => _clock.UtcNowInstant.ToDateTimeOffset();
    //----------------------------------------------------------------------------

    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public override TimeZoneInfo LocalTimeZone => _clock.LocalScheduleTimeZone;
    //----------------------------------------------------------------------------

    //----------------------------------------------------------------------------
    /// <inheritdoc />
    /// <exception cref="ArgumentNullException">
    ///   <paramref name="callback"/> is <c>null</c>.
    /// </exception>
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

        IClockIntervalTimer registration = _clock.RegisterTimer(due,
            repeatInterval,
            () => callback(state),
            CancellationToken.None,
            timerOptions: null);

        return new PrimeClockTimerToITimerAdapter(registration);
    }
    //----------------------------------------------------------------------------

    #endregion Overrides
}
//################################################################################
