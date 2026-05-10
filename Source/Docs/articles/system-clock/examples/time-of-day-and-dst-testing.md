# Time-of-day and DST (testing) — System Clock stack

Deterministic DST tests under a virtual **`PrimeTestClock`**. The System Clock stack does not bundle a TZDB zone library, so the examples lean on environment probes for invalid/ambiguous calendar samples plus a stable virtual-clock advancement check.

[!code-csharp[](../../../../Dev/Testing/KZDev.SystemClock.PrimeTime.Testing.Examples/UsingPrimeTestClockDstExamples.cs#Snippet)]

## What to notice

- **`TestEnvironmentDescriptor.FromLocalMachine()`** wraps the host's `TimeZoneInfo.Local`. It returns `null` for the invalid/ambiguous samples on hosts without DST data, so each test **early-returns** instead of asserting against a missing sample.
- The third test demonstrates a **stable baseline**: advancing the virtual clock 400 days never throws, regardless of DST behavior of the host.
- For deterministic DST behavior independent of the host, prefer the **PrimeTime / NodaTime** stack, which can construct a `PrimeTestClock` against a fixed TZDB **`DateTimeZone`**.

## Related

- [Time-of-day and DST (production)](time-of-day-and-dst-production.md) — wall-clock scheduling and environment probes in the example app.
- [Test-clock control APIs](../../concepts/examples/test-clock-control-testing.md) — `Set*`, `Advance`, `RunFor`, `Start`/`Stop` reference for both stacks.
- **API:** [`IPrimeTestClock`](xref:KZDev.SystemClock.PrimeTime.Testing.IPrimeTestClock)
