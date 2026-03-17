using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using NodaTime;
using NodaTime.TimeZones;

namespace KZDev.PrimeTime;

/// <summary>
///   Test clock implementation of <see cref="IPrimeTestClock"/> that maintains virtual
///   time. All "now" values, delays, time-based cancellation, and timers are driven
///   by this virtual time so tests can advance time deterministically.
/// </summary>
public sealed class PrimeTestClock : IPrimeTestClock
{
    private static int _nextTimerId;
#if NET10_OR_GREATER
    private readonly Lock _gate = new();
#else
    private readonly object _gate = new();
#endif
    private Instant _now;
    private readonly DateTimeZone _zone;
    private bool _isRunning;
    private Thread? _runThread;
    private Duration _runRate = Duration.FromSeconds(1);
    private readonly List<PendingDelay> _pendingDelays = [];
    private readonly List<TimeExpiryEntry> _timeExpiryEntries = [];
    private readonly List<VirtualIntervalTimerBase> _intervalTimers = [];
    private readonly List<VirtualDayTimeTimerBase> _dayTimeTimers = [];

    /// <summary>
    ///   Occurs when the clock's current time has changed.
    /// </summary>
    public event EventHandler<NodaClockTimeChangedEventArgs>? ClockEvents;

    /// <summary>
    ///   Initializes a new instance of the <see cref="PrimeTestClock"/> class with
    ///   virtual time set to the current system instant at construction.
    /// </summary>
    public PrimeTestClock ()
    {
        _now = SystemClock.Instance.GetCurrentInstant();
        _zone = GetSystemDefaultTimeZone();
    }

    /// <summary>
    ///   Initializes a new instance of the <see cref="PrimeTestClock"/> class with
    ///   the specified initial instant and system default time zone.
    /// </summary>
    /// <param name="initialInstant">
    ///   The initial virtual instant.
    /// </param>
    public PrimeTestClock (Instant initialInstant)
    {
        _now = initialInstant;
        _zone = GetSystemDefaultTimeZone();
    }

    /// <summary>
    ///   Initializes a new instance of the <see cref="PrimeTestClock"/> class with
    ///   the specified initial instant and time zone.
    /// </summary>
    /// <param name="initialInstant">
    ///   The initial virtual instant.
    /// </param>
    /// <param name="zone">
    ///   The time zone used for local "now" values.
    /// </param>
    public PrimeTestClock (Instant initialInstant, DateTimeZone zone)
    {
        _now = initialInstant;
        _zone = zone ?? throw new ArgumentNullException(nameof(zone));
    }

    private static DateTimeZone GetSystemDefaultTimeZone ()
    {
        try
        {
            return DateTimeZoneProviders.Bcl.GetSystemDefault();
        }
        catch (DateTimeZoneNotFoundException)
        {
            return BclDateTimeZone.ForSystemDefault();
        }
    }

    #region IPrimeTestClock Implementation

    /// <inheritdoc />
    public void SetInstant (Instant instant)
    {
        lock (_gate)
        {
            _now = instant;
        }

        RaiseClockEvents(instant);
    }

    /// <inheritdoc />
    public void SetTime (DateTimeOffset utcTime)
    {
        SetInstant(Instant.FromDateTimeUtc(utcTime.UtcDateTime));
    }

    /// <inheritdoc />
    public void SetLocalTime (LocalDateTime localDateTime)
    {
        SetInstant(localDateTime.InZoneLeniently(_zone).ToInstant());
    }

    /// <inheritdoc />
    public void Advance (Duration duration)
    {
        if (duration < Duration.Zero)
            duration = Duration.Zero;

        Instant newNow;
        List<PendingDelay>? toComplete = null;
        List<TimeExpiryEntry>? toCancel = null;
        List<VirtualIntervalTimerBase>? intervalDue = null;
        List<VirtualDayTimeTimerBase>? dayTimeDue = null;

        lock (_gate)
        {
            _now += duration;
            newNow = _now;

            foreach (PendingDelay pd in _pendingDelays)
            {
                if (pd.DueInstant <= newNow)
                {
                    toComplete ??= [];
                    toComplete.Add(pd);
                }
            }

            if (toComplete != null)
            {
                foreach (PendingDelay pd in toComplete)
                    _pendingDelays.Remove(pd);
            }

            foreach (TimeExpiryEntry tee in _timeExpiryEntries)
            {
                if (tee.ExpireInstant <= newNow)
                {
                    toCancel ??= [];
                    toCancel.Add(tee);
                }
            }

            if (toCancel != null)
            {
                foreach (TimeExpiryEntry tee in toCancel)
                    _timeExpiryEntries.Remove(tee);
            }

            foreach (VirtualIntervalTimerBase t in _intervalTimers)
            {
                if (t.IsDue(newNow))
                {
                    intervalDue ??= [];
                    intervalDue.Add(t);
                }
            }

            foreach (VirtualDayTimeTimerBase t in _dayTimeTimers)
            {
                if (t.IsDue(newNow))
                {
                    dayTimeDue ??= [];
                    dayTimeDue.Add(t);
                }
            }
        }

        if (toComplete != null)
        {
            foreach (PendingDelay pd in toComplete)
                pd.Complete();
        }

        if (toCancel != null)
        {
            foreach (TimeExpiryEntry tee in toCancel)
                tee.Cancel();
        }

        while (intervalDue != null && intervalDue.Count > 0)
        {
            foreach (VirtualIntervalTimerBase t in intervalDue)
                t.RunDueCallback(newNow);

            intervalDue = null;
            lock (_gate)
            {
                foreach (VirtualIntervalTimerBase t in _intervalTimers)
                {
                    if (t.IsDue(newNow))
                    {
                        intervalDue ??= [];
                        intervalDue.Add(t);
                    }
                }
            }
        }

        while (dayTimeDue != null && dayTimeDue.Count > 0)
        {
            foreach (VirtualDayTimeTimerBase t in dayTimeDue)
                t.RunDueCallback(newNow);

            dayTimeDue = null;
            lock (_gate)
            {
                foreach (VirtualDayTimeTimerBase t in _dayTimeTimers)
                {
                    if (t.IsDue(newNow))
                    {
                        dayTimeDue ??= [];
                        dayTimeDue.Add(t);
                    }
                }
            }
        }

        RaiseClockEvents(newNow);
    }

    /// <inheritdoc />
    public void RunFor (Duration duration)
    {
        Advance(duration);
    }

    /// <inheritdoc />
    public void Start (Duration? rate = null)
    {
        lock (_gate)
        {
            if (_isRunning)
                return;
            _isRunning = true;
            _runRate = rate ?? Duration.FromSeconds(1);
        }

        _runThread = new Thread(RunLoop)
        {
            IsBackground = true
        };
        _runThread.Start();
    }

    /// <inheritdoc />
    public bool Stop ()
    {
        lock (_gate)
        {
            if (!_isRunning)
                return false;
            _isRunning = false;
        }

        _runThread?.Join(TimeSpan.FromSeconds(5));
        _runThread = null;
        return true;
    }

    #endregion IPrimeTestClock Implementation

    #region IPrimeTestTime Implementation

    /// <inheritdoc />
    public bool IsRunning
    {
        get
        {
            lock (_gate)
                return _isRunning;
        }
    }

    #endregion IPrimeTestTime Implementation

    #region IPrimeClock Implementation — Now

    /// <inheritdoc />
    public Instant Instant
    {
        get
        {
            lock (_gate)
                return _now;
        }
    }

    /// <inheritdoc />
    public ZonedDateTime UtcNow
    {
        get
        {
            lock (_gate)
                return _now.InUtc();
        }
    }

    /// <inheritdoc />
    public ZonedDateTime LocalZonedNow
    {
        get
        {
            lock (_gate)
                return _now.InZone(_zone);
        }
    }

    /// <inheritdoc />
    public ZonedDateTime UtcZonedNow => UtcNow;

    /// <inheritdoc />
    public LocalDateTime LocalNow => LocalZonedNow.LocalDateTime;

    /// <inheritdoc />
    public LocalTime LocalNowTime => LocalZonedNow.TimeOfDay;

    /// <inheritdoc />
    public LocalTime UtcNowTime => UtcNow.TimeOfDay;

    /// <inheritdoc />
    public LocalDate LocalNowDate => LocalZonedNow.Date;

    /// <inheritdoc />
    public LocalDate UtcNowDate => UtcNow.Date;

    #endregion IPrimeClock Implementation — Now

    #region IPrimeTime Implementation — Delays (TimeSpan/int)

    /// <inheritdoc />
    public void Sleep (TimeSpan sleepTime) => Sleep(Duration.FromTimeSpan(sleepTime));

    /// <inheritdoc />
    public void Sleep (int sleepMilliseconds) => Sleep(Duration.FromMilliseconds(sleepMilliseconds));

    /// <inheritdoc />
    public Task DelayAsync (TimeSpan delayTime) => DelayAsync(Duration.FromTimeSpan(delayTime));

    /// <inheritdoc />
    public Task DelayAsync (int millisecondsDelay) =>
        DelayAsync(Duration.FromMilliseconds(millisecondsDelay));

    /// <inheritdoc />
    public Task DelayAsync (TimeSpan delayTime, CancellationToken cancellationToken) =>
        DelayAsync(Duration.FromTimeSpan(delayTime), cancellationToken);

    /// <inheritdoc />
    public Task DelayAsync (int millisecondsDelay, CancellationToken cancellationToken) =>
        DelayAsync(Duration.FromMilliseconds(millisecondsDelay), cancellationToken);

    #endregion IPrimeTime Implementation — Delays (TimeSpan/int)

    #region IPrimeClock Implementation — Delays (Duration)

    /// <inheritdoc />
    public void Sleep (Duration duration)
    {
        if (duration <= Duration.Zero)
            return;

        Instant dueInstant;
        TaskCompletionSource<bool> tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);

        lock (_gate)
        {
            dueInstant = _now + duration;
            _pendingDelays.Add(new PendingDelay(dueInstant, tcs, null));
        }

        tcs.Task.GetAwaiter().GetResult();
    }

    /// <inheritdoc />
    public Task DelayAsync (Duration duration)
    {
        return DelayAsync(duration, CancellationToken.None);
    }

    /// <inheritdoc />
    public Task DelayAsync (Duration duration, CancellationToken cancellationToken)
    {
        if (duration <= Duration.Zero)
            return Task.CompletedTask;

        Instant dueInstant;
        TaskCompletionSource<bool> tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);

        lock (_gate)
        {
            dueInstant = _now + duration;
            _pendingDelays.Add(new PendingDelay(dueInstant, tcs, cancellationToken));
        }

        if (cancellationToken.CanBeCanceled)
        {
            CancellationTokenRegistration reg = cancellationToken.Register(() =>
            {
                lock (_gate)
                {
                    if (_pendingDelays.RemoveAll(p => p.TaskCompletionSource == tcs) > 0)
                        tcs.TrySetCanceled(cancellationToken);
                }
            });

            _ = tcs.Task.ContinueWith(
                (_, r) => ((CancellationTokenRegistration)r!).Dispose(),
                reg,
                CancellationToken.None,
                TaskContinuationOptions.None,
                TaskScheduler.Default);
            return tcs.Task;
        }

        return tcs.Task;
    }

    #endregion IPrimeClock Implementation — Delays (Duration)

    #region IPrimeClock Implementation — Time cancellation (Duration)

    /// <inheritdoc />
    public TimeCancellationTokenSource GetTimeCancellationToken (Duration cancelAfter)
    {
        CancellationTokenSource cts = new();
        TimeCancellationTokenSource wrapper = new(cts);

        lock (_gate)
        {
            Instant expireInstant = _now + cancelAfter;
            _timeExpiryEntries.Add(new TimeExpiryEntry(expireInstant, wrapper));
        }

        return wrapper;
    }

    /// <inheritdoc />
    public TimeCancellationTokenSource LinkTimeCancellationToken (
        Duration cancelAfter, CancellationToken cancellationToken)
    {
        CancellationTokenSource timeCts = new();
        CancellationTokenSource linked =
            CancellationTokenSource.CreateLinkedTokenSource(timeCts.Token, cancellationToken);
        TimeCancellationTokenSource wrapper = new(linked, [timeCts]);

        lock (_gate)
        {
            Instant expireInstant = _now + cancelAfter;
            _timeExpiryEntries.Add(new TimeExpiryEntry(expireInstant, wrapper, timeCts));
        }

        return wrapper;
    }

    /// <inheritdoc />
    public TimeCancellationTokenSource LinkTimeCancellationToken (
        Duration cancelAfter, CancellationToken token1, CancellationToken token2)
    {
        CancellationTokenSource timeCts = new();
        CancellationTokenSource linked =
            CancellationTokenSource.CreateLinkedTokenSource(timeCts.Token, token1, token2);
        TimeCancellationTokenSource wrapper = new(linked, [timeCts]);

        lock (_gate)
        {
            Instant expireInstant = _now + cancelAfter;
            _timeExpiryEntries.Add(new TimeExpiryEntry(expireInstant, wrapper, timeCts));
        }

        return wrapper;
    }

    /// <inheritdoc />
    public TimeCancellationTokenSource LinkTimeCancellationToken (
        Duration cancelAfter, params CancellationToken[] cancellationTokens)
    {
        CancellationTokenSource timeCts = new();
        CancellationTokenSource linked =
            CancellationTokenSource.CreateLinkedTokenSource([timeCts.Token, .. cancellationTokens]);
        TimeCancellationTokenSource wrapper = new(linked, [timeCts]);

        lock (_gate)
        {
            Instant expireInstant = _now + cancelAfter;
            _timeExpiryEntries.Add(new TimeExpiryEntry(expireInstant, wrapper, timeCts));
        }

        return wrapper;
    }

    #endregion IPrimeClock Implementation — Time cancellation (Duration)

    #region IPrimeTime Implementation — Time cancellation (TimeSpan/int)

    /// <inheritdoc />
    public TimeCancellationTokenSource GetTimeCancellationToken (TimeSpan cancelTime) =>
        GetTimeCancellationToken(Duration.FromTimeSpan(cancelTime));

    /// <inheritdoc />
    public TimeCancellationTokenSource GetTimeCancellationToken (int cancelMilliseconds) =>
        GetTimeCancellationToken(Duration.FromMilliseconds(cancelMilliseconds));

    /// <inheritdoc />
    public TimeCancellationTokenSource LinkTimeCancellationToken (
        TimeSpan cancelTime, CancellationToken cancellationToken) =>
        LinkTimeCancellationToken(Duration.FromTimeSpan(cancelTime), cancellationToken);

    /// <inheritdoc />
    public TimeCancellationTokenSource LinkTimeCancellationToken (
        int cancelMilliseconds, CancellationToken cancellationToken) =>
        LinkTimeCancellationToken(Duration.FromMilliseconds(cancelMilliseconds), cancellationToken);

    /// <inheritdoc />
    public TimeCancellationTokenSource LinkTimeCancellationToken (
        int cancelMilliseconds, CancellationToken token1, CancellationToken token2)
    {
        return LinkTimeCancellationToken(
            Duration.FromMilliseconds(cancelMilliseconds), token1, token2);
    }

    /// <inheritdoc />
    public TimeCancellationTokenSource LinkTimeCancellationToken (
        TimeSpan cancelTime, CancellationToken token1, CancellationToken token2)
    {
        return LinkTimeCancellationToken(Duration.FromTimeSpan(cancelTime), token1, token2);
    }

    /// <inheritdoc />
    public TimeCancellationTokenSource LinkTimeCancellationToken (
        TimeSpan cancelTime, params CancellationToken[] cancellationTokens)
    {
        return LinkTimeCancellationToken(Duration.FromTimeSpan(cancelTime), cancellationTokens);
    }

    /// <inheritdoc />
    public TimeCancellationTokenSource LinkTimeCancellationToken (
        int cancelMilliseconds, params CancellationToken[] cancellationTokens)
    {
        return LinkTimeCancellationToken(
            Duration.FromMilliseconds(cancelMilliseconds), cancellationTokens);
    }

    #endregion IPrimeTime Implementation — Time cancellation (TimeSpan/int)

    #region IPrimeClock Implementation — Interval timers

    private static readonly Duration NoRepeatSentinel = Duration.FromTimeSpan(Timeout.InfiniteTimeSpan);

    /// <inheritdoc />
    public IPrimeClockTimerRegistration RegisterTimer (Duration callbackTime,
        Action callback,
        bool repeat = false,
        IntervalTimerOptions? timerOptions = null,
        CancellationToken cancellationToken = default)
    {
        VirtualIntervalTimerBase t = new VirtualIntervalTimer(
            this,
            callbackTime,
            repeat ? callbackTime : NoRepeatSentinel,
            PrimeClockIntervalTimerCallbackKind.SimpleAction,
            callback,
            null,
            timerOptions,
            cancellationToken);
        lock (_gate)
            _intervalTimers.Add(t);
        return t;
    }

    /// <inheritdoc />
    public IPrimeClockTimerRegistration RegisterTimer (Duration callbackTime,
        Action<PrimeClockTimerCallbackContext> callback,
        object? state = null,
        bool repeat = false,
        IntervalTimerOptions? timerOptions = null,
        CancellationToken cancellationToken = default)
    {
        VirtualIntervalTimerBase t = new VirtualIntervalTimer(
            this,
            callbackTime,
            repeat ? callbackTime : NoRepeatSentinel,
            PrimeClockIntervalTimerCallbackKind.ContextAction,
            callback,
            state,
            timerOptions,
            cancellationToken);
        lock (_gate)
            _intervalTimers.Add(t);
        return t;
    }

    /// <inheritdoc />
    public IPrimeClockTimerRegistration RegisterTimer (Duration callbackTime,
        Action<PrimeClockTimerCallbackContext, CancellationToken> callback,
        object? state = null,
        bool repeat = false,
        IntervalTimerOptions? timerOptions = null,
        CancellationToken cancellationToken = default)
    {
        VirtualIntervalTimerBase t = new VirtualIntervalTimer(
            this,
            callbackTime,
            repeat ? callbackTime : NoRepeatSentinel,
            PrimeClockIntervalTimerCallbackKind.ContextActionWithToken,
            callback,
            state,
            timerOptions,
            cancellationToken);
        lock (_gate)
            _intervalTimers.Add(t);
        return t;
    }

    /// <inheritdoc />
    public IPrimeClockTimerRegistration RegisterAsyncTimer (Duration callbackTime,
        Func<CancellationToken, ValueTask> callback,
        bool repeat = false,
        IntervalTimerOptions? timerOptions = null,
        CancellationToken cancellationToken = default)
    {
        VirtualIntervalTimerBase t = new VirtualIntervalTimer(
            this,
            callbackTime,
            repeat ? callbackTime : NoRepeatSentinel,
            PrimeClockIntervalTimerCallbackKind.SimpleAsync,
            callback,
            null,
            timerOptions,
            cancellationToken);
        lock (_gate)
            _intervalTimers.Add(t);
        return t;
    }

    /// <inheritdoc />
    public IPrimeClockTimerRegistration RegisterAsyncTimer (Duration callbackTime,
        Func<PrimeClockTimerCallbackContext, CancellationToken, ValueTask> callback,
        object? state = null,
        bool repeat = false,
        IntervalTimerOptions? timerOptions = null,
        CancellationToken cancellationToken = default)
    {
        VirtualIntervalTimerBase t = new VirtualIntervalTimer(
            this,
            callbackTime,
            repeat ? callbackTime : NoRepeatSentinel,
            PrimeClockIntervalTimerCallbackKind.ContextAsync,
            callback,
            state,
            timerOptions,
            cancellationToken);
        lock (_gate)
            _intervalTimers.Add(t);
        return t;
    }

    /// <inheritdoc />
    public IPrimeClockTimerRegistration RegisterTimer (Duration callbackTime,
        Duration repeatInterval,
        Action callback,
        IntervalTimerOptions? timerOptions = null,
        CancellationToken cancellationToken = default)
    {
        VirtualIntervalTimerBase t = new VirtualIntervalTimer(
            this,
            callbackTime,
            repeatInterval,
            PrimeClockIntervalTimerCallbackKind.SimpleAction,
            callback,
            null,
            timerOptions,
            cancellationToken);
        lock (_gate)
            _intervalTimers.Add(t);
        return t;
    }

    /// <inheritdoc />
    public IPrimeClockTimerRegistration RegisterTimer (Duration callbackTime,
        Duration repeatInterval,
        Action<PrimeClockTimerCallbackContext> callback,
        object? state = null,
        IntervalTimerOptions? timerOptions = null,
        CancellationToken cancellationToken = default)
    {
        VirtualIntervalTimerBase t = new VirtualIntervalTimer(
            this,
            callbackTime,
            repeatInterval,
            PrimeClockIntervalTimerCallbackKind.ContextAction,
            callback,
            state,
            timerOptions,
            cancellationToken);
        lock (_gate)
            _intervalTimers.Add(t);
        return t;
    }

    /// <inheritdoc />
    public IPrimeClockTimerRegistration RegisterTimer (Duration callbackTime,
        Duration repeatInterval,
        Action<PrimeClockTimerCallbackContext, CancellationToken> callback,
        object? state = null,
        IntervalTimerOptions? timerOptions = null,
        CancellationToken cancellationToken = default)
    {
        VirtualIntervalTimerBase t = new VirtualIntervalTimer(
            this,
            callbackTime,
            repeatInterval,
            PrimeClockIntervalTimerCallbackKind.ContextActionWithToken,
            callback,
            state,
            timerOptions,
            cancellationToken);
        lock (_gate)
            _intervalTimers.Add(t);
        return t;
    }

    /// <inheritdoc />
    public IPrimeClockTimerRegistration RegisterAsyncTimer (Duration callbackTime,
        Duration repeatInterval,
        Func<CancellationToken, ValueTask> callback,
        IntervalTimerOptions? timerOptions = null,
        CancellationToken cancellationToken = default)
    {
        VirtualIntervalTimerBase t = new VirtualIntervalTimer(
            this,
            callbackTime,
            repeatInterval,
            PrimeClockIntervalTimerCallbackKind.SimpleAsync,
            callback,
            null,
            timerOptions,
            cancellationToken);
        lock (_gate)
            _intervalTimers.Add(t);
        return t;
    }

    /// <inheritdoc />
    public IPrimeClockTimerRegistration RegisterAsyncTimer (Duration callbackTime,
        Duration repeatInterval,
        Func<PrimeClockTimerCallbackContext, CancellationToken, ValueTask> callback,
        object? state = null,
        IntervalTimerOptions? timerOptions = null,
        CancellationToken cancellationToken = default)
    {
        VirtualIntervalTimerBase t = new VirtualIntervalTimer(
            this,
            callbackTime,
            repeatInterval,
            PrimeClockIntervalTimerCallbackKind.ContextAsync,
            callback,
            state,
            timerOptions,
            cancellationToken);
        lock (_gate)
            _intervalTimers.Add(t);
        return t;
    }

    #endregion IPrimeClock Implementation — Interval timers

    #region IPrimeClock Implementation — Time-of-day timers

    /// <inheritdoc />
    public IPrimeClockTimerRegistration RegisterTimeOfDay (LocalTime timeOfDay,
        Action callback,
        DayTimeTimerOptions? timerOptions = null,
        CancellationToken cancellationToken = default)
    {
        VirtualDayTimeTimerBase t = new VirtualDayTimeTimer(
            this,
            timeOfDay,
            PrimeClockIntervalTimerCallbackKind.SimpleAction,
            callback,
            null,
            timerOptions,
            cancellationToken);
        lock (_gate)
            _dayTimeTimers.Add(t);
        return t;
    }

    /// <inheritdoc />
    public IPrimeClockTimerRegistration RegisterTimeOfDay (LocalTime timeOfDay,
        Action<PrimeClockTimerCallbackContext> callback,
        object? state = null,
        DayTimeTimerOptions? timerOptions = null,
        CancellationToken cancellationToken = default)
    {
        VirtualDayTimeTimerBase t = new VirtualDayTimeTimer(
            this,
            timeOfDay,
            PrimeClockIntervalTimerCallbackKind.ContextAction,
            callback,
            state,
            timerOptions,
            cancellationToken);
        lock (_gate)
            _dayTimeTimers.Add(t);
        return t;
    }

    /// <inheritdoc />
    public IPrimeClockTimerRegistration RegisterTimeOfDay (LocalTime timeOfDay,
        Action<PrimeClockTimerCallbackContext, CancellationToken> callback,
        object? state = null,
        DayTimeTimerOptions? timerOptions = null,
        CancellationToken cancellationToken = default)
    {
        VirtualDayTimeTimerBase t = new VirtualDayTimeTimer(
            this,
            timeOfDay,
            PrimeClockIntervalTimerCallbackKind.ContextActionWithToken,
            callback,
            state,
            timerOptions,
            cancellationToken);
        lock (_gate)
            _dayTimeTimers.Add(t);
        return t;
    }

    /// <inheritdoc />
    public IPrimeClockTimerRegistration RegisterAsyncTimeOfDay (LocalTime timeOfDay,
        Func<CancellationToken, ValueTask> callback,
        DayTimeTimerOptions? timerOptions = null,
        CancellationToken cancellationToken = default)
    {
        VirtualDayTimeTimerBase t = new VirtualDayTimeTimer(
            this,
            timeOfDay,
            PrimeClockIntervalTimerCallbackKind.SimpleAsync,
            callback,
            null,
            timerOptions,
            cancellationToken);
        lock (_gate)
            _dayTimeTimers.Add(t);
        return t;
    }

    /// <inheritdoc />
    public IPrimeClockTimerRegistration RegisterAsyncTimeOfDay (LocalTime timeOfDay,
        Func<PrimeClockTimerCallbackContext, CancellationToken, ValueTask> callback,
        object? state = null,
        DayTimeTimerOptions? timerOptions = null,
        CancellationToken cancellationToken = default)
    {
        VirtualDayTimeTimerBase t = new VirtualDayTimeTimer(
            this,
            timeOfDay,
            PrimeClockIntervalTimerCallbackKind.ContextAsync,
            callback,
            state,
            timerOptions,
            cancellationToken);
        lock (_gate)
            _dayTimeTimers.Add(t);
        return t;
    }

    #endregion IPrimeClock Implementation — Time-of-day timers

    #region Private helpers

    private void RunLoop ()
    {
        while (true)
        {
            Thread.Sleep(1000);
            Duration toAdvance;
            lock (_gate)
            {
                if (!_isRunning)
                    return;
                toAdvance = _runRate;
            }

            Advance(toAdvance);
        }
    }

    private void RaiseClockEvents (Instant instant)
    {
        ClockEvents?.Invoke(this, new NodaClockTimeChangedEventArgs(instant));
    }

    private void RemoveIntervalTimer (VirtualIntervalTimerBase timer)
    {
        lock (_gate)
            _intervalTimers.Remove(timer);
    }

    private void RemoveDayTimeTimer (VirtualDayTimeTimerBase timer)
    {
        lock (_gate)
            _dayTimeTimers.Remove(timer);
    }

    #endregion Private helpers

    #region Nested types — Pending delay and time expiry

    private sealed class PendingDelay
    {
        public Instant DueInstant { [DebuggerStepThrough] get; }
        public TaskCompletionSource<bool> TaskCompletionSource { [DebuggerStepThrough] get; }

        public PendingDelay (
            Instant dueInstant,
            TaskCompletionSource<bool> tcs,
            CancellationToken? cancellationToken)
        {
            DueInstant = dueInstant;
            TaskCompletionSource = tcs;
        }

        public void Complete ()
        {
            TaskCompletionSource.TrySetResult(true);
        }
    }

    private sealed class TimeExpiryEntry
    {
        public Instant ExpireInstant { [DebuggerStepThrough] get; }
        private readonly TimeCancellationTokenSource _wrapper;

        public TimeExpiryEntry (
            Instant expireInstant,
            TimeCancellationTokenSource wrapper,
            CancellationTokenSource? timeCts = null)
        {
            ExpireInstant = expireInstant;
            _wrapper = wrapper;
        }

        public void Cancel ()
        {
            try
            {
                _wrapper.Cancel();
            }
            catch (ObjectDisposedException)
            {
                // Caller may have disposed already.
            }
        }
    }

    #endregion Nested types — Pending delay and time expiry

    #region Nested types — Virtual interval timer

    private abstract class VirtualIntervalTimerBase : IPrimeClockTimerRegistration
    {
        protected readonly PrimeTestClock Clock;
        protected readonly PrimeClockIntervalTimerCallbackKind CallbackKind;
        protected readonly Delegate Callback;
        protected readonly object? CallbackState;
        protected readonly CancellationToken CancellationToken;
#if NET10_OR_GREATER
        protected readonly Lock Gate = new();
#else
        protected readonly object Gate = new();
#endif
        protected Instant? NextDueInstant;
        protected Instant? LastCallbackInstant;
        protected TimerState State = TimerState.Active;
        protected bool _enabled = true;
        protected bool Disposed;
        protected bool CancelRequested;
        protected int CallbacksRunning;
        protected Duration InitialCallbackTime;
        protected Duration RepeatInterval;
        private readonly bool _isLocalTimeRepresentation;
        private readonly bool _resetAfterCallback;

        protected VirtualIntervalTimerBase (
            PrimeTestClock clock,
            Duration initialCallbackTime,
            Duration repeatInterval,
            PrimeClockIntervalTimerCallbackKind callbackKind,
            Delegate callback,
            object? callbackState,
            IntervalTimerOptions? options,
            CancellationToken cancellationToken)
        {
            Clock = clock;
            InitialCallbackTime = initialCallbackTime;
            RepeatInterval = repeatInterval;
            CallbackKind = callbackKind;
            Callback = callback;
            CallbackState = callbackState;
            CancellationToken = cancellationToken;
            _isLocalTimeRepresentation = options?.LocalTimeRepresentation ?? false;
            _resetAfterCallback = options?.ResetIntervalAfterCallback ?? false;
            Id = Interlocked.Increment(ref _nextTimerId);
            Instant now = clock.Instant;
            RegisteredInstant = now;
            NextDueInstant = now + initialCallbackTime;

            if (cancellationToken.CanBeCanceled)
            {
                cancellationToken.Register(OnCancelRequested);
                if (cancellationToken.IsCancellationRequested)
                {
                    CancelRequested = true;
                    State = TimerState.Cancelled;
                    _enabled = false;
                }
            }
        }

        public int Id { [DebuggerStepThrough] get; }
        public Instant RegisteredInstant { [DebuggerStepThrough] get; }
        public bool IsTimeOfDay => false;
        public bool IsRepeating =>
            RepeatInterval > Duration.Zero && RepeatInterval != NoRepeatSentinel;
        public bool IsResetAfterCallback => _resetAfterCallback && IsRepeating;
        public bool IsCancelled => State == TimerState.Cancelled;
        public bool IsLocalTimeRepresentation => _isLocalTimeRepresentation;
        public bool IsActive =>
            State != TimerState.Cancelled && State != TimerState.Disposed && _enabled;
        public bool CallbacksProcessing => CallbacksRunning > 0;

        public bool Enabled
        {
            get => _enabled && !IsCancelled && State != TimerState.Disposed;
            set
            {
                lock (Gate)
                {
                    if (Disposed || State == TimerState.Cancelled)
                        return;
                    if (value)
                        Start();
                    else
                        Stop();
                }
            }
        }

        TimerState IRegisteredTimer.State => State;

        public long ElapsedTime
        {
            get
            {
                lock (Gate)
                {
                    if (LastCallbackInstant is not { } last)
                        return -1;
                    if (CallbacksRunning > 0)
                        return 0;
                    Instant now = Clock.Instant;
                    if (last >= now)
                        return 0;
                    return (long)(now - last).TotalMilliseconds;
                }
            }
        }

        public long TimeUntilNextCallback
        {
            get
            {
                lock (Gate)
                {
                    if (!IsRepeating && LastCallbackInstant.HasValue)
                        return -1;
                    if (NextDueInstant is not { } next)
                        return -1;
                    Instant now = Clock.Instant;
                    if (next <= now)
                        return 0;
                    if (CallbacksRunning > 0 && IsResetAfterCallback)
                        return (long)RepeatInterval.TotalMilliseconds;
                    return (long)(next - now).TotalMilliseconds;
                }
            }
        }

        public bool IsDue (Instant now)
        {
            lock (Gate)
            {
                if (Disposed || CancelRequested || State == TimerState.Cancelled || !_enabled)
                    return false;
                if (NextDueInstant is not { } next)
                    return false;
                return next <= now;
            }
        }

        public abstract void RunDueCallback (Instant now);

        protected void OnCancelRequested ()
        {
            lock (Gate)
            {
                if (Disposed || State == TimerState.Cancelled)
                    return;
                CancelRequested = true;
                State = TimerState.Cancelled;
                _enabled = false;
            }

            Clock.RemoveIntervalTimer(this);
        }

        public bool Change (Duration interval) =>
            Change(interval, IsRepeating ? interval : NoRepeatSentinel);

        public bool Change (Duration nextInterval, Duration repeatInterval)
        {
            lock (Gate)
            {
                if (Disposed || State == TimerState.Cancelled)
                    return false;
                if (!IsRepeating && repeatInterval > Duration.Zero && repeatInterval != NoRepeatSentinel)
                    throw new InvalidOperationException(
                        "Cannot change a non-repeating timer to a repeating timer.");
                InitialCallbackTime = nextInterval;
                RepeatInterval = repeatInterval;
                if (State == TimerState.Completed)
                    State = TimerState.Active;
                if (!_enabled)
                    return true;
                NextDueInstant = Clock.Instant + nextInterval;
                return true;
            }
        }

        public bool Change (LocalTime timeOfDay) => false;

        public void Cancel ()
        {
            lock (Gate)
            {
                if (State == TimerState.Cancelled || Disposed)
                    return;
                CancelRequested = true;
                State = TimerState.Cancelled;
                _enabled = false;
            }

            Clock.RemoveIntervalTimer(this);
        }

        public bool Stop ()
        {
            lock (Gate)
            {
                if (!_enabled || State == TimerState.Cancelled || Disposed)
                    return false;
                _enabled = false;
                State = TimerState.Disabled;
                return true;
            }
        }

        public bool Start ()
        {
            lock (Gate)
            {
                if (Disposed || State == TimerState.Cancelled)
                    return false;
                if (State != TimerState.Completed && State != TimerState.Disabled)
                    return false;
                _enabled = true;
                State = TimerState.Active;
                NextDueInstant = Clock.Instant + InitialCallbackTime;
                return true;
            }
        }

        public void Dispose ()
        {
            lock (Gate)
            {
                if (Disposed)
                    return;
                Disposed = true;
                State = TimerState.Disposed;
                _enabled = false;
            }

            Clock.RemoveIntervalTimer(this);
        }
    }

    private sealed class VirtualIntervalTimer : VirtualIntervalTimerBase
    {
        public VirtualIntervalTimer (
            PrimeTestClock clock,
            Duration initialCallbackTime,
            Duration repeatInterval,
            PrimeClockIntervalTimerCallbackKind callbackKind,
            Delegate callback,
            object? callbackState,
            IntervalTimerOptions? options,
            CancellationToken cancellationToken)
            : base(clock, initialCallbackTime, repeatInterval, callbackKind, callback, callbackState, options, cancellationToken)
        {
        }

        public override void RunDueCallback (Instant now)
        {
            bool resetAfter;
            bool isRepeating;
            Instant firedAt;

            lock (Gate)
            {
                if (Disposed || CancelRequested || State == TimerState.Cancelled || !_enabled)
                    return;
                if (NextDueInstant is not { } next || next > now)
                    return;
                firedAt = next;
                NextDueInstant = null;
                LastCallbackInstant = firedAt;
                isRepeating = IsRepeating;
                resetAfter = IsResetAfterCallback;
                State = isRepeating && !resetAfter
                    ? TimerState.RepeatProcessingCallback
                    : TimerState.ProcessingCallback;
                CallbacksRunning++;
            }

            try
            {
                RunCallback(resetAfter, isRepeating, firedAt);
            }
            finally
            {
                lock (Gate)
                    CallbacksRunning--;
            }
        }

        private void RunCallback (bool resetAfter, bool isRepeating, Instant now)
        {
            switch (CallbackKind)
            {
                case PrimeClockIntervalTimerCallbackKind.SimpleAction:
                    ((Action)Callback)();
                    break;
                case PrimeClockIntervalTimerCallbackKind.ContextAction:
                    ((Action<PrimeClockTimerCallbackContext>)Callback)(
                        new PrimeClockTimerCallbackContext(this, CallbackState));
                    break;
                case PrimeClockIntervalTimerCallbackKind.ContextActionWithToken:
                    ((Action<PrimeClockTimerCallbackContext, CancellationToken>)Callback)(
                        new PrimeClockTimerCallbackContext(this, CallbackState), CancellationToken);
                    break;
                case PrimeClockIntervalTimerCallbackKind.SimpleAsync:
                    RunAsyncAndScheduleAfter(
                        () => ((Func<CancellationToken, ValueTask>)Callback)(CancellationToken),
                        resetAfter,
                        isRepeating,
                        now);
                    return;
                case PrimeClockIntervalTimerCallbackKind.ContextAsync:
                    RunAsyncAndScheduleAfter(
                        () => ((Func<PrimeClockTimerCallbackContext, CancellationToken, ValueTask>)Callback)(
                            new PrimeClockTimerCallbackContext(this, CallbackState), CancellationToken),
                        resetAfter,
                        isRepeating,
                        now);
                    return;
                default:
                    throw new InvalidOperationException($"Unsupported callback kind: {CallbackKind}");
            }

            OnSyncCallbackCompleted(resetAfter, isRepeating, now);
        }

        private void RunAsyncAndScheduleAfter (
            Func<ValueTask> run,
            bool resetAfter,
            bool isRepeating,
            Instant now)
        {
            ValueTask vt = run();
            if (vt.IsCompletedSuccessfully)
            {
                OnAsyncCallbackCompleted(resetAfter, isRepeating, now);
                return;
            }

            vt.AsTask().ContinueWith(
                (_, state) =>
                {
                    (VirtualIntervalTimer reg, bool ra, bool rep, Instant n) =
                        ((VirtualIntervalTimer, bool, bool, Instant))state!;
                    reg.OnAsyncCallbackCompleted(ra, rep, n);
                },
                (this, resetAfter, isRepeating, now),
                CancellationToken.None,
                TaskContinuationOptions.None,
                TaskScheduler.Default);
        }

        private void OnSyncCallbackCompleted (bool resetAfter, bool isRepeating, Instant now)
        {
            lock (Gate)
            {
                if (Disposed || CancelRequested || State == TimerState.Cancelled)
                    return;
                if (!isRepeating)
                {
                    State = TimerState.Completed;
                    return;
                }

                State = TimerState.RepeatCycle;
                NextDueInstant = now + RepeatInterval;
            }
        }

        private void OnAsyncCallbackCompleted (bool resetAfter, bool isRepeating, Instant now)
        {
            lock (Gate)
            {
                if (Disposed || CancelRequested || State == TimerState.Cancelled)
                    return;
                if (!isRepeating)
                {
                    State = TimerState.Completed;
                    return;
                }

                State = TimerState.RepeatCycle;
                NextDueInstant = now + RepeatInterval;
            }
        }
    }

    #endregion Nested types — Virtual interval timer

    #region Nested types — Virtual day-time timer

    private abstract class VirtualDayTimeTimerBase : IPrimeClockTimerRegistration
    {
        protected readonly PrimeTestClock Clock;
        protected readonly PrimeClockIntervalTimerCallbackKind CallbackKind;
        protected readonly Delegate Callback;
        protected readonly object? CallbackState;
        protected readonly CancellationToken CancellationToken;
#if NET10_OR_GREATER
        protected readonly Lock Gate = new();
#else
        protected readonly object Gate = new();
#endif
        protected Instant? NextDueInstant;
        protected TimerState State = TimerState.Active;
        protected bool _enabledDayTime = true;
        protected bool Disposed;
        protected bool CancelRequested;
        protected int CallbacksRunning;
        protected LocalTime TargetTimeOfDay;

        protected VirtualDayTimeTimerBase (
            PrimeTestClock clock,
            LocalTime timeOfDay,
            PrimeClockIntervalTimerCallbackKind callbackKind,
            Delegate callback,
            object? callbackState,
            DayTimeTimerOptions? options,
            CancellationToken cancellationToken)
        {
            Clock = clock;
            TargetTimeOfDay = timeOfDay;
            CallbackKind = callbackKind;
            Callback = callback;
            CallbackState = callbackState;
            CancellationToken = cancellationToken;
            ConcurrentTriggerProcessing =
                options?.ConcurrentTriggerProcessing ?? ConcurrentTriggerProcessing.RunSequentially;
            SkippedTimeBehavior = options?.SkippedTimeBehavior ?? SkippedTimeBehavior.RunAfter;
            DuplicateTimeBehavior = options?.DuplicateTimeBehavior ?? DuplicateTimeBehavior.RunFirst;
            Id = Interlocked.Increment(ref _nextTimerId);
            RegisteredInstant = clock.Instant;
            IsLocalTimeRepresentation = true;
            NextDueInstant = ComputeNextDue(clock.Instant);

            if (cancellationToken.CanBeCanceled)
            {
                cancellationToken.Register(OnCancelRequested);
                if (cancellationToken.IsCancellationRequested)
                {
                    CancelRequested = true;
                    State = TimerState.Cancelled;
                    _enabledDayTime = false;
                }
            }
        }

        public int Id { [DebuggerStepThrough] get; }
        public Instant RegisteredInstant { [DebuggerStepThrough] get; }
        public bool IsTimeOfDay => true;
        public bool IsResetAfterCallback => false;
        public bool IsLocalTimeRepresentation { [DebuggerStepThrough] get; }
        public bool IsRepeating => true;
        public bool IsCancelled => State == TimerState.Cancelled;
        public bool IsActive =>
            State != TimerState.Cancelled && State != TimerState.Disposed && _enabledDayTime;
        public bool CallbacksProcessing => CallbacksRunning > 0;
        public ConcurrentTriggerProcessing ConcurrentTriggerProcessing { [DebuggerStepThrough] get; }
        public SkippedTimeBehavior SkippedTimeBehavior { [DebuggerStepThrough] get; }
        public DuplicateTimeBehavior DuplicateTimeBehavior { [DebuggerStepThrough] get; }

        public bool Enabled
        {
            get => _enabledDayTime && !IsCancelled && State != TimerState.Disposed;
            set
            {
                lock (Gate)
                {
                    if (Disposed || State == TimerState.Cancelled)
                        return;
                    if (value)
                    {
                        _enabledDayTime = true;
                        State = TimerState.Active;
                        NextDueInstant = ComputeNextDue(Clock.Instant);
                    }
                    else
                    {
                        _enabledDayTime = false;
                        State = TimerState.Disabled;
                    }
                }
            }
        }

        TimerState IRegisteredTimer.State => State;

        public long ElapsedTime => -1;

        public long TimeUntilNextCallback
        {
            get
            {
                lock (Gate)
                {
                    if (NextDueInstant is not { } next)
                        return -1;
                    Instant now = Clock.Instant;
                    if (next <= now)
                        return 0;
                    return (long)(next - now).TotalMilliseconds;
                }
            }
        }

        public bool IsDue (Instant now)
        {
            lock (Gate)
            {
                if (Disposed || CancelRequested || State == TimerState.Cancelled || !_enabledDayTime)
                    return false;
                if (NextDueInstant is not { } next)
                    return false;
                return next <= now;
            }
        }

        public abstract void RunDueCallback (Instant now);

        protected Instant? ComputeNextDue (Instant now)
        {
            ZonedDateTime nowZ = now.InZone(Clock._zone);
            LocalDate today = nowZ.Date;
            LocalDateTime nextLdt = today.At(TargetTimeOfDay);
            ZonedDateTime nextZdt = nextLdt.InZoneLeniently(nowZ.Zone);
            if (nextZdt.ToInstant() <= now)
            {
                nextLdt = today.PlusDays(1).At(TargetTimeOfDay);
                nextZdt = nextLdt.InZoneLeniently(nowZ.Zone);
            }
            return nextZdt.ToInstant();
        }

        protected void OnCancelRequested ()
        {
            lock (Gate)
            {
                if (Disposed || State == TimerState.Cancelled)
                    return;
                CancelRequested = true;
                State = TimerState.Cancelled;
                _enabledDayTime = false;
            }

            Clock.RemoveDayTimeTimer(this);
        }

        public bool Change (Duration interval) => false;

        public bool Change (Duration nextInterval, Duration repeatInterval) => false;

        public bool Change (LocalTime timeOfDay)
        {
            lock (Gate)
            {
                if (Disposed || State == TimerState.Cancelled)
                    return false;
                TargetTimeOfDay = timeOfDay;
                if (Enabled)
                    NextDueInstant = ComputeNextDue(Clock.Instant);
                return true;
            }
        }

        public void Cancel ()
        {
            lock (Gate)
            {
                if (State == TimerState.Cancelled || Disposed)
                    return;
                CancelRequested = true;
                State = TimerState.Cancelled;
                _enabledDayTime = false;
            }

            Clock.RemoveDayTimeTimer(this);
        }

        public bool Stop ()
        {
            lock (Gate)
            {
                if (!_enabledDayTime || State == TimerState.Cancelled || Disposed)
                    return false;
                _enabledDayTime = false;
                State = TimerState.Disabled;
                return true;
            }
        }

        public bool Start ()
        {
            lock (Gate)
            {
                if (Disposed || State == TimerState.Cancelled)
                    return false;
                if (State != TimerState.Completed && State != TimerState.Disabled)
                    return false;
                _enabledDayTime = true;
                State = TimerState.Active;
                NextDueInstant = ComputeNextDue(Clock.Instant);
                return true;
            }
        }

        public void Dispose ()
        {
            lock (Gate)
            {
                if (Disposed)
                    return;
                Disposed = true;
                State = TimerState.Disposed;
                _enabledDayTime = false;
            }

            Clock.RemoveDayTimeTimer(this);
        }
    }

    private sealed class VirtualDayTimeTimer : VirtualDayTimeTimerBase
    {
        public VirtualDayTimeTimer (
            PrimeTestClock clock,
            LocalTime timeOfDay,
            PrimeClockIntervalTimerCallbackKind callbackKind,
            Delegate callback,
            object? callbackState,
            DayTimeTimerOptions? options,
            CancellationToken cancellationToken)
            : base(clock, timeOfDay, callbackKind, callback, callbackState, options, cancellationToken)
        {
        }

        public override void RunDueCallback (Instant now)
        {
            lock (Gate)
            {
                if (Disposed || CancelRequested || State == TimerState.Cancelled || !_enabledDayTime)
                    return;
                if (NextDueInstant is not { } next || next > now)
                    return;
                NextDueInstant = ComputeNextDue(now);
                State = TimerState.RepeatProcessingCallback;
                CallbacksRunning++;
            }

            try
            {
                RunCallback();
            }
            finally
            {
                lock (Gate)
                {
                    CallbacksRunning--;
                    State = TimerState.RepeatCycle;
                }
            }
        }

        private void RunCallback ()
        {
            switch (CallbackKind)
            {
                case PrimeClockIntervalTimerCallbackKind.SimpleAction:
                    ((Action)Callback)();
                    break;
                case PrimeClockIntervalTimerCallbackKind.ContextAction:
                    ((Action<PrimeClockTimerCallbackContext>)Callback)(
                        new PrimeClockTimerCallbackContext(this, CallbackState));
                    break;
                case PrimeClockIntervalTimerCallbackKind.ContextActionWithToken:
                    ((Action<PrimeClockTimerCallbackContext, CancellationToken>)Callback)(
                        new PrimeClockTimerCallbackContext(this, CallbackState), CancellationToken);
                    break;
                case PrimeClockIntervalTimerCallbackKind.SimpleAsync:
                    ((Func<CancellationToken, ValueTask>)Callback)(CancellationToken).AsTask().GetAwaiter().GetResult();
                    break;
                case PrimeClockIntervalTimerCallbackKind.ContextAsync:
                    ((Func<PrimeClockTimerCallbackContext, CancellationToken, ValueTask>)Callback)(
                        new PrimeClockTimerCallbackContext(this, CallbackState), CancellationToken)
                        .AsTask().GetAwaiter().GetResult();
                    break;
                default:
                    throw new InvalidOperationException($"Unsupported callback kind: {CallbackKind}");
            }
        }
    }

    #endregion Nested types — Virtual day-time timer
}
