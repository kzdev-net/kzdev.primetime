using System.Diagnostics;

#if SYSTEMCLOCK
namespace KZDev.SystemClock.PrimeTime;
#else
namespace KZDev.PrimeTime;
#endif

/// <summary>
/// Extends <see cref="IPrimeTime"/> with test-controlled time support for clock implementations used in tests.
/// </summary>
public interface IPrimeTestTime : IPrimeTime
{
    /// <summary>
    /// Gets whether test time is currently advancing.
    /// When <c>false</c>, time is frozen and controlled through explicit test-time operations.
    /// </summary>
    bool IsRunning { [DebuggerStepThrough] get; }
}
