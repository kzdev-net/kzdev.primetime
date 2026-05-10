# Timers, daylight saving, and testing

This article applies to **both** [KZDev.SystemClock.PrimeTime](../system-clock/systemclock-package.md) and [KZDev.PrimeTime](../primetime/primetime-superset.md). API shapes differ (BCL vs NodaTime types), but the **ideas** are shared.

## Core types

- **`IPrimeClock`** — Application-facing clock: current time projections, interval timers, and (on modern .NET targets) local **time-of-day** registrations.
- **`IPrimeTime`** — Narrower “time” surface; default DI registers it to the same instance as **`IPrimeClock`**.
- **`PrimeClock`** — Production implementation wired by **`AddPrimeClock`** unless you replace registrations.
- **`IPrimeTestClock` / `PrimeTestClock`** — Virtual time for deterministic unit tests (`Advance`, `RunFor`, and related members). See the API reference for constructors (they differ slightly between packages).

## Interval timers

Interval timers fire after an initial delay and optionally repeat. You register them on **`IPrimeClock`** (for example `RegisterTimer` / `RegisterAsyncTimer`). The superset adds **`NodaTime.Duration`** overloads; the SystemClock package uses **`TimeSpan`** (and matches those semantics on the shared contract).

Use **`IntervalTimerOptions`** where you need to tune behavior described in the API documentation.

Runnable examples:

- **System Clock track:** [Interval timer (production)](../system-clock/examples/interval-timer-production.md) · [Interval timer (testing)](../system-clock/examples/interval-timer-testing.md)
- **PrimeTime (NodaTime) track:** [Interval timer (production)](../primetime/examples/interval-timer-production.md) · [Interval timer (testing)](../primetime/examples/interval-timer-testing.md)

## Time-of-day timers and daylight saving

On **.NET 8+** (SDK `NET`), **`IPrimeClock`** includes **local** and **UTC** time-of-day registration APIs. **Local** wall-clock scheduling uses the clock’s view of the local zone and honors:

- **`SkippedTimeBehavior`** — What to do when the requested local time does not exist (spring-forward gap).
- **`DuplicateTimeBehavior`** — What to do when the same local time occurs twice (fall-back overlap).

Configure these on **`DayTimeTimerOptions`**. **UTC** time-of-day registrations do not use skipped/duplicate behaviors.

> **Note:** Some members are **omitted** from `netstandard2.0` builds. If you target older TFMs, confirm availability in the API reference for your target.

Runnable examples:

- **System Clock track:** [Time-of-day and DST (production)](../system-clock/examples/time-of-day-and-dst-production.md)
- **PrimeTime (NodaTime) track:** [Time-of-day and DST (production)](../primetime/examples/time-of-day-and-dst-production.md)

Each track's page covers the time-of-day timer registration, environment-aware skipped/ambiguous local-time probes, and the clock's local schedule zone.

## Bridging to `TimeProvider`

Both stacks can expose a **`TimeProvider`** that delegates to an **`IPrimeClock`** via **`PrimeClockTimeProviderExtensions.ToTimeProvider`**. That helps integrate with APIs that expect **`TimeProvider`** while keeping PrimeTime as the source of truth (especially under a **`PrimeTestClock`**).

## Testing tips

1. Replace **`IPrimeClock`** (or **`PrimeClock`**) with **`PrimeTestClock`** in tests.
2. Drive time with **`Advance`** / **`RunFor`** so timers fire predictably.
3. For **daylight saving** edge cases around local wall times, the **NodaTime** **`PrimeTestClock`** constructor that accepts a **`DateTimeZone`** is the most direct way to model zone rules in tests. The SystemClock package exposes **`LocalScheduleTimeZone`** on **`IPrimeClock`** for BCL-oriented local scheduling; align that with your test scenario.

Testing examples:

- **System Clock track:** [Time-of-day and DST (testing)](../system-clock/examples/time-of-day-and-dst-testing.md)
- **PrimeTime (NodaTime) track:** [Time-of-day and DST (testing)](../primetime/examples/time-of-day-and-dst-testing.md)
- **Cross-track:** [Test-clock control APIs](examples/test-clock-control-testing.md) · [DI replacement with `AddPrimeTestClock`](examples/di-replacement-testing.md)

## Further reading

- [Choosing a package](choosing-a-package.md)
- [Persistence and time conversions](persistence-and-conversions.md)
- **API:** [System Clock stack](xref:KZDev.SystemClock.PrimeTime) · [PrimeTime / NodaTime stack](xref:KZDev.PrimeTime)
