# Time-of-day and DST (testing) — PrimeTime (NodaTime) stack

Deterministic DST tests under a virtual **`PrimeTestClock`** anchored to a fixed NodaTime **`DateTimeZone`** (US Eastern in the example fixture). Because the zone is part of the clock construction, the tests do not depend on the host's `TimeZoneInfo.Local` and produce the same results everywhere.

[!code-csharp[](../../../../Dev/Testing/KZDev.PrimeTime.Testing.Examples/UsingPrimeTestClockDstExamples.cs#Snippet)]

## What to notice

- **`NodaDstScenarioFixture.CreateClock`** constructs a `PrimeTestClock` with a fixed TZDB zone (`UsEastern`), so spring-forward and fall-back transitions are reproducible.
- Crossing a spring-forward boundary changes **`LocalZonedNowInstant.Offset`** because the clock's local view jumps from EST (-05:00) to EDT (-04:00).
- For ambiguous fall-back wall-clock times, **`SetLocalTime`** uses **lenient** mapping: the wall clock is resolved deterministically to the earlier of the two valid instants.
- The third test demonstrates a host-environment **probe**: when `TimeZoneInfo.Local` exposes a known invalid sample, the test asserts the BCL marks it invalid; otherwise it short-circuits.

## Related

- [Time-of-day and DST (production)](time-of-day-and-dst-production.md) — wall-clock scheduling and environment probes in the example app.
- [Test-clock control APIs](../../concepts/examples/test-clock-control-testing.md) — `Set*`, `Advance`, `RunFor`, `Start`/`Stop` reference for both stacks.
- **API:** [`IPrimeTestClock`](xref:KZDev.PrimeTime.Testing.IPrimeTestClock) · [`PrimeTestClock`](xref:KZDev.PrimeTime.Testing.PrimeTestClock)
