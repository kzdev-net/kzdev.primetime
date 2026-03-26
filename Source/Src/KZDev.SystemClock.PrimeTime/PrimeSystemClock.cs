namespace KZDev.PrimeTime;

//################################################################################
/// <summary>
///   Production implementation of <see cref="IPrimeSystemClock"/> that delegates "now"
///   to a <see cref="TimeProvider"/> time source (e.g. <see cref="TimeProvider.System"/>),
///   analogous to the NodaTime stack using an IClock abstraction.
/// </summary>
/// <remarks>
///   Delay and time-cancellation members (Sleep, DelayAsync, GetTimeCancellationToken,
///   LinkTimeCancellationToken) delegate to the BCL (Thread.Sleep, Task.Delay,
///   CancellationTokenSource with a timer), so they use system time rather than the provider's
///   time. For deterministic tests, use <see cref="PrimeTestSystemClock"/> or <see cref="IPrimeTestSystemClock"/>.
/// </remarks>
internal sealed class PrimeSystemClock : IPrimeSystemClock
{
    private readonly TimeProvider _timeProvider;

    #region Constructors/Finalizers

    /// <summary>
    ///   Initializes a new instance of the <see cref="PrimeSystemClock"/> class using
    ///   <see cref="TimeProvider.System"/> as the time source.
    /// </summary>
    public PrimeSystemClock ()
        : this(TimeProvider.System)
    {
    }

    /// <summary>
    ///   Initializes a new instance of the <see cref="PrimeSystemClock"/> class with the
    ///   specified time provider.
    /// </summary>
    /// <param name="timeProvider">
    ///   The time provider used to obtain the current UTC and local time.
    /// </param>
    public PrimeSystemClock (TimeProvider timeProvider)
    {
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    #endregion Constructors/Finalizers

    #region Interface Implementations

    #region IPrimeSystemClock Implementation

    /// <inheritdoc />
    public DateTimeOffset LocalNow => _timeProvider.GetLocalNow();

    /// <inheritdoc />
    public DateTimeOffset UtcNow => _timeProvider.GetUtcNow();

    /// <inheritdoc />
    public DateTime LocalDateTimeNow => _timeProvider.GetLocalNow().LocalDateTime;

    /// <inheritdoc />
    public DateTime UtcDateTimeNow => _timeProvider.GetUtcNow().UtcDateTime;

#if NET
    /// <inheritdoc />
    public TimeOnly LocalNowTime => TimeOnly.FromDateTime(_timeProvider.GetLocalNow().DateTime);

    /// <inheritdoc />
    public TimeOnly UtcNowTime => TimeOnly.FromDateTime(_timeProvider.GetUtcNow().DateTime);

    /// <inheritdoc />
    public DateOnly LocalNowDate => DateOnly.FromDateTime(_timeProvider.GetLocalNow().DateTime);

    /// <inheritdoc />
    public DateOnly UtcNowDate => DateOnly.FromDateTime(_timeProvider.GetUtcNow().DateTime);
#endif

    #endregion IPrimeSystemClock Implementation

    #region IPrimeTime Implementation

    /// <inheritdoc />
    public void Sleep (TimeSpan sleepTime)
    {
        Thread.Sleep(sleepTime);
    }

    /// <inheritdoc />
    public void Sleep (int sleepMilliseconds)
    {
        Thread.Sleep(sleepMilliseconds);
    }

    /// <inheritdoc />
    public Task DelayAsync (TimeSpan delayTime)
    {
        return Task.Delay(delayTime);
    }

    /// <inheritdoc />
    public Task DelayAsync (int millisecondsDelay)
    {
        return Task.Delay(millisecondsDelay);
    }

    /// <inheritdoc />
    public Task DelayAsync (TimeSpan delayTime, CancellationToken cancellationToken)
    {
        return Task.Delay(delayTime, cancellationToken);
    }

    /// <inheritdoc />
    public Task DelayAsync (int millisecondsDelay, CancellationToken cancellationToken)
    {
        return Task.Delay(millisecondsDelay, cancellationToken);
    }

    /// <inheritdoc />
    public TimeCancellationTokenSource GetTimeCancellationToken (TimeSpan cancelTime)
    {
        CancellationTokenSource cts = new(cancelTime);
        return new TimeCancellationTokenSource(cts);
    }

    /// <inheritdoc />
    public TimeCancellationTokenSource GetTimeCancellationToken (int cancelMilliseconds)
    {
        CancellationTokenSource cts = new(cancelMilliseconds);
        return new TimeCancellationTokenSource(cts);
    }

    /// <inheritdoc />
    public TimeCancellationTokenSource LinkTimeCancellationToken (TimeSpan cancelTime, CancellationToken cancellationToken)
    {
        CancellationTokenSource timeCts = new(cancelTime);
        CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(timeCts.Token, cancellationToken);
        return new TimeCancellationTokenSource(linkedCts, [timeCts]);
    }

    /// <inheritdoc />
    public TimeCancellationTokenSource LinkTimeCancellationToken (int cancelMilliseconds, CancellationToken cancellationToken)
    {
        CancellationTokenSource timeCts = new(cancelMilliseconds);
        CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(timeCts.Token, cancellationToken);
        return new TimeCancellationTokenSource(linkedCts, [timeCts]);
    }

    /// <inheritdoc />
    public TimeCancellationTokenSource LinkTimeCancellationToken (int cancelMilliseconds, CancellationToken token1,
        CancellationToken token2)
    {
        CancellationTokenSource timeCts = new(cancelMilliseconds);
        CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(timeCts.Token, token1, token2);
        return new TimeCancellationTokenSource(linkedCts, [timeCts]);
    }

    /// <inheritdoc />
    public TimeCancellationTokenSource LinkTimeCancellationToken (TimeSpan cancelTime, CancellationToken token1,
        CancellationToken token2)
    {
        CancellationTokenSource timeCts = new(cancelTime);
        CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(timeCts.Token, token1, token2);
        return new TimeCancellationTokenSource(linkedCts, [timeCts]);
    }

    /// <inheritdoc />
    public TimeCancellationTokenSource LinkTimeCancellationToken (TimeSpan cancelTime, params CancellationToken[] cancellationTokens)
    {
        CancellationTokenSource timeCts = new(cancelTime);
        CancellationToken[] all = new CancellationToken[cancellationTokens.Length + 1];
        all[0] = timeCts.Token;
        Array.Copy(cancellationTokens, 0, all, 1, cancellationTokens.Length);
        CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(all);
        return new TimeCancellationTokenSource(linkedCts, [timeCts]);
    }

    /// <inheritdoc />
    public TimeCancellationTokenSource LinkTimeCancellationToken (int cancelMilliseconds,
        params CancellationToken[] cancellationTokens)
    {
        CancellationTokenSource timeCts = new(cancelMilliseconds);
        CancellationToken[] all = new CancellationToken[cancellationTokens.Length + 1];
        all[0] = timeCts.Token;
        Array.Copy(cancellationTokens, 0, all, 1, cancellationTokens.Length);
        CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(all);
        return new TimeCancellationTokenSource(linkedCts, [timeCts]);
    }

    #endregion IPrimeTime Implementation

    #region IPrimeSystemClock — Interval timers

    /// <inheritdoc />
    public IClockIntervalTimer RegisterTimer (TimeSpan callbackTime,
        Action callback,
        CancellationToken cancellationToken,
        bool repeat = false,
        IntervalTimerOptions? timerOptions = null) =>
        new ClockIntervalTimerRegistration(this,
            callbackTime,
            repeat ? callbackTime : Timeout.InfiniteTimeSpan,
            IntervalTimerCallbackKind.SimpleAction,
            callback,
            null,
            timerOptions,
            cancellationToken);

    /// <inheritdoc />
    public IClockIntervalTimer RegisterTimer (TimeSpan callbackTime,
        Action<ClockTimerCallbackContext> callback,
        CancellationToken cancellationToken,
        object? state = null,
        bool repeat = false,
        IntervalTimerOptions? timerOptions = null) =>
        new ClockIntervalTimerRegistration(this,
            callbackTime,
            repeat ? callbackTime : Timeout.InfiniteTimeSpan,
            IntervalTimerCallbackKind.ContextAction,
            callback,
            state,
            timerOptions,
            cancellationToken);

    /// <inheritdoc />
    public IClockIntervalTimer RegisterTimer (TimeSpan callbackTime,
        Action<ClockTimerCallbackContext, CancellationToken> callback,
        CancellationToken cancellationToken,
        object? state = null,
        bool repeat = false,
        IntervalTimerOptions? timerOptions = null) =>
        new ClockIntervalTimerRegistration(this,
            callbackTime,
            repeat ? callbackTime : Timeout.InfiniteTimeSpan,
            IntervalTimerCallbackKind.ContextActionWithToken,
            callback,
            state,
            timerOptions,
            cancellationToken);

    /// <inheritdoc />
    public IClockIntervalTimer RegisterAsyncTimer (TimeSpan callbackTime,
        Func<CancellationToken, ValueTask> callback,
        CancellationToken cancellationToken,
        bool repeat = false,
        IntervalTimerOptions? timerOptions = null) =>
        new ClockIntervalTimerRegistration(this,
            callbackTime,
            repeat ? callbackTime : Timeout.InfiniteTimeSpan,
            IntervalTimerCallbackKind.SimpleAsync,
            callback,
            null,
            timerOptions,
            cancellationToken);

    /// <inheritdoc />
    public IClockIntervalTimer RegisterAsyncTimer (TimeSpan callbackTime,
        Func<ClockTimerCallbackContext, CancellationToken, ValueTask> callback,
        CancellationToken cancellationToken,
        object? state = null,
        bool repeat = false,
        IntervalTimerOptions? timerOptions = null) =>
        new ClockIntervalTimerRegistration(this,
            callbackTime,
            repeat ? callbackTime : Timeout.InfiniteTimeSpan,
            IntervalTimerCallbackKind.ContextAsync,
            callback,
            state,
            timerOptions,
            cancellationToken);

    /// <inheritdoc />
    public IClockIntervalTimer RegisterTimer (TimeSpan callbackTime,
        TimeSpan repeatInterval,
        Action callback,
        CancellationToken cancellationToken,
        IntervalTimerOptions? timerOptions = null) =>
        new ClockIntervalTimerRegistration(this,
            callbackTime,
            repeatInterval,
            IntervalTimerCallbackKind.SimpleAction,
            callback,
            null,
            timerOptions,
            cancellationToken);

    /// <inheritdoc />
    public IClockIntervalTimer RegisterTimer (TimeSpan callbackTime,
        TimeSpan repeatInterval,
        Action<ClockTimerCallbackContext> callback,
        CancellationToken cancellationToken,
        object? state = null,
        IntervalTimerOptions? timerOptions = null) =>
        new ClockIntervalTimerRegistration(this,
            callbackTime,
            repeatInterval,
            IntervalTimerCallbackKind.ContextAction,
            callback,
            state,
            timerOptions,
            cancellationToken);

    /// <inheritdoc />
    public IClockIntervalTimer RegisterTimer (TimeSpan callbackTime,
        TimeSpan repeatInterval,
        Action<ClockTimerCallbackContext, CancellationToken> callback,
        CancellationToken cancellationToken,
        object? state = null,
        IntervalTimerOptions? timerOptions = null) =>
        new ClockIntervalTimerRegistration(this,
            callbackTime,
            repeatInterval,
            IntervalTimerCallbackKind.ContextActionWithToken,
            callback,
            state,
            timerOptions,
            cancellationToken);

    /// <inheritdoc />
    public IClockIntervalTimer RegisterAsyncTimer (TimeSpan callbackTime,
        TimeSpan repeatInterval,
        Func<CancellationToken, ValueTask> callback,
        CancellationToken cancellationToken,
        IntervalTimerOptions? timerOptions = null) =>
        new ClockIntervalTimerRegistration(this,
            callbackTime,
            repeatInterval,
            IntervalTimerCallbackKind.SimpleAsync,
            callback,
            null,
            timerOptions,
            cancellationToken);

    /// <inheritdoc />
    public IClockIntervalTimer RegisterAsyncTimer (TimeSpan callbackTime,
        TimeSpan repeatInterval,
        Func<ClockTimerCallbackContext, CancellationToken, ValueTask> callback,
        CancellationToken cancellationToken,
        object? state = null,
        IntervalTimerOptions? timerOptions = null) =>
        new ClockIntervalTimerRegistration(this,
            callbackTime,
            repeatInterval,
            IntervalTimerCallbackKind.ContextAsync,
            callback,
            state,
            timerOptions,
            cancellationToken);

    #endregion IPrimeSystemClock — Interval timers

#if NET
    #region IPrimeSystemClock — Day-time timers

    /// <inheritdoc />
    public IClockDayTimeTimer RegisterTimeOfDay (LocalTimeOfDay timeOfDay,
        Action callback,
        CancellationToken cancellationToken,
        DayTimeTimerOptions? timerOptions = null) =>
        new ClockDayTimeTimerRegistration(this,
            timeOfDay,
            IntervalTimerCallbackKind.SimpleAction,
            callback,
            null,
            timerOptions,
            cancellationToken);

    /// <inheritdoc />
    public IClockDayTimeTimer RegisterTimeOfDay (LocalTimeOfDay timeOfDay,
        Action<ClockTimerCallbackContext> callback,
        CancellationToken cancellationToken,
        object? state = null,
        DayTimeTimerOptions? timerOptions = null) =>
        new ClockDayTimeTimerRegistration(this,
            timeOfDay,
            IntervalTimerCallbackKind.ContextAction,
            callback,
            state,
            timerOptions,
            cancellationToken);

    /// <inheritdoc />
    public IClockDayTimeTimer RegisterTimeOfDay (LocalTimeOfDay timeOfDay,
        Action<ClockTimerCallbackContext, CancellationToken> callback,
        CancellationToken cancellationToken,
        object? state = null,
        DayTimeTimerOptions? timerOptions = null) =>
        new ClockDayTimeTimerRegistration(this,
            timeOfDay,
            IntervalTimerCallbackKind.ContextActionWithToken,
            callback,
            state,
            timerOptions,
            cancellationToken);

    /// <inheritdoc />
    public IClockDayTimeTimer RegisterAsyncTimeOfDay (LocalTimeOfDay timeOfDay,
        Func<CancellationToken, ValueTask> callback,
        CancellationToken cancellationToken,
        DayTimeTimerOptions? timerOptions = null) =>
        new ClockDayTimeTimerRegistration(this,
            timeOfDay,
            IntervalTimerCallbackKind.SimpleAsync,
            callback,
            null,
            timerOptions,
            cancellationToken);

    /// <inheritdoc />
    public IClockDayTimeTimer RegisterAsyncTimeOfDay (LocalTimeOfDay timeOfDay,
        Func<ClockTimerCallbackContext, CancellationToken, ValueTask> callback,
        CancellationToken cancellationToken,
        object? state = null,
        DayTimeTimerOptions? timerOptions = null) =>
        new ClockDayTimeTimerRegistration(this,
            timeOfDay,
            IntervalTimerCallbackKind.ContextAsync,
            callback,
            state,
            timerOptions,
            cancellationToken);

    /// <inheritdoc />
    public IClockDayTimeTimer RegisterTimeOfDay (UtcTimeOfDay timeOfDay,
        Action callback,
        CancellationToken cancellationToken,
        DayTimeTimerOptions? timerOptions = null) =>
        new ClockDayTimeTimerRegistration(this,
            timeOfDay,
            IntervalTimerCallbackKind.SimpleAction,
            callback,
            null,
            timerOptions,
            cancellationToken);

    /// <inheritdoc />
    public IClockDayTimeTimer RegisterTimeOfDay (UtcTimeOfDay timeOfDay,
        Action<ClockTimerCallbackContext> callback,
        CancellationToken cancellationToken,
        object? state = null,
        DayTimeTimerOptions? timerOptions = null) =>
        new ClockDayTimeTimerRegistration(this,
            timeOfDay,
            IntervalTimerCallbackKind.ContextAction,
            callback,
            state,
            timerOptions,
            cancellationToken);

    /// <inheritdoc />
    public IClockDayTimeTimer RegisterTimeOfDay (UtcTimeOfDay timeOfDay,
        Action<ClockTimerCallbackContext, CancellationToken> callback,
        CancellationToken cancellationToken,
        object? state = null,
        DayTimeTimerOptions? timerOptions = null) =>
        new ClockDayTimeTimerRegistration(this,
            timeOfDay,
            IntervalTimerCallbackKind.ContextActionWithToken,
            callback,
            state,
            timerOptions,
            cancellationToken);

    /// <inheritdoc />
    public IClockDayTimeTimer RegisterAsyncTimeOfDay (UtcTimeOfDay timeOfDay,
        Func<CancellationToken, ValueTask> callback,
        CancellationToken cancellationToken,
        DayTimeTimerOptions? timerOptions = null) =>
        new ClockDayTimeTimerRegistration(this,
            timeOfDay,
            IntervalTimerCallbackKind.SimpleAsync,
            callback,
            null,
            timerOptions,
            cancellationToken);

    /// <inheritdoc />
    public IClockDayTimeTimer RegisterAsyncTimeOfDay (UtcTimeOfDay timeOfDay,
        Func<ClockTimerCallbackContext, CancellationToken, ValueTask> callback,
        CancellationToken cancellationToken,
        object? state = null,
        DayTimeTimerOptions? timerOptions = null) =>
        new ClockDayTimeTimerRegistration(this,
            timeOfDay,
            IntervalTimerCallbackKind.ContextAsync,
            callback,
            state,
            timerOptions,
            cancellationToken);

    #endregion IPrimeSystemClock — Day-time timers
#endif

    #endregion Interface Implementations
}
//################################################################################
