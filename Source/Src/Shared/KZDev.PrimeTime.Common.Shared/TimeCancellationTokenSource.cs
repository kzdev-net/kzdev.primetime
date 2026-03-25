using System.Threading;

namespace KZDev.PrimeTime;

//################################################################################
/// <summary>
///   Wraps one or more <see cref="CancellationTokenSource"/> instances used for time-based
///   cancellation, exposing token and cancel operations and ensuring all underlying
///   sources are disposed when this instance is disposed.
/// </summary>
/// <remarks>
///   Callers dispose this wrapper instead of a raw <see cref="CancellationTokenSource"/>.
///   Disposing this instance disposes the primary source and any related intermediate
///   sources (e.g. the internal time-based source used when linking tokens), avoiding
///   resource leaks that occur when only a linked BCL source is disposed.
/// </remarks>
public sealed class TimeCancellationTokenSource : IDisposable
{
    // Token exposed via Token; disposed with this wrapper.
    private readonly CancellationTokenSource _primary;
    // Extra CTS instances created for linking (e.g. time-based source) that must be disposed with the wrapper.
    private readonly CancellationTokenSource[]? _additionalToDispose;
    // Non-zero after Dispose completes; read with Volatile for thread-safe disposal checks.
    private int _disposed;

    #region Constructors/Finalizers

    /// <summary>
    ///   Initializes a new instance of the <see cref="TimeCancellationTokenSource"/> class.
    /// </summary>
    /// <param name="primary">
    ///   The primary cancellation token source; its <see cref="Token"/> is exposed to callers.
    /// </param>
    /// <param name="additionalToDispose">
    ///   Optional additional sources to dispose when this instance is disposed (e.g. internal
    ///   time-based sources). May be null or empty.
    /// </param>
    /// <exception cref="ArgumentNullException">
    ///   <paramref name="primary"/> is null.
    /// </exception>
    internal TimeCancellationTokenSource (CancellationTokenSource primary,
        CancellationTokenSource[]? additionalToDispose = null)
    {
        _primary = primary ?? throw new ArgumentNullException(nameof(primary));
        _additionalToDispose = additionalToDispose;
    }

    #endregion Constructors/Finalizers

    /// <summary>
    ///   Gets the cancellation token associated with the primary source.
    /// </summary>
    /// <exception cref="ObjectDisposedException">
    ///   This instance has been disposed.
    /// </exception>
    public CancellationToken Token
    {
        get
        {
            if (Volatile.Read(ref _disposed) != 0)
                throw new ObjectDisposedException(nameof(TimeCancellationTokenSource));
            return _primary.Token;
        }
    }

    /// <summary>
    ///   Gets whether cancellation has been requested for this token.
    /// </summary>
    /// <exception cref="ObjectDisposedException">
    ///   This instance has been disposed.
    /// </exception>
    public bool IsCancellationRequested
    {
        get
        {
            if (Volatile.Read(ref _disposed) != 0)
                throw new ObjectDisposedException(nameof(TimeCancellationTokenSource));
            return _primary.IsCancellationRequested;
        }
    }

    /// <summary>
    ///   Communicates a request for cancellation.
    /// </summary>
    /// <exception cref="ObjectDisposedException">
    ///   This instance has been disposed.
    /// </exception>
    public void Cancel ()
    {
        if (Volatile.Read(ref _disposed) != 0)
            throw new ObjectDisposedException(nameof(TimeCancellationTokenSource));
        _primary.Cancel();
    }

    /// <summary>
    ///   Communicates a request for cancellation and specifies whether an exception
    ///   should be thrown if cancellation is successful.
    /// </summary>
    /// <param name="throwOnFirstException">
    ///   True if an exception should be thrown if cancellation is successful; otherwise false.
    /// </param>
    /// <remarks>
    ///   Delegates to <see cref="CancellationTokenSource.Cancel(bool)"/>; when
    ///   <paramref name="throwOnFirstException"/> is <c>true</c>, the first exception from a
    ///   registered cancellation callback is propagated; otherwise an
    ///   <see cref="AggregateException"/> may be thrown when multiple callbacks fail.
    /// </remarks>
    /// <exception cref="AggregateException">
    ///   <paramref name="throwOnFirstException"/> is <c>false</c> and one or more registered
    ///   cancellation callbacks throw.
    /// </exception>
    /// <exception cref="ObjectDisposedException">
    ///   This instance has been disposed.
    /// </exception>
    public void Cancel (bool throwOnFirstException)
    {
        if (Volatile.Read(ref _disposed) != 0)
            throw new ObjectDisposedException(nameof(TimeCancellationTokenSource));
        _primary.Cancel(throwOnFirstException);
    }

    #region Interface Implementations

    /// <summary>
    ///   Disposes the primary <see cref="CancellationTokenSource"/> and any additional sources
    ///   supplied at construction.
    /// </summary>
    public void Dispose ()
    {
        if (Interlocked.CompareExchange(ref _disposed, 1, 0) != 0)
            return;
        _primary.Dispose();
        if (_additionalToDispose == null)
        {
            return;
        }

        foreach (CancellationTokenSource cts in _additionalToDispose)
            cts.Dispose();
    }

    #endregion Interface Implementations
}
//################################################################################
