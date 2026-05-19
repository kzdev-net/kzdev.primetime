#if SYSTEMCLOCK
namespace KZDev.SystemClock.PrimeTime.Testing;
#else
namespace KZDev.PrimeTime.Testing;
#endif

//################################################################################
/// <summary>
///   <see cref="PrimeTestClockTimedEvent"/> raised after <see cref="IPrimeTestClock.Stop"/> stops a running clock.
/// </summary>
public sealed partial class PrimeTestClockStoppedEvent : PrimeTestClockTimedEvent
{
}
//################################################################################
