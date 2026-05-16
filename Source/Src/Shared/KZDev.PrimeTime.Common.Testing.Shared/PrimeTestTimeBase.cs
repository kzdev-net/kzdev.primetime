// Copyright (c) Kevin Zehrer
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Diagnostics;


#if SYSTEMCLOCK
namespace KZDev.SystemClock.PrimeTime.Testing;
#else
using NodaTime;

namespace KZDev.PrimeTime.Testing;
#endif

//################################################################################
/// <summary>
///   Shared implementation of <see cref="IPrimeTestTime"/> virtual time, delays,
///   and cancellation tokens.
/// </summary>
public abstract partial class PrimeTestTimeBase : IPrimeTestTime
{
    #region Nested types — Pending delay and time expiry

    //============================================================================
    /// <summary>
    ///   A virtual-time delay that completes a <see cref="TaskCompletionSource{TResult}"/> when
    ///   <see cref="PrimeTestClock.Advance(System.TimeSpan)"/> reaches <see cref="DueUtc"/>.
    /// </summary>
    internal sealed class PendingDelay
    {
        //------------------------------------------------------------------------
        /// <summary>
        ///   Initializes a new instance of the <see cref="PendingDelay"/> class.
        /// </summary>
        /// <param name="dueUtc">Virtual UTC instant when the delay completes.</param>
        /// <param name="taskCompletionSource">Completion source signaled when due.</param>
        public PendingDelay (DateTimeOffset dueUtc, TaskCompletionSource<bool> taskCompletionSource)
        {
            DueUtc = dueUtc;
            TaskCompletionSource = taskCompletionSource;
        }
        //------------------------------------------------------------------------
        /// <summary>
        ///   Gets the virtual UTC instant when this delay completes.
        /// </summary>
        public DateTimeOffset DueUtc { [DebuggerStepThrough] get; }
        //------------------------------------------------------------------------
        /// <summary>
        ///   Gets the task completion source completed when the delay elapses.
        /// </summary>
        public TaskCompletionSource<bool> TaskCompletionSource { [DebuggerStepThrough] get; }
        //------------------------------------------------------------------------
        /// <summary>
        ///   Completes the delay successfully if not already completed.
        /// </summary>
        public void Complete ()
        {
            TaskCompletionSource.TrySetResult(true);
        }
        //------------------------------------------------------------------------
    }
    //============================================================================
    /// <summary>
    ///   A time-based cancellation entry that cancels its wrapper when virtual UTC reaches
    ///   <see cref="ExpireUtc"/>.
    /// </summary>
    internal sealed class TimeExpiryEntry
    {
        //------------------------------------------------------------------------
        /// <summary>
        ///   Cancellation wrapper whose token is cancelled when virtual time reaches <see cref="ExpireUtc"/>.
        /// </summary>
        private readonly TimeCancellationTokenSource _wrapper;
        //------------------------------------------------------------------------
        /// <summary>
        ///   Initializes a new instance of the <see cref="TimeExpiryEntry"/> class.
        /// </summary>
        /// <param name="expireUtc">Virtual UTC instant when cancellation is requested.</param>
        /// <param name="wrapper">Wrapper whose token should be cancelled at expiry.</param>
        /// <param name="timeCts">Unused; reserved for future use.</param>
        public TimeExpiryEntry (DateTimeOffset expireUtc, TimeCancellationTokenSource wrapper,
            CancellationTokenSource? timeCts = null)
        {
            ExpireUtc = expireUtc;
            _wrapper = wrapper;
        }
        //------------------------------------------------------------------------
        /// <summary>
        ///   Gets the virtual UTC instant when cancellation is requested.
        /// </summary>
        public DateTimeOffset ExpireUtc { [DebuggerStepThrough] get; }
        //------------------------------------------------------------------------
        /// <summary>
        ///   Requests cancellation on the wrapper, ignoring <see cref="ObjectDisposedException"/> if already disposed.
        /// </summary>
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
        //------------------------------------------------------------------------
    }
    //============================================================================

    #endregion Nested types — Pending delay and time expiry

    //----------------------------------------------------------------------------
#if NET10_OR_GREATER
    /// <summary>
    ///   Synchronizes virtual time, pending delays, expiries, and timer lists.
    /// </summary>
    internal readonly Lock Gate = new();
#else
    /// <summary>
    ///   Synchronizes virtual time, pending delays, expiries, and timer lists.
    /// </summary>
    internal readonly object Gate = new();
#endif

    /// <summary>
    ///   Pending Sleep and DelayAsync completions ordered by due instant.
    /// </summary>
    internal List<PendingDelay> PendingDelays { [DebuggerStepThrough] get; } = [];

    /// <summary>
    ///   Active time-based cancellation entries ordered by expiry instant.
    /// </summary>
    internal List<TimeExpiryEntry> TimeExpiryEntries { [DebuggerStepThrough] get; } = [];

    /// <summary>
    ///   Whether Start(TimeSpan?) is driving automatic Advance(TimeSpan) on a background thread.
    /// </summary>
    internal bool InternalIsRunning { [DebuggerStepThrough] get; [DebuggerStepThrough] set; }

    /// <summary>
    ///   Reads the virtual UTC instant while <see cref="Gate"/> is held.
    /// </summary>
    /// <returns>The current virtual UTC time.</returns>
    internal partial DateTimeOffset ReadVirtualUtcNowLocked ();
    //----------------------------------------------------------------------------
    /// <summary>
    ///   Called while <see cref="Gate"/> is held when pending delays, time expiries, or their lists change in a way
    ///   that can affect the automatic runner&apos;s next deadline.
    /// </summary>
    protected virtual void OnSchedulingMutatedWhileGateHeld ()
    {
    }
    //----------------------------------------------------------------------------

    #region Interface Implementations

    #region IPrimeTestTime Implementation

    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public bool IsRunning
    {
        get
        {
            lock (Gate)
            {
                return InternalIsRunning;
            }
        }
    }
    //----------------------------------------------------------------------------

    #endregion IPrimeTestTime Implementation

    #region IPrimeTime Implementation — Delays

    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public void Sleep (TimeSpan sleepTime)
    {
        if (sleepTime <= TimeSpan.Zero)
            return;

        TaskCompletionSource<bool> taskCompletionSource = new(TaskCreationOptions.RunContinuationsAsynchronously);

        lock (Gate)
        {
            DateTimeOffset dueUtc = ReadVirtualUtcNowLocked() + sleepTime;
            PendingDelays.Add(new PendingDelay(dueUtc, taskCompletionSource));
            OnSchedulingMutatedWhileGateHeld();
        }

        taskCompletionSource.Task.GetAwaiter().GetResult();
    }
    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public void Sleep (int sleepMilliseconds)
    {
        Sleep(TimeSpan.FromMilliseconds(sleepMilliseconds));
    }
    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public Task DelayAsync (TimeSpan delayTime)
    {
        return DelayAsync(delayTime, CancellationToken.None);
    }
    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public Task DelayAsync (int millisecondsDelay)
    {
        return DelayAsync(millisecondsDelay, CancellationToken.None);
    }
    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public Task DelayAsync (TimeSpan delayTime, CancellationToken cancellationToken)
    {
        if (delayTime <= TimeSpan.Zero)
            return Task.CompletedTask;

        TaskCompletionSource<bool> taskCompletionSource = new(TaskCreationOptions.RunContinuationsAsynchronously);

        lock (Gate)
        {
            DateTimeOffset dueUtc = ReadVirtualUtcNowLocked() + delayTime;
            PendingDelays.Add(new PendingDelay(dueUtc, taskCompletionSource));
            OnSchedulingMutatedWhileGateHeld();
        }

        if (!cancellationToken.CanBeCanceled)
        {
            return taskCompletionSource.Task;
        }

        CancellationTokenRegistration cancellationRegistration = cancellationToken.Register(static args =>
        {
            (PrimeTestTimeBase @this, TaskCompletionSource<bool> taskCompletion, CancellationToken cancellationToken) =
                (Tuple<PrimeTestTimeBase, TaskCompletionSource<bool>, CancellationToken>)args!;
            lock (@this.Gate)
            {
                if (@this.PendingDelays.RemoveAll(pendingDelay => pendingDelay.TaskCompletionSource == taskCompletion) > 0)
                {
                    taskCompletion.TrySetCanceled(cancellationToken);
                    @this.OnSchedulingMutatedWhileGateHeld();
                }
            }
        }, Tuple.Create(this, taskCompletionSource, cancellationToken));

        _ = taskCompletionSource.Task.ContinueWith((_, state) => ((CancellationTokenRegistration)state!).Dispose(),
            cancellationRegistration, CancellationToken.None, TaskContinuationOptions.None, TaskScheduler.Default);

        return taskCompletionSource.Task;
    }
    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public Task DelayAsync (int millisecondsDelay, CancellationToken cancellationToken)
    {
        return DelayAsync(TimeSpan.FromMilliseconds(millisecondsDelay), cancellationToken);
    }
    //----------------------------------------------------------------------------

    #endregion IPrimeTime Implementation — Delays

    #region IPrimeTime Implementation — Time cancellation

    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public TimeCancellationTokenSource GetTimeCancellationToken (TimeSpan cancelTime)
    {
        CancellationTokenSource cts = new();
        TimeCancellationTokenSource wrapper = new(cts);

        lock (Gate)
        {
            DateTimeOffset expireUtc = ReadVirtualUtcNowLocked() + cancelTime;
            TimeExpiryEntries.Add(new TimeExpiryEntry(expireUtc, wrapper));
            OnSchedulingMutatedWhileGateHeld();
        }

        return wrapper;
    }
    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public TimeCancellationTokenSource GetTimeCancellationToken (int cancelMilliseconds)
    {
        return GetTimeCancellationToken(TimeSpan.FromMilliseconds(cancelMilliseconds));
    }
    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public TimeCancellationTokenSource LinkTimeCancellationToken (int cancelMilliseconds,
        CancellationToken token1,
        CancellationToken token2)
    {
        CancellationTokenSource timeCts = new();
        CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(timeCts.Token, token1, token2);
        TimeCancellationTokenSource wrapper = new(linked, [timeCts]);

        lock (Gate)
        {
            DateTimeOffset expireUtc = ReadVirtualUtcNowLocked() + TimeSpan.FromMilliseconds(cancelMilliseconds);
            TimeExpiryEntries.Add(new TimeExpiryEntry(expireUtc, wrapper, timeCts));
            OnSchedulingMutatedWhileGateHeld();
        }

        return wrapper;
    }
    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public TimeCancellationTokenSource LinkTimeCancellationToken (TimeSpan cancelTime,
        CancellationToken token1,
        CancellationToken token2)
    {
        CancellationTokenSource timeCts = new();
        CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(timeCts.Token, token1, token2);
        TimeCancellationTokenSource wrapper = new(linked, [timeCts]);

        lock (Gate)
        {
            DateTimeOffset expireUtc = ReadVirtualUtcNowLocked() + cancelTime;
            TimeExpiryEntries.Add(new TimeExpiryEntry(expireUtc, wrapper, timeCts));
            OnSchedulingMutatedWhileGateHeld();
        }

        return wrapper;
    }
    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public TimeCancellationTokenSource LinkTimeCancellationToken (TimeSpan cancelTime, CancellationToken cancellationToken)
    {
        CancellationTokenSource timeCts = new();
        CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(timeCts.Token, cancellationToken);
        TimeCancellationTokenSource wrapper = new(linked, [timeCts]);

        lock (Gate)
        {
            DateTimeOffset expireUtc = ReadVirtualUtcNowLocked() + cancelTime;
            TimeExpiryEntries.Add(new TimeExpiryEntry(expireUtc, wrapper, timeCts));
            OnSchedulingMutatedWhileGateHeld();
        }

        return wrapper;
    }
    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public TimeCancellationTokenSource LinkTimeCancellationToken (int cancelMilliseconds, CancellationToken cancellationToken)
    {
        return LinkTimeCancellationToken(TimeSpan.FromMilliseconds(cancelMilliseconds), cancellationToken);
    }
    //----------------------------------------------------------------------------
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

        lock (Gate)
        {
            DateTimeOffset expireUtc = ReadVirtualUtcNowLocked() + cancelTime;
            TimeExpiryEntries.Add(new TimeExpiryEntry(expireUtc, wrapper, timeCts));
            OnSchedulingMutatedWhileGateHeld();
        }

        return wrapper;
    }
    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public TimeCancellationTokenSource LinkTimeCancellationToken (int cancelMilliseconds,
        params CancellationToken[] cancellationTokens)
    {
        return LinkTimeCancellationToken(TimeSpan.FromMilliseconds(cancelMilliseconds), cancellationTokens);
    }
    //----------------------------------------------------------------------------

    #endregion IPrimeTime Implementation — Time cancellation

    #endregion Interface Implementations
}
//################################################################################


