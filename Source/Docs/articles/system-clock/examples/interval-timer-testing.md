# Interval timer (testing) — System Clock stack

These xUnit examples drive interval timers under a virtual **`PrimeTestClock`**. Callbacks fire **only** when the test advances the clock, so the assertions are deterministic and there are no real-world wait times.

[!code-csharp[](../../../../Dev/Testing/KZDev.SystemClock.PrimeTime.Testing.Examples/UsingPrimeTestClockIntervalTimerExamples.cs#Snippet)]

## What to notice

- **`PrimeTestClock`** is constructed from a `DateTimeOffset` so the BCL stack starts at a known UTC instant.
- **`Advance(TimeSpan)`** moves virtual time forward; callbacks scheduled at or before the new "now" are dispatched synchronously by the test clock before `Advance` returns.
- A one-shot timer is requested by passing **`Timeout.InfiniteTimeSpan`** as the repeat interval. **`IClockIntervalTimer.IsRepeating`** is `false` for one-shot registrations.
- For repeating timers, advancing past each multiple of the repeat interval produces one callback per interval crossed.
- The async overload (**`RegisterAsyncTimer`**) completes the awaited callback before `Advance` returns.

## Related

- [Interval timer (production)](interval-timer-production.md) — wall-clock counterpart in the example app.
- [Test-clock control APIs](../../concepts/examples/test-clock-control-testing.md) — `Set*`, `Advance`, `RunFor`, `Start`/`Stop`, marching, and automatic runner semantics for both stacks.
- **API:** [`IPrimeTestClock`](xref:KZDev.SystemClock.PrimeTime.Testing.IPrimeTestClock) · [`PrimeTestClock`](xref:KZDev.SystemClock.PrimeTime.Testing.PrimeTestClock)
