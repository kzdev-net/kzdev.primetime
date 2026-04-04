using System.Diagnostics;

#if SYSTEMCLOCK
namespace KZDev.SystemClock.PrimeTime;
#else
namespace KZDev.PrimeTime;
#endif

/// <summary>
///   Extends <see cref="IPrimeTime"/> with test-controlled time support for clock implementations used in tests.
/// </summary>
public interface IPrimeTestTime : IPrimeTime
{
    /// <summary>
    ///   Gets a value indicating whether test time is currently advancing automatically.
    /// </summary>
    /// <remarks>
    ///   When <c>false</c>, virtual time is frozen except for explicit test-time operations on the clock.
    /// </remarks>
    bool IsRunning { [DebuggerStepThrough] get; }
}
