# System Clock examples

Self-contained snippets for the **`KZDev.SystemClock.PrimeTime`** stack (BCL types, **`TimeProvider`**, `TimeSpan`, `DateTimeOffset`). Each page shows code copied directly from the in-repo example projects:

- Production demos live in `Source/Dev/Production/KZDev.SystemClock.PrimeTime.Examples/`.
- Test demos live in `Source/Dev/Testing/KZDev.SystemClock.PrimeTime.Testing.Examples/` and use [xUnit](https://xunit.net) with [AwesomeAssertions](https://www.nuget.org/packages/AwesomeAssertions).

## Production

- [Interval timers](interval-timer-production.md) — register sync and async interval callbacks on **`IPrimeClock`**.
- [Time-of-day and DST](time-of-day-and-dst-production.md) — register local wall-clock callbacks, plus environment-aware skipped/ambiguous handling and the clock's `LocalScheduleTimeZone`.
- [DI registration](di-registration-production.md) — what **`AddPrimeClock`** registers and how to resolve the abstractions.
- ["Now" surfaces](now-surfaces-production.md) — the `LocalNow*` and `UtcNow*` projections this stack exposes.
- [Persistence and conversions (production)](persistence-and-conversions-production.md) — UTC **`DateTimeOffset`** → schedule zone; **`DateOnly`** / **`TimeOnly`** extraction and recombination.
- [Sleep, delay, and cancellation](sleep-delay-cancellation-production.md) — `Sleep`, `DelayAsync`, and time-based cancellation tokens.

## Testing (virtual time)

- [Interval timers (testing)](interval-timer-testing.md) — drive interval timers under **`PrimeTestClock`** with `Advance`.
- [Time-of-day and DST (testing)](time-of-day-and-dst-testing.md) — environment-aware DST probes and stable virtual-clock advancement.
- [Persistence and conversions (testing)](persistence-and-conversions-testing.md) — schedule-zone **`DateTimeOffset`** projection and **`DateOnly`** / **`TimeOnly`** round-trips.

## Cross-track testing examples

The following pages cover patterns shared by both stacks; both are presented side-by-side:

- [DI replacement with `AddPrimeTestClock`](../../concepts/examples/di-replacement-testing.md)
- [Test-clock control APIs](../../concepts/examples/test-clock-control-testing.md)

## Related

- **Concepts:** [Persistence and time conversions](../../concepts/persistence-and-conversions.md) · [Timers, daylight saving, and testing](../../concepts/concepts-timers-and-testing.md)
- **Usage guide:** [KZDev.SystemClock.PrimeTime](../systemclock-package.md)
- **API:** [Production](xref:KZDev.SystemClock.PrimeTime) · [Testing](xref:KZDev.SystemClock.PrimeTime.Testing)
