namespace KZDev.SystemClock.PrimeTime;

//################################################################################
/// <summary>
///   Production implementation of <see cref="IPrimeClock"/> that delegates "now"
///   to a <see cref="TimeProvider"/> time source (e.g. <see cref="TimeProvider.System"/>),
///   analogous to the NodaTime stack using an IClock abstraction.
/// </summary>
/// <remarks>
///   Delay and time-cancellation members (Sleep, DelayAsync, GetTimeCancellationToken,
///   LinkTimeCancellationToken) delegate to the BCL (Thread.Sleep, Task.Delay,
///   CancellationTokenSource with a timer), so they use system time rather than the provider's
///   time. For deterministic tests, use <see cref="PrimeTestClock"/> or <see cref="IPrimeTestClock"/>.
/// </remarks>
internal sealed partial class PrimeClock
{
    /// <summary>
    ///   The <see cref="TimeProvider"/> used for current UTC and local time (<see cref="GetUtcNow"/> and related APIs).
    /// </summary>
    private readonly TimeProvider _timeProvider;

    #region Constructors/Finalizers

    /// <summary>
    ///   Initializes a new instance of the <see cref="PrimeClock"/> class using
    ///   <see cref="TimeProvider.System"/> as the time source.
    /// </summary>
    public PrimeClock ()
        : this(TimeProvider.System)
    {
    }

    /// <summary>
    ///   Initializes a new instance of the <see cref="PrimeClock"/> class with the
    ///   specified time provider.
    /// </summary>
    /// <param name="timeProvider">
    ///   The time provider used to obtain the current UTC and local time.
    /// </param>
    /// <exception cref="ArgumentNullException">
    ///   <paramref name="timeProvider"/> is <c>null</c>.
    /// </exception>
    public PrimeClock (TimeProvider timeProvider)
    {
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    #endregion Constructors/Finalizers

    #region Interface Implementations

    #region IPrimeClock Implementation

    /// <inheritdoc />
    public DateTimeOffset LocalNowOffset => _timeProvider.GetLocalNow();

    /// <inheritdoc />
    public DateTimeOffset UtcNowOffset => _timeProvider.GetUtcNow();

    /// <inheritdoc />
    public DateTime LocalNowDateTime => _timeProvider.GetLocalNow().LocalDateTime;

    /// <inheritdoc />
    public DateTime UtcNowDateTime => _timeProvider.GetUtcNow().UtcDateTime;

#if NET
    /// <inheritdoc />
    public TimeOnly LocalNowTimeOnly => TimeOnly.FromDateTime(_timeProvider.GetLocalNow().DateTime);

    /// <inheritdoc />
    public TimeOnly UtcNowTimeOnly => TimeOnly.FromDateTime(_timeProvider.GetUtcNow().DateTime);

    /// <inheritdoc />
    public DateOnly LocalNowDateOnly => DateOnly.FromDateTime(_timeProvider.GetLocalNow().DateTime);

    /// <inheritdoc />
    public DateOnly UtcNowDateOnly => DateOnly.FromDateTime(_timeProvider.GetUtcNow().DateTime);
#endif

    #endregion IPrimeClock Implementation

    #endregion Interface Implementations
}
//################################################################################

