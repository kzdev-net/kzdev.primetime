using System.Diagnostics;
using NodaTime;

namespace KZDev.PrimeTime;

/// <summary>
///   Implementation of day-time timer registration contracts used by <see cref="PrimeClock"/>
///   (fire once per day at a given <see cref="LocalTime"/>).
/// </summary>
internal sealed class PrimeClockDayTimeTimerRegistration : IClockDayTimeTimer
{
    private static int _nextId;
    private static readonly Duration RunSequentiallyRetryDelay = Duration.FromMilliseconds(30);

    private readonly IPrimeClock _clock;
    private readonly bool _captureContext;
    private readonly IntervalTimerCallbackKind _callbackKind;
    private readonly Delegate _callback;
    private readonly object? _callbackState;
    private readonly CancellationToken _cancellationToken;
    private readonly CancellationTokenRegistration _cancelRegistration;
#if NET10_OR_GREATER
    private readonly Lock _gate = new();
#else
    private readonly object _gate = new();
#endif
    private Timer? _timer;

    /// <summary>
    ///   Indicates whether the configured time of day is interpreted in UTC (<c>true</c>) or in the
    ///   clock's local time zone (<c>false</c>) when scheduling daily callbacks.
    /// </summary>
    private readonly bool _utcTimeOfDaySchedule;
    private LocalTime _targetTimeOfDay;
    private Instant? _nextCallbackInstant;
    private Instant? _lastCallbackInstant;
    private TimerState _state;
    private bool _enabled = true;
    private bool _disposed;
    private int _callbacksRunning;
    private bool _cancelRequested;
    private bool _pendingRunSequential;

    #region Constructors/Finalizers

    /// <summary>
    ///   Initializes a new instance of the <see cref="PrimeClockDayTimeTimerRegistration"/> class.
    /// </summary>
    /// <param name="utcTimeOfDaySchedule">
    ///   <c>true</c> to interpret <paramref name="timeOfDay"/> in UTC for each UTC calendar day;
    ///   <c>false</c> (the default) to interpret it in the clock's local time zone for each local day.
    /// </param>
    internal PrimeClockDayTimeTimerRegistration (IPrimeClock clock,
        LocalTime timeOfDay,
        IntervalTimerCallbackKind callbackKind,
        Delegate callback,
        object? callbackState,
        DayTimeTimerOptions? options,
        CancellationToken cancellationToken,
        bool utcTimeOfDaySchedule = false)
    {
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _utcTimeOfDaySchedule = utcTimeOfDaySchedule;
        _targetTimeOfDay = timeOfDay;
        _callbackKind = callbackKind;
        _callback = callback ?? throw new ArgumentNullException(nameof(callback));
        _callbackState = callbackState;

        DayTimeTimerOptions opts = options ?? new DayTimeTimerOptions();
        ConcurrentTriggerProcessing = opts.ConcurrentTriggerProcessing;
        SkippedTimeBehavior = opts.SkippedTimeBehavior;
        DuplicateTimeBehavior = opts.DuplicateTimeBehavior;
        _captureContext = opts.CallbackExecutionContext != TimerCallbackExecutionContext.Unsafe;
        _cancellationToken = cancellationToken;

        Id = Interlocked.Increment(ref _nextId);
        RegisteredInstant = clock.NowInstant;
        IsLocalTimeRepresentation = !utcTimeOfDaySchedule;

        if (cancellationToken.CanBeCanceled)
        {
            _cancelRegistration = cancellationToken.Register(OnCancelRequested);
            if (cancellationToken.IsCancellationRequested)
            {
                _cancelRequested = true;
                _state = TimerState.Cancelled;
                return;
            }
        }

        _state = TimerState.Active;
        ScheduleNext();
    }

    #endregion Constructors/Finalizers

    /// <inheritdoc />
    public int Id { [DebuggerStepThrough] get; }

    /// <inheritdoc />
    public Instant RegisteredInstant { [DebuggerStepThrough] get; }

    /// <inheritdoc />
    public DateTimeOffset RegisteredTime => RegisteredInstant.ToDateTimeOffset();

    /// <inheritdoc />
    public bool IsTimeOfDay => true;

    /// <inheritdoc />
    public bool IsResetAfterCallback => false;

    /// <inheritdoc />
    public bool IsLocalTimeRepresentation { [DebuggerStepThrough] get; }

    /// <inheritdoc />
    public bool IsRepeating => true;

    /// <inheritdoc />
    public bool IsCancelled => _state == TimerState.Cancelled;

    /// <inheritdoc />
    public bool IsActive =>
        _state != TimerState.Cancelled &&
        _state != TimerState.Disposed &&
        _enabled;

    /// <inheritdoc />
    public TimerState State => _state;

    /// <inheritdoc />
    public bool CallbacksProcessing => _callbacksRunning > 0;

    /// <inheritdoc />
    public ConcurrentTriggerProcessing ConcurrentTriggerProcessing { [DebuggerStepThrough] get; }

    /// <inheritdoc />
    public SkippedTimeBehavior SkippedTimeBehavior { [DebuggerStepThrough] get; }

    /// <inheritdoc />
    public DuplicateTimeBehavior DuplicateTimeBehavior { [DebuggerStepThrough] get; }

    /// <inheritdoc />
    public bool Enabled
    {
        get => _enabled && !IsCancelled && _state != TimerState.Disposed;
        set
        {
            lock (_gate)
            {
                if (_disposed || _state == TimerState.Cancelled)
                    return;
                if (value)
                    Start();
                else
                    Stop();
            }
        }
    }

    /// <inheritdoc />
    public long ElapsedTime
    {
        get
        {
            lock (_gate)
            {
                if (_lastCallbackInstant is not { } last)
                    return -1;
                if (_callbacksRunning > 0)
                    return 0;
                Instant now = _clock.NowInstant;
                if (last >= now)
                    return 0;
                return (long)(now - last).TotalMilliseconds;
            }
        }
    }

    /// <inheritdoc />
    public long TimeUntilNextCallback
    {
        get
        {
            lock (_gate)
            {
                if (_nextCallbackInstant is not { } next)
                    return -1;
                Instant now = _clock.NowInstant;
                if (next <= now)
                    return 0;
                return (long)(next - now).TotalMilliseconds;
            }
        }
    }

    private void OnCancelRequested ()
    {
        lock (_gate)
        {
            if (_disposed || _state == TimerState.Cancelled)
                return;
            _cancelRequested = true;
            _state = TimerState.Cancelled;
            _enabled = false;
            _timer?.Change(Timeout.Infinite, Timeout.Infinite);
        }
    }

    /// <summary>
    ///   Computes the duration until the next occurrence of the target time of day in the
    ///   clock's local zone.
    /// </summary>
    private Duration GetDelayUntilNext ()
    {
        Instant now = _clock.NowInstant;
        if (_utcTimeOfDaySchedule)
        {
            ZonedDateTime nowZ = _clock.UtcNow;
            LocalDate today = nowZ.Date;
            LocalDateTime nextLdt = today.At(_targetTimeOfDay);
            ZonedDateTime nextZdt = nextLdt.InZoneLeniently(DateTimeZone.Utc);
            if (nextZdt.ToInstant() <= now)
            {
                nextLdt = today.PlusDays(1).At(_targetTimeOfDay);
                nextZdt = nextLdt.InZoneLeniently(DateTimeZone.Utc);
            }

            return nextZdt.ToInstant() - now;
        }

        ZonedDateTime nowLocalZ = _clock.LocalZonedNow;
        LocalDate todayLocal = nowLocalZ.Date;
        LocalDateTime nextLocalLdt = todayLocal.At(_targetTimeOfDay);
        ZonedDateTime nextLocalZdt = nextLocalLdt.InZoneLeniently(nowLocalZ.Zone);
        if (nextLocalZdt.ToInstant() <= now)
        {
            nextLocalLdt = todayLocal.PlusDays(1).At(_targetTimeOfDay);
            nextLocalZdt = nextLocalLdt.InZoneLeniently(nowLocalZ.Zone);
        }

        return nextLocalZdt.ToInstant() - now;
    }

    private static int DurationToTimerMilliseconds (Duration duration)
    {
        if (duration <= Duration.Zero)
            return 0;
        try
        {
            TimeSpan ts = duration.ToTimeSpan();
            long msLong = (long)Math.Min(ts.TotalMilliseconds, int.MaxValue);
            if (msLong <= 0)
                return 0;
            return (int)msLong;
        }
        catch (OverflowException)
        {
            return int.MaxValue;
        }
    }

    private void ScheduleNext ()
    {
        lock (_gate)
        {
            if (_disposed || _cancelRequested || _state == TimerState.Cancelled || !_enabled)
                return;
            Duration delay = GetDelayUntilNext();
            int ms = DurationToTimerMilliseconds(delay);
            if (ms == Timeout.Infinite)
                return;
            _nextCallbackInstant = _clock.NowInstant + delay;
            if (_timer is null)
                _timer = new Timer(OnTimerTick, null, ms, Timeout.Infinite);
            else
                _timer.Change(ms, Timeout.Infinite);
        }
    }

    private void ScheduleNextAfterShortDelay ()
    {
        lock (_gate)
        {
            if (_disposed || _cancelRequested || _state == TimerState.Cancelled || !_enabled)
                return;
            int ms = (int)Math.Min(RunSequentiallyRetryDelay.TotalMilliseconds, int.MaxValue);
            if (ms <= 0)
                ms = 1;
            if (_timer is null)
                _timer = new Timer(OnTimerTick, null, ms, Timeout.Infinite);
            else
                _timer.Change(ms, Timeout.Infinite);
        }
    }

    private void OnTimerTick (object? _)
    {
        lock (_gate)
        {
            if (_disposed || _cancelRequested || _state == TimerState.Cancelled || !_enabled)
                return;
            _timer!.Change(Timeout.Infinite, Timeout.Infinite);
        }

        if (ConcurrentTriggerProcessing == ConcurrentTriggerProcessing.Skip && _callbacksRunning > 0)
        {
            ScheduleNext();
            return;
        }

        if (ConcurrentTriggerProcessing == ConcurrentTriggerProcessing.RunSequentially && _callbacksRunning > 0)
        {
            lock (_gate)
            {
                _pendingRunSequential = true;
            }
            ScheduleNextAfterShortDelay();
            return;
        }

        _nextCallbackInstant = null;
        Instant nowAtTick = _clock.NowInstant;
        _lastCallbackInstant = nowAtTick;
        lock (_gate)
        {
            _state = TimerState.RepeatProcessingCallback;
            _callbacksRunning++;
        }

        bool isAsync = _callbackKind == IntervalTimerCallbackKind.SimpleAsync ||
            _callbackKind == IntervalTimerCallbackKind.ContextAsync;
        try
        {
            RunCallback();
        }
        finally
        {
            if (!isAsync)
            {
                lock (_gate)
                {
                    _callbacksRunning--;
                }
            }
        }
        if (!isAsync)
        {
            bool doRetry;
            lock (_gate)
            {
                doRetry = _pendingRunSequential;
                _pendingRunSequential = false;
            }
            if (doRetry)
                ScheduleNextAfterShortDelay();
            else
                ScheduleNext();
        }
    }

    private void RunCallback ()
    {
        void InvokeSync (Action run)
        {
            if (_captureContext && !ExecutionContext.IsFlowSuppressed())
            {
                ExecutionContext? ec = ExecutionContext.Capture();
                if (ec is not null)
                {
                    ExecutionContext.Run(ec, _ => run(), null);
                    return;
                }
            }
            if (!_captureContext && !ExecutionContext.IsFlowSuppressed())
            {
                using (ExecutionContext.SuppressFlow())
                {
                    run();
                }
                return;
            }
            run();
        }

        switch (_callbackKind)
        {
            case IntervalTimerCallbackKind.SimpleAction:
                InvokeSync(() => ((Action)_callback)());
                break;
            case IntervalTimerCallbackKind.ContextAction:
                InvokeSync(() => ((Action<ClockTimerCallbackContext>)_callback)(new ClockTimerCallbackContext(this, _callbackState)));
                break;
            case IntervalTimerCallbackKind.ContextActionWithToken:
                InvokeSync(() => ((Action<ClockTimerCallbackContext, CancellationToken>)_callback)(new ClockTimerCallbackContext(this, _callbackState),
                    _cancellationToken));
                break;
            case IntervalTimerCallbackKind.SimpleAsync:
                RunAsyncAndScheduleAfter(() => ((Func<CancellationToken, ValueTask>)_callback)(_cancellationToken));
                return;
            case IntervalTimerCallbackKind.ContextAsync:
                RunAsyncAndScheduleAfter(() => ((Func<ClockTimerCallbackContext, CancellationToken, ValueTask>)_callback)(new ClockTimerCallbackContext(this, _callbackState),
                    _cancellationToken));
                return;
            default:
                throw new InvalidOperationException($"Unsupported callback kind: {_callbackKind}");
        }
    }

    private void RunAsyncAndScheduleAfter (Func<ValueTask> run)
    {
        ValueTask vt = run();
        if (vt.IsCompletedSuccessfully)
        {
            ScheduleNextFromAsync();
            return;
        }
        vt.AsTask().ContinueWith((_, state) =>
            {
                ((PrimeClockDayTimeTimerRegistration)state!).ScheduleNextFromAsync();
            },
            this,
            CancellationToken.None,
            TaskContinuationOptions.None,
            TaskScheduler.Default);
    }

    private void ScheduleNextFromAsync ()
    {
        lock (_gate)
        {
            _callbacksRunning--;
            if (_disposed || _cancelRequested || _state == TimerState.Cancelled)
                return;
            if (_pendingRunSequential)
            {
                _pendingRunSequential = false;
                ScheduleNextAfterShortDelay();
                return;
            }
        }
        ScheduleNext();
    }

    #region Day-time change operations

#if NET
    /// <inheritdoc />
    public bool Change (LocalTimeOfDay newTimeOfDay)
    {
        if (_utcTimeOfDaySchedule)
        {
            return false;
        }

        TimeOnly t = newTimeOfDay.Value;
        LocalTime localTime = new(t.Hour, t.Minute, t.Second, t.Millisecond);
        return Change(localTime);
    }

    /// <inheritdoc />
    public bool Change (UtcTimeOfDay newTimeOfDay)
    {
        if (!_utcTimeOfDaySchedule)
        {
            return false;
        }

        TimeOnly t = newTimeOfDay.Value;
        LocalTime localTime = new(t.Hour, t.Minute, t.Second, t.Millisecond);
        return Change(localTime);
    }
#endif

    /// <inheritdoc />
    public bool Change (LocalTime timeOfDay)
    {
        lock (_gate)
        {
            if (_disposed || _state == TimerState.Cancelled)
                return false;
            _targetTimeOfDay = timeOfDay;
            if (!_enabled)
                return true;
            ScheduleNext();
            return true;
        }
    }

#if NET
    /// <inheritdoc />
    public bool Change (Duration interval) => false;
#endif

    #endregion Day-time change operations

    #region IRegisteredTimer Implementation

    /// <inheritdoc />
    public void Cancel ()
    {
        lock (_gate)
        {
            if (_state == TimerState.Cancelled || _disposed)
                return;
            _cancelRequested = true;
            _state = TimerState.Cancelled;
            _enabled = false;
            _timer?.Change(Timeout.Infinite, Timeout.Infinite);
        }
    }

    /// <inheritdoc />
    public bool Stop ()
    {
        lock (_gate)
        {
            if (!_enabled || _state == TimerState.Cancelled || _disposed)
                return false;
            _enabled = false;
            _state = TimerState.Disabled;
            _timer?.Change(Timeout.Infinite, Timeout.Infinite);
            return true;
        }
    }

    /// <inheritdoc />
    public bool Start ()
    {
        lock (_gate)
        {
            if (_disposed || _state == TimerState.Cancelled)
                return false;
            if (_state != TimerState.Disabled)
                return false;
            _enabled = true;
            _state = TimerState.Active;
            ScheduleNext();
            return true;
        }
    }

    /// <inheritdoc />
    public void Dispose ()
    {
        lock (_gate)
        {
            if (_disposed)
                return;
            _disposed = true;
            _state = TimerState.Disposed;
            _enabled = false;
            _cancelRegistration.Dispose();
            _timer?.Dispose();
            _timer = null;
        }
    }

    #endregion IRegisteredTimer Implementation
}

