# Sleep, delay, and cancellation — PrimeTime (NodaTime) stack

The PrimeTime (NodaTime) stack exposes synchronous **`Sleep`** and asynchronous **`DelayAsync`** APIs that accept NodaTime **`Duration`** values. It also includes **`GetTimeCancellationToken`** to produce a **`TimeCancellationTokenSource`** that auto-cancels after a clock-driven elapsed time.

[!code-csharp[](../../../../Dev/Production/KZDev.PrimeTime.Examples/Scenarios/SleepDelayCancellationScenario.cs#Snippet)]

## What to notice

- **`primeClock.Sleep(Duration)`** blocks the calling thread for the specified duration. Under a `PrimeTestClock`, the call returns deterministically once virtual time is advanced.
- **`primeClock.DelayAsync(Duration, CancellationToken)`** is the awaitable counterpart and honors the supplied cancellation token.
- **`GetTimeCancellationToken(Duration)`** returns a **`TimeCancellationTokenSource`** that auto-cancels its `Token` after the specified clock-time elapsed period — useful for awaitable timeouts that follow virtual time in tests.
- **`TimeSpan`** overloads remain available from the shared contract for cross-stack code.

## Related

- **Concepts:** [Timers, daylight saving, and testing](../../concepts/concepts-timers-and-testing.md)
- **API:** [`IPrimeClock`](xref:KZDev.PrimeTime.IPrimeClock) · [`TimeCancellationTokenSource`](xref:KZDev.PrimeTime.TimeCancellationTokenSource)
