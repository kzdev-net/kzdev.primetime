#if SYSTEMCLOCK
namespace KZDev.SystemClock.PrimeTime.Testing;
#else
namespace KZDev.PrimeTime.Testing;
#endif

//################################################################################
/// <summary>
///   <see cref="PrimeTestClockTimedEvent"/> raised when virtual time advances to a new instant.
/// </summary>
public sealed partial class PrimeTestClockNewTimeEvent : PrimeTestClockTimedEvent
{
}
//################################################################################
