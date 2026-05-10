# Sleep, delay, and cancellation — System Clock stack

The System Clock stack exposes synchronous **`Sleep`** and asynchronous **`DelayAsync`** APIs that route through the registered **`TimeProvider`** (so tests can substitute a virtual one). It also includes **`GetTimeCancellationToken`** to produce a **`TimeCancellationTokenSource`** that auto-cancels after a clock-driven elapsed time.

[!code-csharp[](../../../../Dev/Production/KZDev.SystemClock.PrimeTime.Examples/Scenarios/SleepDelayCancellationScenario.cs#Snippet)]

## What to notice

- **`primeClock.Sleep(TimeSpan)`** blocks the calling thread for the specified `TimeSpan`. Under a `PrimeTestClock`, the call returns deterministically once virtual time is advanced.
- **`primeClock.DelayAsync(TimeSpan, CancellationToken)`** is the awaitable counterpart and honors the supplied cancellation token.
- **`GetTimeCancellationToken(TimeSpan)`** returns a **`TimeCancellationTokenSource`** that auto-cancels its `Token` after the specified clock-time elapsed period — useful for awaitable timeouts that follow virtual time in tests.

## Related

- **Concepts:** [Timers, daylight saving, and testing](../../concepts/concepts-timers-and-testing.md)
- **API:** [`IPrimeClock`](xref:KZDev.SystemClock.PrimeTime.IPrimeClock) · [`TimeCancellationTokenSource`](xref:KZDev.SystemClock.PrimeTime.TimeCancellationTokenSource)
