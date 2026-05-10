# Test-clock control APIs (testing)

**`PrimeTestClock`** exposes the same control verbs in both stacks — they differ only in the time types each one uses (BCL `DateTimeOffset` / `TimeSpan` vs NodaTime `Instant` / `Duration`).

| Verb | Purpose |
|------|---------|
| **`SetTime`** / **`SetInstant`** | Move the virtual UTC timeline to a specific value. |
| **`SetLocalTime`** | Move the timeline by anchoring a local wall-clock value (lenient mapping for DST gaps and overlaps). |
| **`Advance`** | Step virtual time forward by a duration; pending callbacks are dispatched synchronously before the call returns. |
| **`RunFor`** | Same effect as a single `Advance` step of the same length; useful for reading-style test code. |
| **`Start`** / **`Stop`** | Toggle continuous automatic advancement; `IsRunning` reflects the current state. |

## System Clock stack

[!code-csharp[](../../../../Dev/Testing/KZDev.SystemClock.PrimeTime.Testing.Examples/UsingPrimeTestClockControlExamples.cs#Snippet)]

## PrimeTime (NodaTime) stack

[!code-csharp[](../../../../Dev/Testing/KZDev.PrimeTime.Testing.Examples/UsingPrimeTestClockControlExamples.cs#Snippet)]

## What to notice (both stacks)

- **`SetTime`** / **`SetInstant`** does **not** trigger callbacks — it only repositions the timeline. Use **`Advance`** when you want pending timer callbacks to dispatch.
- **`SetLocalTime`** maps a **`LocalDateTime`** (NodaTime) or BCL `DateTime` against the clock's configured local zone. Spring-forward gaps resolve to the next valid wall-clock instant (lenient mapping).
- **`Start(interval)`** runs the clock continuously, advancing by the supplied step on its internal cadence. Pair with **`Stop()`** to return to manual control. **`Stop()`** returns `true` when it actually stopped a running clock and `false` otherwise.
- **`RunFor`** is a synonym for **`Advance`** on the same time argument — they produce identical timeline state.

## Related

- [DI replacement with `AddPrimeTestClock`](di-replacement-testing.md) — registering a `PrimeTestClock` so production code receives virtual time through DI.
- **System Clock testing examples:** [Interval (testing)](../../system-clock/examples/interval-timer-testing.md) · [Time-of-day & DST (testing)](../../system-clock/examples/time-of-day-and-dst-testing.md)
- **PrimeTime testing examples:** [Interval (testing)](../../primetime/examples/interval-timer-testing.md) · [Time-of-day & DST (testing)](../../primetime/examples/time-of-day-and-dst-testing.md)
- **API:** [`KZDev.SystemClock.PrimeTime.Testing.IPrimeTestClock`](xref:KZDev.SystemClock.PrimeTime.Testing.IPrimeTestClock) · [`KZDev.PrimeTime.Testing.IPrimeTestClock`](xref:KZDev.PrimeTime.Testing.IPrimeTestClock)
