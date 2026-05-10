# PrimeTime (NodaTime) examples

Self-contained snippets for the **`KZDev.PrimeTime`** stack, the NodaTime superset that ships **`Instant`**, **`Duration`**, **`LocalTime`**, **`LocalDate`**, and **`ZonedDateTime`** APIs on top of the BCL surfaces. Each page shows code copied directly from the in-repo example projects:

- Production demos live in `Source/Dev/Production/KZDev.PrimeTime.Examples/`.
- Test demos live in `Source/Dev/Testing/KZDev.PrimeTime.Testing.Examples/` and use [xUnit](https://xunit.net) with [AwesomeAssertions](https://www.nuget.org/packages/AwesomeAssertions).

## Production

- [Interval timers](interval-timer-production.md) — register sync and async interval callbacks on **`IPrimeClock`** using NodaTime **`Duration`**.
- [Time-of-day and DST](time-of-day-and-dst-production.md) — register local **`LocalTime`** callbacks, plus environment-aware skipped/ambiguous handling and the clock's `LocalScheduleTimeZone`.
- [DI registration](di-registration-production.md) — what **`AddPrimeClock`** registers and how to resolve the abstractions, including the NodaTime **`IClock`**.
- ["Now" surfaces](now-surfaces-production.md) — the NodaTime-first projections (`NowInstant`, `LocalZonedNowInstant`, `LocalNowTime`, `LocalNowDate`, etc.).
- [Persistence and conversions (production)](persistence-and-conversions-production.md) — project persisted **`Instant`** values into the schedule zone; **`LocalTimeOfDay`** round-trip; delay **`TimeSpan`** clamping.
- [Sleep, delay, and cancellation](sleep-delay-cancellation-production.md) — `Sleep`, `DelayAsync`, and time-based cancellation tokens, all expressed with **`Duration`**.

## Testing (virtual time)

- [Interval timers (testing)](interval-timer-testing.md) — drive interval timers under **`PrimeTestClock`** with `Advance`.
- [Time-of-day and DST (testing)](time-of-day-and-dst-testing.md) — deterministic spring-forward and fall-back examples against a fixed **`DateTimeZone`**.
- [Persistence and conversions (testing)](persistence-and-conversions-testing.md) — schedule-zone projections and duration clamping under **`PrimeTestClock`**.

## Cross-track testing examples

The following pages cover patterns shared by both stacks; both are presented side-by-side:

- [DI replacement with `AddPrimeTestClock`](../../concepts/examples/di-replacement-testing.md)
- [Test-clock control APIs](../../concepts/examples/test-clock-control-testing.md)

## Related

- **Concepts:** [Persistence and time conversions](../../concepts/persistence-and-conversions.md) · [Timers, daylight saving, and testing](../../concepts/concepts-timers-and-testing.md)
- **Usage guide:** [KZDev.PrimeTime](../primetime-superset.md)
- **API:** [Production](xref:KZDev.PrimeTime) · [Testing](xref:KZDev.PrimeTime.Testing)
