# Test-clock control APIs (testing)

**`PrimeTestClock`** exposes the same control verbs in both stacks — they differ only in the time types each one uses (BCL `DateTimeOffset` / `TimeSpan` vs NodaTime `Instant` / `Duration`).

| Verb | Purpose |
|------|---------|
| **`SetTime`** / **`SetInstant`** | Set virtual UTC time; **forward** targets march through due delays, expiries, and timers; **backward** targets follow the rules below. |
| **`SetLocalTime`** | Set virtual time from a local wall-clock value (lenient DST mapping); same forward/backward semantics as **`SetTime`** after resolving to UTC. |
| **`Advance`** | Step virtual time forward by marching to each due instant; callbacks see that instant in **`UtcNow`** / **`NowInstant`**. |
| **`RunFor`** | Same effect as a single **`Advance`** step of the same length. |
| **`Start`** / **`Stop`** | Toggle the deadline-driven automatic runner; **`IsRunning`** reflects the state. |

## System Clock stack

[!code-csharp[](../../../../Dev/Testing/KZDev.SystemClock.PrimeTime.Testing.Examples/UsingPrimeTestClockControlExamples.cs#Snippet)]

## PrimeTime (NodaTime) stack

[!code-csharp[](../../../../Dev/Testing/KZDev.PrimeTime.Testing.Examples/UsingPrimeTestClockControlExamples.cs#Snippet)]

## Forward marching (`Advance`, forward `SetTime` / `SetInstant` / `SetLocalTime`)

- Virtual time moves to the **earliest** due instant in the open interval toward the target, dispatches all work due at that instant (delays complete, time expiries cancel, interval and day-time timers fire), then repeats until the horizon is reached.
- Timer callbacks observe **`UtcNowDateTimeOffset`** / **`NowInstant`** equal to **that firing instant**, not only the final horizon.
- **`ClockEvents`** fires **once per distinct** virtual instant visited during the march.
- Negative **`Advance`** duration is treated as zero (no backward move).

## Automatic runner (`Start` / `Stop`)

- **`Start(rate)`** runs a background loop that waits for the **next virtual deadline** among pending delays, time expiries, timers, and a **virtual one-minute `ClockEvents` heartbeat** when no other work is due. Virtual delay is mapped to real time using **`rate`** (virtual time per real second).
- **Run rate** must be between **100 ms** and **1 hour** of virtual time per real second (inclusive), or **`ArgumentOutOfRangeException`** is thrown. **`null`** means 1:1.
- The runner does **not** poll every real second. Intended real sleeps **shorter than 15 ms** are skipped in favor of in-process virtual bursts until the next sleep would be at least 15 ms.
- After a real wait, virtual catch-up uses **measured** anchor elapsed time × rate (oversleep produces extra virtual progress), not only the intended timeout.
- Registering new delays, timers, or time expiries, cancelling work, **`Stop`**, or a **persist-on-read** from a "now" getter can **wake** the runner so sooner deadlines are not missed.
- **`Stop()`** stops the runner, clears the anchor, and returns **`true`** if the clock was running. "Now" getters then return the **persisted** instant only (no projection).

## "Now" while running (projection and persist-on-read)

- While **`Start()`** is active, **`UtcNowDateTimeOffset`**, **`LocalNow*`**, and Noda **`NowInstant`** / related members **project** linearly from the last committed virtual instant and a shared **`Stopwatch`** anchor at the run rate.
- Reading "now" also **persists**: due work up to the projected instant is marched, the committed instant is stored, and the anchor restarts (the runner is signaled to recompute). **Frequent reads can be expensive** — prefer explicit **`Advance`** / **`RunFor`** when you do not need projection.
- See also [Timers, daylight saving, and testing](../concepts-timers-and-testing.md#test-clock-runner-and-marching).

## Backward moves

| State | Backward `SetTime` / `SetInstant` / `SetLocalTime` |
|--------|-----------------------------------------------------|
| **Running** | **`InvalidOperationException`** |
| **Stopped**, any **active interval** timer | **`InvalidOperationException`** |
| **Stopped**, no active interval timers | Instant jump, **one** **`ClockEvents`**, no replay of skipped delay/timer work; day-time timers recompute **`NextDueUtc`** (at most one callback per discrete time-of-day occurrence) |

## What to notice (both stacks)

- Use **`Advance`** or forward **`SetTime`** when you need intermediate timer and delay callbacks on the path to a target instant.
- **`SetLocalTime`** maps a local wall time in the clock's zone; spring-forward gaps and fall-back overlaps use lenient mapping consistent with production day-time scheduling.
- **`RunFor`** is a synonym for **`Advance`** on the same time argument.

## Related

- [DI replacement with `AddPrimeTestClock`](di-replacement-testing.md) — registering a `PrimeTestClock` so production code receives virtual time through DI.
- [Timers, daylight saving, and testing](../concepts-timers-and-testing.md) — interval/day-time timers and runner overview.
- **System Clock testing examples:** [Interval (testing)](../../system-clock/examples/interval-timer-testing.md) · [Time-of-day & DST (testing)](../../system-clock/examples/time-of-day-and-dst-testing.md)
- **PrimeTime testing examples:** [Interval (testing)](../../primetime/examples/interval-timer-testing.md) · [Time-of-day & DST (testing)](../../primetime/examples/time-of-day-and-dst-testing.md)
- **API:** [`KZDev.SystemClock.PrimeTime.Testing.IPrimeTestClock`](xref:KZDev.SystemClock.PrimeTime.Testing.IPrimeTestClock) · [`KZDev.PrimeTime.Testing.IPrimeTestClock`](xref:KZDev.PrimeTime.Testing.IPrimeTestClock)
