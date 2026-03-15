using System.Collections.Generic;
using System.Threading.Tasks;

namespace KZDev.PrimeTime
{
    /// <summary>
    ///   Test clock implementation of <see cref="IPrimeTestSystemClock"/> that maintains
    ///   virtual time. All "now" values, delays, time-based cancellation, and timers are
    ///   driven by this virtual time so tests can advance time deterministically.
    /// </summary>
    public sealed class PrimeTestSystemClock : IPrimeTestSystemClock
    {
        private static int _nextTimerId;
#if NET10_OR_GREATER
        private readonly Lock _gate = new();
#else
        private readonly object _gate = new();
#endif
        private DateTimeOffset _utcNow;
        private bool _isRunning;
        private Thread? _runThread;
        private TimeSpan _runRate = TimeSpan.FromSeconds(1);
        private readonly List<PendingDelay> _pendingDelays = [];
        private readonly List<TimeExpiryEntry> _timeExpiryEntries = [];
        private readonly List<VirtualIntervalTimerBase> _intervalTimers = [];
#if NET
        private readonly List<VirtualDayTimeTimerBase> _dayTimeTimers = [];
#endif

        /// <summary>
        ///   Occurs when the clock's current time has changed.
        /// </summary>
        public event EventHandler<ClockTimeChangedEventArgs>? ClockEvents;

        /// <summary>
        ///   Initializes a new instance of the <see cref="PrimeTestSystemClock"/> class
        ///   with virtual time set to <see cref="DateTimeOffset.UtcNow"/> at construction.
        /// </summary>
        public PrimeTestSystemClock ()
        {
            _utcNow = DateTimeOffset.UtcNow;
        }

        /// <summary>
        ///   Initializes a new instance of the <see cref="PrimeTestSystemClock"/> class
        ///   with the specified initial UTC time.
        /// </summary>
        /// <param name="initialUtcTime">
        ///   The initial virtual UTC time.
        /// </param>
        public PrimeTestSystemClock (DateTimeOffset initialUtcTime)
        {
            _utcNow = initialUtcTime;
        }

        #region IPrimeTestSystemClock Implementation

        /// <inheritdoc />
        public void SetTime (DateTimeOffset utcTime)
        {
            lock (_gate)
            {
                _utcNow = utcTime;
            }

            RaiseClockEvents(utcTime);
        }

        /// <inheritdoc />
        public void Advance (TimeSpan duration)
        {
            if (duration < TimeSpan.Zero)
                duration = TimeSpan.Zero;

            DateTimeOffset newNow;
            List<PendingDelay>? toComplete = null;
            List<TimeExpiryEntry>? toCancel = null;
            List<VirtualIntervalTimerBase>? intervalDue = null;
#if NET
            List<VirtualDayTimeTimerBase>? dayTimeDue = null;
#endif

            lock (_gate)
            {
                _utcNow += duration;
                newNow = _utcNow;

                foreach (PendingDelay pd in _pendingDelays)
                {
                    if (pd.DueUtc <= newNow)
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
                    if (tee.ExpireUtc <= newNow)
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

#if NET
                foreach (VirtualDayTimeTimerBase t in _dayTimeTimers)
                {
                    if (t.IsDue(newNow))
                    {
                        dayTimeDue ??= [];
                        dayTimeDue.Add(t);
                    }
                }
#endif
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

#if NET
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
#endif

            RaiseClockEvents(newNow);
        }

        /// <inheritdoc />
        public void RunFor (TimeSpan duration)
        {
            Advance(duration);
        }

        /// <inheritdoc />
        public void Start (TimeSpan? rate = null)
        {
            lock (_gate)
            {
                if (_isRunning)
                    return;
                _isRunning = true;
                _runRate = rate ?? TimeSpan.FromSeconds(1);
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

        #endregion IPrimeTestSystemClock Implementation

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

        #region IPrimeSystemClock Implementation — Now

        /// <inheritdoc />
        public DateTimeOffset LocalNow
        {
            get
            {
                lock (_gate)
                    return ToLocal(_utcNow);
            }
        }

        /// <inheritdoc />
        public DateTimeOffset UtcNow
        {
            get
            {
                lock (_gate)
                    return _utcNow;
            }
        }

        /// <inheritdoc />
        public DateTime LocalDateTimeNow
        {
            get
            {
                lock (_gate)
                    return ToLocal(_utcNow).DateTime;
            }
        }

        /// <inheritdoc />
        public DateTime UtcDateTimeNow
        {
            get
            {
                lock (_gate)
                    return _utcNow.UtcDateTime;
            }
        }

#if NET
        /// <inheritdoc />
        public TimeOnly LocalNowTime
        {
            get
            {
                lock (_gate)
                    return TimeOnly.FromDateTime(ToLocal(_utcNow).DateTime);
            }
        }

        /// <inheritdoc />
        public TimeOnly UtcNowTime
        {
            get
            {
                lock (_gate)
                    return TimeOnly.FromDateTime(_utcNow.UtcDateTime);
            }
        }

        /// <inheritdoc />
        public DateOnly LocalNowDate
        {
            get
            {
                lock (_gate)
                    return DateOnly.FromDateTime(ToLocal(_utcNow).DateTime);
            }
        }

        /// <inheritdoc />
        public DateOnly UtcNowDate
        {
            get
            {
                lock (_gate)
                    return DateOnly.FromDateTime(_utcNow.UtcDateTime);
            }
        }
#endif

        #endregion IPrimeSystemClock Implementation — Now

        #region IPrimeTime Implementation — Delays

        /// <inheritdoc />
        public void Sleep (TimeSpan sleepTime)
        {
            if (sleepTime <= TimeSpan.Zero)
                return;

            DateTimeOffset dueUtc;
            TaskCompletionSource<bool> tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);

            lock (_gate)
            {
                dueUtc = _utcNow + sleepTime;
                _pendingDelays.Add(new PendingDelay(dueUtc, tcs, null));
            }

            tcs.Task.GetAwaiter().GetResult();
        }

        /// <inheritdoc />
        public void Sleep (int sleepMilliseconds)
        {
            Sleep(TimeSpan.FromMilliseconds(sleepMilliseconds));
        }

        /// <inheritdoc />
        public Task DelayAsync (TimeSpan delayTime)
        {
            return DelayAsync(delayTime, CancellationToken.None);
        }

        /// <inheritdoc />
        public Task DelayAsync (int millisecondsDelay)
        {
            return DelayAsync(millisecondsDelay, CancellationToken.None);
        }

        /// <inheritdoc />
        public Task DelayAsync (TimeSpan delayTime, CancellationToken cancellationToken)
        {
            if (delayTime <= TimeSpan.Zero)
                return Task.CompletedTask;

            DateTimeOffset dueUtc;
            TaskCompletionSource<bool> tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);

            lock (_gate)
            {
                dueUtc = _utcNow + delayTime;
                _pendingDelays.Add(new PendingDelay(dueUtc, tcs, cancellationToken));
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

                return tcs.Task.ContinueWith(
                    (_, r) => ((CancellationTokenRegistration)r!).Dispose(),
                    reg,
                    CancellationToken.None,
                    TaskContinuationOptions.None,
                    TaskScheduler.Default);
            }

            return tcs.Task;
        }

        /// <inheritdoc />
        public Task DelayAsync (int millisecondsDelay, CancellationToken cancellationToken)
        {
            return DelayAsync(TimeSpan.FromMilliseconds(millisecondsDelay), cancellationToken);
        }

        #endregion IPrimeTime Implementation — Delays

        #region IPrimeTime Implementation — Time cancellation

        /// <inheritdoc />
        public TimeCancellationTokenSource GetTimeCancellationToken (TimeSpan cancelTime)
        {
            CancellationTokenSource cts = new();
            TimeCancellationTokenSource wrapper = new(cts);

            lock (_gate)
            {
                DateTimeOffset expireUtc = _utcNow + cancelTime;
                _timeExpiryEntries.Add(new TimeExpiryEntry(expireUtc, wrapper));
            }

            return wrapper;
        }

        /// <inheritdoc />
        public TimeCancellationTokenSource GetTimeCancellationToken (int cancelMilliseconds)
        {
            return GetTimeCancellationToken(TimeSpan.FromMilliseconds(cancelMilliseconds));
        }

        /// <inheritdoc />
        public TimeCancellationTokenSource LinkTimeCancellationToken (TimeSpan cancelTime, CancellationToken cancellationToken)
        {
            CancellationTokenSource timeCts = new();
            CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(timeCts.Token, cancellationToken);
            TimeCancellationTokenSource wrapper = new(linked, [timeCts]);

            lock (_gate)
            {
                DateTimeOffset expireUtc = _utcNow + cancelTime;
                _timeExpiryEntries.Add(new TimeExpiryEntry(expireUtc, wrapper, timeCts));
            }

            return wrapper;
        }

        /// <inheritdoc />
        public TimeCancellationTokenSource LinkTimeCancellationToken (int cancelMilliseconds, CancellationToken cancellationToken)
        {
            return LinkTimeCancellationToken(TimeSpan.FromMilliseconds(cancelMilliseconds), cancellationToken);
        }

        /// <inheritdoc />
        public TimeCancellationTokenSource LinkTimeCancellationToken (int cancelMilliseconds,
            CancellationToken token1,
            CancellationToken token2)
        {
            CancellationTokenSource timeCts = new();
            CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(timeCts.Token, token1, token2);
            TimeCancellationTokenSource wrapper = new(linked, [timeCts]);

            lock (_gate)
            {
                DateTimeOffset expireUtc = _utcNow + TimeSpan.FromMilliseconds(cancelMilliseconds);
                _timeExpiryEntries.Add(new TimeExpiryEntry(expireUtc, wrapper, timeCts));
            }

            return wrapper;
        }

        /// <inheritdoc />
        public TimeCancellationTokenSource LinkTimeCancellationToken (TimeSpan cancelTime,
            CancellationToken token1,
            CancellationToken token2)
        {
            CancellationTokenSource timeCts = new();
            CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(timeCts.Token, token1, token2);
            TimeCancellationTokenSource wrapper = new(linked, [timeCts]);

            lock (_gate)
            {
                DateTimeOffset expireUtc = _utcNow + cancelTime;
                _timeExpiryEntries.Add(new TimeExpiryEntry(expireUtc, wrapper, timeCts));
            }

            return wrapper;
        }

        /// <inheritdoc />
        public TimeCancellationTokenSource LinkTimeCancellationToken (TimeSpan cancelTime,
            params CancellationToken[] cancellationTokens)
        {
            CancellationTokenSource timeCts = new();
            CancellationToken[] all = new CancellationToken[cancellationTokens.Length + 1];
            all[0] = timeCts.Token;
            Array.Copy(cancellationTokens, 0, all, 1, cancellationTokens.Length);
            CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(all);
            TimeCancellationTokenSource wrapper = new(linked, [timeCts]);

            lock (_gate)
            {
                DateTimeOffset expireUtc = _utcNow + cancelTime;
                _timeExpiryEntries.Add(new TimeExpiryEntry(expireUtc, wrapper, timeCts));
            }

            return wrapper;
        }

        /// <inheritdoc />
        public TimeCancellationTokenSource LinkTimeCancellationToken (int cancelMilliseconds,
            params CancellationToken[] cancellationTokens)
        {
            return LinkTimeCancellationToken(TimeSpan.FromMilliseconds(cancelMilliseconds), cancellationTokens);
        }

        #endregion IPrimeTime Implementation — Time cancellation

        #region IPrimeSystemClock Implementation — Interval timers

        /// <inheritdoc />
        public IClockIntervalTimer RegisterTimer (TimeSpan callbackTime,
            Action callback,
            bool repeat = false,
            IntervalTimerOptions? timerOptions = null,
            CancellationToken cancellationToken = default)
        {
            VirtualIntervalTimerBase t = new VirtualIntervalTimer(
                this,
                callbackTime,
                repeat ? callbackTime : Timeout.InfiniteTimeSpan,
                IntervalTimerCallbackKind.SimpleAction,
                callback,
                null,
                timerOptions,
                cancellationToken);
            lock (_gate)
                _intervalTimers.Add(t);
            return t;
        }

        /// <inheritdoc />
        public IClockIntervalTimer RegisterTimer (TimeSpan callbackTime,
            Action<ClockTimerCallbackContext> callback,
            object? state = null,
            bool repeat = false,
            IntervalTimerOptions? timerOptions = null,
            CancellationToken cancellationToken = default)
        {
            VirtualIntervalTimerBase t = new VirtualIntervalTimer(
                this,
                callbackTime,
                repeat ? callbackTime : Timeout.InfiniteTimeSpan,
                IntervalTimerCallbackKind.ContextAction,
                callback,
                state,
                timerOptions,
                cancellationToken);
            lock (_gate)
                _intervalTimers.Add(t);
            return t;
        }

        /// <inheritdoc />
        public IClockIntervalTimer RegisterTimer (TimeSpan callbackTime,
            Action<ClockTimerCallbackContext, CancellationToken> callback,
            object? state = null,
            bool repeat = false,
            IntervalTimerOptions? timerOptions = null,
            CancellationToken cancellationToken = default)
        {
            VirtualIntervalTimerBase t = new VirtualIntervalTimer(
                this,
                callbackTime,
                repeat ? callbackTime : Timeout.InfiniteTimeSpan,
                IntervalTimerCallbackKind.ContextActionWithToken,
                callback,
                state,
                timerOptions,
                cancellationToken);
            lock (_gate)
                _intervalTimers.Add(t);
            return t;
        }

        /// <inheritdoc />
        public IClockIntervalTimer RegisterAsyncTimer (TimeSpan callbackTime,
            Func<CancellationToken, ValueTask> callback,
            bool repeat = false,
            IntervalTimerOptions? timerOptions = null,
            CancellationToken cancellationToken = default)
        {
            VirtualIntervalTimerBase t = new VirtualIntervalTimer(
                this,
                callbackTime,
                repeat ? callbackTime : Timeout.InfiniteTimeSpan,
                IntervalTimerCallbackKind.SimpleAsync,
                callback,
                null,
                timerOptions,
                cancellationToken);
            lock (_gate)
                _intervalTimers.Add(t);
            return t;
        }

        /// <inheritdoc />
        public IClockIntervalTimer RegisterAsyncTimer (TimeSpan callbackTime,
            Func<ClockTimerCallbackContext, CancellationToken, ValueTask> callback,
            object? state = null,
            bool repeat = false,
            IntervalTimerOptions? timerOptions = null,
            CancellationToken cancellationToken = default)
        {
            VirtualIntervalTimerBase t = new VirtualIntervalTimer(
                this,
                callbackTime,
                repeat ? callbackTime : Timeout.InfiniteTimeSpan,
                IntervalTimerCallbackKind.ContextAsync,
                callback,
                state,
                timerOptions,
                cancellationToken);
            lock (_gate)
                _intervalTimers.Add(t);
            return t;
        }

        /// <inheritdoc />
        public IClockIntervalTimer RegisterTimer (TimeSpan callbackTime,
            TimeSpan repeatInterval,
            Action callback,
            IntervalTimerOptions? timerOptions = null,
            CancellationToken cancellationToken = default)
        {
            VirtualIntervalTimerBase t = new VirtualIntervalTimer(
                this,
                callbackTime,
                repeatInterval,
                IntervalTimerCallbackKind.SimpleAction,
                callback,
                null,
                timerOptions,
                cancellationToken);
            lock (_gate)
                _intervalTimers.Add(t);
            return t;
        }

        /// <inheritdoc />
        public IClockIntervalTimer RegisterTimer (TimeSpan callbackTime,
            TimeSpan repeatInterval,
            Action<ClockTimerCallbackContext> callback,
            object? state = null,
            IntervalTimerOptions? timerOptions = null,
            CancellationToken cancellationToken = default)
        {
            VirtualIntervalTimerBase t = new VirtualIntervalTimer(
                this,
                callbackTime,
                repeatInterval,
                IntervalTimerCallbackKind.ContextAction,
                callback,
                state,
                timerOptions,
                cancellationToken);
            lock (_gate)
                _intervalTimers.Add(t);
            return t;
        }

        /// <inheritdoc />
        public IClockIntervalTimer RegisterTimer (TimeSpan callbackTime,
            TimeSpan repeatInterval,
            Action<ClockTimerCallbackContext, CancellationToken> callback,
            object? state = null,
            IntervalTimerOptions? timerOptions = null,
            CancellationToken cancellationToken = default)
        {
            VirtualIntervalTimerBase t = new VirtualIntervalTimer(
                this,
                callbackTime,
                repeatInterval,
                IntervalTimerCallbackKind.ContextActionWithToken,
                callback,
                state,
                timerOptions,
                cancellationToken);
            lock (_gate)
                _intervalTimers.Add(t);
            return t;
        }

        /// <inheritdoc />
        public IClockIntervalTimer RegisterAsyncTimer (TimeSpan callbackTime,
            TimeSpan repeatInterval,
            Func<CancellationToken, ValueTask> callback,
            IntervalTimerOptions? timerOptions = null,
            CancellationToken cancellationToken = default)
        {
            VirtualIntervalTimerBase t = new VirtualIntervalTimer(
                this,
                callbackTime,
                repeatInterval,
                IntervalTimerCallbackKind.SimpleAsync,
                callback,
                null,
                timerOptions,
                cancellationToken);
            lock (_gate)
                _intervalTimers.Add(t);
            return t;
        }

        /// <inheritdoc />
        public IClockIntervalTimer RegisterAsyncTimer (TimeSpan callbackTime,
            TimeSpan repeatInterval,
            Func<ClockTimerCallbackContext, CancellationToken, ValueTask> callback,
            object? state = null,
            IntervalTimerOptions? timerOptions = null,
            CancellationToken cancellationToken = default)
        {
            VirtualIntervalTimerBase t = new VirtualIntervalTimer(
                this,
                callbackTime,
                repeatInterval,
                IntervalTimerCallbackKind.ContextAsync,
                callback,
                state,
                timerOptions,
                cancellationToken);
            lock (_gate)
                _intervalTimers.Add(t);
            return t;
        }

        #endregion IPrimeSystemClock Implementation — Interval timers

#if NET
        #region IPrimeSystemClock Implementation — Day-time timers

        /// <inheritdoc />
        public IClockDayTimeTimer RegisterTimeOfDay (LocalTimeOfDay timeOfDay,
            Action callback,
            DayTimeTimerOptions? timerOptions = null,
            CancellationToken cancellationToken = default) =>
            RegisterTimeOfDayLocal(timeOfDay.Value, IntervalTimerCallbackKind.SimpleAction, callback, null, timerOptions, cancellationToken);

        /// <inheritdoc />
        public IClockDayTimeTimer RegisterTimeOfDay (LocalTimeOfDay timeOfDay,
            Action<ClockTimerCallbackContext> callback,
            object? state = null,
            DayTimeTimerOptions? timerOptions = null,
            CancellationToken cancellationToken = default) =>
            RegisterTimeOfDayLocal(timeOfDay.Value, IntervalTimerCallbackKind.ContextAction, callback, state, timerOptions, cancellationToken);

        /// <inheritdoc />
        public IClockDayTimeTimer RegisterTimeOfDay (LocalTimeOfDay timeOfDay,
            Action<ClockTimerCallbackContext, CancellationToken> callback,
            object? state = null,
            DayTimeTimerOptions? timerOptions = null,
            CancellationToken cancellationToken = default) =>
            RegisterTimeOfDayLocal(timeOfDay.Value, IntervalTimerCallbackKind.ContextActionWithToken, callback, state, timerOptions, cancellationToken);

        /// <inheritdoc />
        public IClockDayTimeTimer RegisterAsyncTimeOfDay (LocalTimeOfDay timeOfDay,
            Func<CancellationToken, ValueTask> callback,
            DayTimeTimerOptions? timerOptions = null,
            CancellationToken cancellationToken = default) =>
            RegisterTimeOfDayLocal(timeOfDay.Value, IntervalTimerCallbackKind.SimpleAsync, callback, null, timerOptions, cancellationToken);

        /// <inheritdoc />
        public IClockDayTimeTimer RegisterAsyncTimeOfDay (LocalTimeOfDay timeOfDay,
            Func<ClockTimerCallbackContext, CancellationToken, ValueTask> callback,
            object? state = null,
            DayTimeTimerOptions? timerOptions = null,
            CancellationToken cancellationToken = default) =>
            RegisterTimeOfDayLocal(timeOfDay.Value, IntervalTimerCallbackKind.ContextAsync, callback, state, timerOptions, cancellationToken);

        /// <inheritdoc />
        public IClockDayTimeTimer RegisterTimeOfDay (UtcTimeOfDay timeOfDay,
            Action callback,
            DayTimeTimerOptions? timerOptions = null,
            CancellationToken cancellationToken = default) =>
            RegisterTimeOfDayUtc(timeOfDay.Value, IntervalTimerCallbackKind.SimpleAction, callback, null, timerOptions, cancellationToken);

        /// <inheritdoc />
        public IClockDayTimeTimer RegisterTimeOfDay (UtcTimeOfDay timeOfDay,
            Action<ClockTimerCallbackContext> callback,
            object? state = null,
            DayTimeTimerOptions? timerOptions = null,
            CancellationToken cancellationToken = default) =>
            RegisterTimeOfDayUtc(timeOfDay.Value, IntervalTimerCallbackKind.ContextAction, callback, state, timerOptions, cancellationToken);

        /// <inheritdoc />
        public IClockDayTimeTimer RegisterTimeOfDay (UtcTimeOfDay timeOfDay,
            Action<ClockTimerCallbackContext, CancellationToken> callback,
            object? state = null,
            DayTimeTimerOptions? timerOptions = null,
            CancellationToken cancellationToken = default) =>
            RegisterTimeOfDayUtc(timeOfDay.Value, IntervalTimerCallbackKind.ContextActionWithToken, callback, state, timerOptions, cancellationToken);

        /// <inheritdoc />
        public IClockDayTimeTimer RegisterAsyncTimeOfDay (UtcTimeOfDay timeOfDay,
            Func<CancellationToken, ValueTask> callback,
            DayTimeTimerOptions? timerOptions = null,
            CancellationToken cancellationToken = default) =>
            RegisterTimeOfDayUtc(timeOfDay.Value, IntervalTimerCallbackKind.SimpleAsync, callback, null, timerOptions, cancellationToken);

        /// <inheritdoc />
        public IClockDayTimeTimer RegisterAsyncTimeOfDay (UtcTimeOfDay timeOfDay,
            Func<ClockTimerCallbackContext, CancellationToken, ValueTask> callback,
            object? state = null,
            DayTimeTimerOptions? timerOptions = null,
            CancellationToken cancellationToken = default) =>
            RegisterTimeOfDayUtc(timeOfDay.Value, IntervalTimerCallbackKind.ContextAsync, callback, state, timerOptions, cancellationToken);

        #endregion IPrimeSystemClock Implementation — Day-time timers
#endif

        #region Private helpers

        private static DateTimeOffset ToLocal (DateTimeOffset utc)
        {
            TimeSpan offset = TimeZoneInfo.Local.GetUtcOffset(utc.DateTime);
            return new DateTimeOffset(utc.UtcDateTime + offset, offset);
        }

        private void RunLoop ()
        {
            while (true)
            {
                Thread.Sleep(1000);
                TimeSpan toAdvance;
                lock (_gate)
                {
                    if (!_isRunning)
                        return;
                    toAdvance = _runRate;
                }

                Advance(toAdvance);
            }
        }

        private void RaiseClockEvents (DateTimeOffset utcNow)
        {
            ClockEvents?.Invoke(this, new ClockTimeChangedEventArgs(utcNow));
        }

        private void RemoveIntervalTimer (VirtualIntervalTimerBase timer)
        {
            lock (_gate)
                _intervalTimers.Remove(timer);
        }

#if NET
        private void RemoveDayTimeTimer (VirtualDayTimeTimerBase timer)
        {
            lock (_gate)
                _dayTimeTimers.Remove(timer);
        }

        private IClockDayTimeTimer RegisterTimeOfDayLocal (TimeOnly targetTimeOfDay,
            IntervalTimerCallbackKind kind,
            Delegate callback,
            object? state,
            DayTimeTimerOptions? options,
            CancellationToken cancellationToken)
        {
            VirtualDayTimeTimerBase t = new VirtualDayTimeTimer(
                this,
                isLocal: true,
                targetTimeOfDay,
                kind,
                callback,
                state,
                options,
                cancellationToken);
            lock (_gate)
                _dayTimeTimers.Add(t);
            return t;
        }

        private IClockDayTimeTimer RegisterTimeOfDayUtc (TimeOnly targetTimeOfDay,
            IntervalTimerCallbackKind kind,
            Delegate callback,
            object? state,
            DayTimeTimerOptions? options,
            CancellationToken cancellationToken)
        {
            VirtualDayTimeTimerBase t = new VirtualDayTimeTimer(
                this,
                isLocal: false,
                targetTimeOfDay,
                kind,
                callback,
                state,
                options,
                cancellationToken);
            lock (_gate)
                _dayTimeTimers.Add(t);
            return t;
        }
#endif

        #endregion Private helpers

        #region Nested types — Pending delay and time expiry

        private sealed class PendingDelay
        {
            public DateTimeOffset DueUtc { get; }
            public TaskCompletionSource<bool> TaskCompletionSource { get; }

            public PendingDelay (DateTimeOffset dueUtc, TaskCompletionSource<bool> tcs, CancellationToken? cancellationToken)
            {
                DueUtc = dueUtc;
                TaskCompletionSource = tcs;
            }

            public void Complete ()
            {
                TaskCompletionSource.TrySetResult(true);
            }
        }

        private sealed class TimeExpiryEntry
        {
            public DateTimeOffset ExpireUtc { get; }
            private readonly TimeCancellationTokenSource _wrapper;

            public TimeExpiryEntry (DateTimeOffset expireUtc, TimeCancellationTokenSource wrapper,
                CancellationTokenSource? timeCts = null)
            {
                ExpireUtc = expireUtc;
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

        private abstract class VirtualIntervalTimerBase : IClockIntervalTimer
        {
            protected readonly PrimeTestSystemClock Clock;
            protected readonly IntervalTimerCallbackKind CallbackKind;
            protected readonly Delegate Callback;
            protected readonly object? CallbackState;
            protected readonly CancellationToken CancellationToken;
#if NET10_OR_GREATER
            protected readonly Lock Gate = new();
#else
            protected readonly object Gate = new();
#endif
            protected DateTimeOffset? NextDueUtc;
            protected DateTimeOffset? LastCallbackUtc;
            protected TimerState State = TimerState.Active;
            protected bool _enabled = true;
            protected bool Disposed;
            protected bool CancelRequested;
            protected int CallbacksRunning;
            protected TimeSpan InitialCallbackTime;
            protected TimeSpan RepeatInterval;
            private readonly bool _isLocalTimeRepresentation;
            private readonly bool _resetAfterCallback;

            protected VirtualIntervalTimerBase (
                PrimeTestSystemClock clock,
                TimeSpan initialCallbackTime,
                TimeSpan repeatInterval,
                IntervalTimerCallbackKind callbackKind,
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
                _isLocalTimeRepresentation = options?.LocalTimeRepresentation == true;
                _resetAfterCallback = options?.ResetIntervalAfterCallback ?? false;
                Id = Interlocked.Increment(ref _nextTimerId);
                DateTimeOffset now = clock.UtcNow;
                RegisteredTime = _isLocalTimeRepresentation ? clock.LocalNow : now;
                NextDueUtc = now + initialCallbackTime;

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

            public int Id { get; }
            public DateTimeOffset RegisteredTime { get; }
            public bool IsTimeOfDay => false;
            public bool IsRepeating => RepeatInterval != Timeout.InfiniteTimeSpan && RepeatInterval > TimeSpan.Zero;
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
                        if (LastCallbackUtc is not { } last)
                            return -1;
                        if (CallbacksRunning > 0)
                            return 0;
                        DateTimeOffset now = Clock.UtcNow;
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
                        if (!IsRepeating && LastCallbackUtc.HasValue)
                            return -1;
                        if (NextDueUtc is not { } next)
                            return -1;
                        DateTimeOffset now = Clock.UtcNow;
                        if (next <= now)
                            return 0;
                        if (CallbacksRunning > 0 && IsResetAfterCallback)
                            return (long)RepeatInterval.TotalMilliseconds;
                        return (long)(next - now).TotalMilliseconds;
                    }
                }
            }

            public bool IsDue (DateTimeOffset now)
            {
                lock (Gate)
                {
                    if (Disposed || CancelRequested || State == TimerState.Cancelled || !_enabled)
                        return false;
                    if (NextDueUtc is not { } next)
                        return false;
                    return next <= now;
                }
            }

            public abstract void RunDueCallback (DateTimeOffset now);

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

            public bool Change (TimeSpan interval) =>
                Change(interval, IsRepeating ? interval : Timeout.InfiniteTimeSpan);

            public bool Change (TimeSpan nextInterval, TimeSpan repeatInterval)
            {
                lock (Gate)
                {
                    if (Disposed || State == TimerState.Cancelled)
                        return false;
                    if (!IsRepeating && repeatInterval != Timeout.InfiniteTimeSpan && repeatInterval > TimeSpan.Zero)
                        throw new InvalidOperationException(
                            "Cannot change a non-repeating timer to a repeating timer.");
                    InitialCallbackTime = nextInterval;
                    RepeatInterval = repeatInterval;
                    if (State == TimerState.Completed)
                        State = TimerState.Active;
                    if (!_enabled)
                        return true;
                    NextDueUtc = Clock.UtcNow + nextInterval;
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
                    NextDueUtc = Clock.UtcNow + InitialCallbackTime;
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
                PrimeTestSystemClock clock,
                TimeSpan initialCallbackTime,
                TimeSpan repeatInterval,
                IntervalTimerCallbackKind callbackKind,
                Delegate callback,
                object? callbackState,
                IntervalTimerOptions? options,
                CancellationToken cancellationToken)
                : base(clock, initialCallbackTime, repeatInterval, callbackKind, callback, callbackState, options, cancellationToken)
            {
            }

            public override void RunDueCallback (DateTimeOffset now)
            {
                bool resetAfter;
                bool isRepeating;
                DateTimeOffset firedAt;

                lock (Gate)
                {
                    if (Disposed || CancelRequested || State == TimerState.Cancelled || !_enabled)
                        return;
                    if (NextDueUtc is not { } next || next > now)
                        return;
                    firedAt = next;
                    NextDueUtc = null;
                    LastCallbackUtc = firedAt;
                    isRepeating = IsRepeating;
                    resetAfter = IsResetAfterCallback;
                    State = isRepeating && !resetAfter ? TimerState.RepeatProcessingCallback : TimerState.ProcessingCallback;
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

            private void RunCallback (bool resetAfter, bool isRepeating, DateTimeOffset now)
            {
                void InvokeSync (Action run)
                {
                    run();
                }

                switch (CallbackKind)
                {
                    case IntervalTimerCallbackKind.SimpleAction:
                        InvokeSync(() => ((Action)Callback)());
                        break;
                    case IntervalTimerCallbackKind.ContextAction:
                        InvokeSync(() => ((Action<ClockTimerCallbackContext>)Callback)(
                            new ClockTimerCallbackContext(this, CallbackState)));
                        break;
                    case IntervalTimerCallbackKind.ContextActionWithToken:
                        InvokeSync(() => ((Action<ClockTimerCallbackContext, CancellationToken>)Callback)(
                            new ClockTimerCallbackContext(this, CallbackState), CancellationToken));
                        break;
                    case IntervalTimerCallbackKind.SimpleAsync:
                        RunAsyncAndScheduleAfter(
                            () => ((Func<CancellationToken, ValueTask>)Callback)(CancellationToken),
                            resetAfter,
                            isRepeating,
                            now);
                        return;
                    case IntervalTimerCallbackKind.ContextAsync:
                        RunAsyncAndScheduleAfter(
                            () => ((Func<ClockTimerCallbackContext, CancellationToken, ValueTask>)Callback)(
                                new ClockTimerCallbackContext(this, CallbackState), CancellationToken),
                            resetAfter,
                            isRepeating,
                            now);
                        return;
                    default:
                        throw new InvalidOperationException($"Unsupported callback kind: {CallbackKind}");
                }

                OnSyncCallbackCompleted(resetAfter, isRepeating, now);
            }

            private void RunAsyncAndScheduleAfter (Func<ValueTask> run, bool resetAfter, bool isRepeating, DateTimeOffset now)
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
                        (VirtualIntervalTimer reg, bool ra, bool rep, DateTimeOffset n) =
                            ((VirtualIntervalTimer, bool, bool, DateTimeOffset))state!;
                        reg.OnAsyncCallbackCompleted(ra, rep, n);
                    },
                    (this, resetAfter, isRepeating, now),
                    CancellationToken.None,
                    TaskContinuationOptions.None,
                    TaskScheduler.Default);
            }

            private void OnSyncCallbackCompleted (bool resetAfter, bool isRepeating, DateTimeOffset now)
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
                    NextDueUtc = now + RepeatInterval;
                }
            }

            private void OnAsyncCallbackCompleted (bool resetAfter, bool isRepeating, DateTimeOffset now)
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
                    NextDueUtc = now + RepeatInterval;
                }
            }
        }

        #endregion Nested types — Virtual interval timer

#if NET
        #region Nested types — Virtual day-time timer

        private abstract class VirtualDayTimeTimerBase : IClockDayTimeTimer
        {
            protected readonly PrimeTestSystemClock Clock;
            protected readonly bool IsLocal;
            protected readonly IntervalTimerCallbackKind CallbackKind;
            protected readonly Delegate Callback;
            protected readonly object? CallbackState;
            protected readonly CancellationToken CancellationToken;
#if NET10_OR_GREATER
            protected readonly Lock Gate = new();
#else
            protected readonly object Gate = new();
#endif
            protected DateTimeOffset? NextDueUtc;
            protected TimerState State = TimerState.Active;
            protected bool _enabledDayTime = true;
            protected bool Disposed;
            protected bool CancelRequested;
            protected int CallbacksRunning;
            protected TimeOnly TargetTimeOfDay;

            protected VirtualDayTimeTimerBase (
                PrimeTestSystemClock clock,
                bool isLocal,
                TimeOnly targetTimeOfDay,
                IntervalTimerCallbackKind callbackKind,
                Delegate callback,
                object? callbackState,
                DayTimeTimerOptions? options,
                CancellationToken cancellationToken)
            {
                Clock = clock;
                IsLocal = isLocal;
                TargetTimeOfDay = targetTimeOfDay;
                CallbackKind = callbackKind;
                Callback = callback;
                CallbackState = callbackState;
                CancellationToken = cancellationToken;
                ConcurrentTriggerProcessing = options?.ConcurrentTriggerProcessing ?? ConcurrentTriggerProcessing.RunSequentially;
                SkippedTimeBehavior = options?.SkippedTimeBehavior ?? SkippedTimeBehavior.RunAfter;
                DuplicateTimeBehavior = options?.DuplicateTimeBehavior ?? DuplicateTimeBehavior.RunFirst;
                Id = Interlocked.Increment(ref _nextTimerId);
                RegisteredTime = Clock.UtcNow;
                NextDueUtc = ComputeNextDue(clock.UtcNow);

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

            private static readonly TimeSpan OneDay = TimeSpan.FromDays(1);

            public int Id { get; }
            public DateTimeOffset RegisteredTime { get; }
            public bool IsTimeOfDay => true;
            public bool IsLocalTimeRepresentation => IsLocal;
            public bool IsRepeating => true;
            public bool IsCancelled => State == TimerState.Cancelled;
            public bool IsActive =>
                State != TimerState.Cancelled && State != TimerState.Disposed && _enabledDayTime;
            public bool CallbacksProcessing => CallbacksRunning > 0;
            public ConcurrentTriggerProcessing ConcurrentTriggerProcessing { get; }
            public SkippedTimeBehavior SkippedTimeBehavior { get; }
            public DuplicateTimeBehavior DuplicateTimeBehavior { get; }

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
                            NextDueUtc = ComputeNextDue(Clock.UtcNow);
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
                        if (NextDueUtc is not { } next)
                            return -1;
                        DateTimeOffset now = Clock.UtcNow;
                        if (next <= now)
                            return 0;
                        return (long)(next - now).TotalMilliseconds;
                    }
                }
            }

            public bool IsDue (DateTimeOffset now)
            {
                lock (Gate)
                {
                    if (Disposed || CancelRequested || State == TimerState.Cancelled || !_enabledDayTime)
                        return false;
                    if (NextDueUtc is not { } next)
                        return false;
                    return next <= now;
                }
            }

            public abstract void RunDueCallback (DateTimeOffset now);

            protected DateTimeOffset? ComputeNextDue (DateTimeOffset now)
            {
                if (IsLocal)
                {
                    DateTimeOffset localNow = Clock.LocalNow;
                    DateOnly today = DateOnly.FromDateTime(localNow.DateTime);
                    DateTime nextDt = today.ToDateTime(TargetTimeOfDay);
                    if (nextDt <= localNow.DateTime)
                        nextDt = today.AddDays(1).ToDateTime(TargetTimeOfDay);
                    DateTimeOffset nextLocal = new(nextDt, localNow.Offset);
                    return nextLocal.ToUniversalTime();
                }
                else
                {
                    DateTime utcDate = now.UtcDateTime;
                    DateOnly today = DateOnly.FromDateTime(utcDate);
                    DateTime nextDt = today.ToDateTime(TargetTimeOfDay);
                    if (nextDt <= utcDate)
                        nextDt = today.AddDays(1).ToDateTime(TargetTimeOfDay);
                    return new DateTimeOffset(nextDt, TimeSpan.Zero);
                }
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

            public bool Change (LocalTimeOfDay newTimeOfDay)
            {
                if (!IsLocal)
                    return false;
                lock (Gate)
                {
                    if (Disposed || State == TimerState.Cancelled)
                        return false;
                    TargetTimeOfDay = newTimeOfDay.Value;
                    if (Enabled)
                        NextDueUtc = ComputeNextDue(Clock.UtcNow);
                    return true;
                }
            }

            public bool Change (UtcTimeOfDay newTimeOfDay)
            {
                if (IsLocal)
                    return false;
                lock (Gate)
                {
                    if (Disposed || State == TimerState.Cancelled)
                        return false;
                    TargetTimeOfDay = newTimeOfDay.Value;
                    if (Enabled)
                        NextDueUtc = ComputeNextDue(Clock.UtcNow);
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
                    Enabled = false;
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
                    NextDueUtc = ComputeNextDue(Clock.UtcNow);
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
                PrimeTestSystemClock clock,
                bool isLocal,
                TimeOnly targetTimeOfDay,
                IntervalTimerCallbackKind callbackKind,
                Delegate callback,
                object? callbackState,
                DayTimeTimerOptions? options,
                CancellationToken cancellationToken)
                : base(clock, isLocal, targetTimeOfDay, callbackKind, callback, callbackState, options, cancellationToken)
            {
            }

            public override void RunDueCallback (DateTimeOffset now)
            {
                lock (Gate)
                {
                    if (Disposed || CancelRequested || State == TimerState.Cancelled || !_enabledDayTime)
                        return;
                    if (NextDueUtc is not { } next || next > now)
                        return;
                    NextDueUtc = ComputeNextDue(now);
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
                    case IntervalTimerCallbackKind.SimpleAction:
                        ((Action)Callback)();
                        break;
                    case IntervalTimerCallbackKind.ContextAction:
                        ((Action<ClockTimerCallbackContext>)Callback)(
                            new ClockTimerCallbackContext(this, CallbackState));
                        break;
                    case IntervalTimerCallbackKind.ContextActionWithToken:
                        ((Action<ClockTimerCallbackContext, CancellationToken>)Callback)(
                            new ClockTimerCallbackContext(this, CallbackState), CancellationToken);
                        break;
                    case IntervalTimerCallbackKind.SimpleAsync:
                        ((Func<CancellationToken, ValueTask>)Callback)(CancellationToken).AsTask().GetAwaiter().GetResult();
                        break;
                    case IntervalTimerCallbackKind.ContextAsync:
                        ((Func<ClockTimerCallbackContext, CancellationToken, ValueTask>)Callback)(
                            new ClockTimerCallbackContext(this, CallbackState), CancellationToken).AsTask().GetAwaiter().GetResult();
                        break;
                    default:
                        throw new InvalidOperationException($"Unsupported callback kind: {CallbackKind}");
                }
            }
        }

        #endregion Nested types — Virtual day-time timer
#endif
    }
}
