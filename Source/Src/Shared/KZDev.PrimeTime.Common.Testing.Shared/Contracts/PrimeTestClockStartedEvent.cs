#if SYSTEMCLOCK
namespace KZDev.SystemClock.PrimeTime.Testing;
#else
namespace KZDev.PrimeTime.Testing;
#endif

//################################################################################
/// <summary>
///   <see cref="PrimeTestClockTimedEvent"/> raised when the automatic runner starts from a stopped state.
/// </summary>
public sealed partial class PrimeTestClockStartedEvent : PrimeTestClockTimedEvent
{
}
//################################################################################
