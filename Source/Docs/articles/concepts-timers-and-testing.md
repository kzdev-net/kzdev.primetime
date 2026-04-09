# Timers, daylight saving, and testing

This article applies to **both** [KZDev.SystemClock.PrimeTime](systemclock-package.md) and [KZDev.PrimeTime](primetime-superset.md). API shapes differ (BCL vs NodaTime types), but the **ideas** are shared.

## Core types

- **`IPrimeClock`** — Application-facing clock: current time projections, interval timers, and (on modern .NET targets) local **time-of-day** registrations.
- **`IPrimeTime`** — Narrower “time” surface; default DI registers it to the same instance as **`IPrimeClock`**.
- **`PrimeClock`** — Production implementation wired by **`AddPrimeClock`** unless you replace registrations.
- **`IPrimeTestClock` / `PrimeTestClock`** — Virtual time for deterministic unit tests (`Advance`, `RunFor`, and related members). See the API reference for constructors (they differ slightly between packages).

## Interval timers

Interval timers fire after an initial delay and optionally repeat. You register them on **`IPrimeClock`** (for example `RegisterTimer` / `RegisterAsyncTimer`). The superset adds **`NodaTime.Duration`** overloads; the SystemClock package uses **`TimeSpan`** (and matches those semantics on the shared contract).

Use **`IntervalTimerOptions`** where you need to tune behavior described in the API documentation.

## Time-of-day timers and daylight saving

On **.NET 6+** (SDK `NET`), **`IPrimeClock`** includes **local** and **UTC** time-of-day registration APIs. **Local** wall-clock scheduling uses the clock’s view of the local zone and honors:

- **`SkippedTimeBehavior`** — What to do when the requested local time does not exist (spring-forward gap).
- **`DuplicateTimeBehavior`** — What to do when the same local time occurs twice (fall-back overlap).

Configure these on **`DayTimeTimerOptions`**. **UTC** time-of-day registrations do not use skipped/duplicate behaviors.

> **Note:** Some members are **omitted** from `netstandard2.0` builds. If you target older TFMs, confirm availability in the API reference for your target.

## Bridging to `TimeProvider`

Both stacks can expose a **`TimeProvider`** that delegates to an **`IPrimeClock`** via **`PrimeClockTimeProviderExtensions.ToTimeProvider`**. That helps integrate with APIs that expect **`TimeProvider`** while keeping PrimeTime as the source of truth (especially under a **`PrimeTestClock`**).

## Testing tips

1. Replace **`IPrimeClock`** (or **`PrimeClock`**) with **`PrimeTestClock`** in tests.
2. Drive time with **`Advance`** / **`RunFor`** so timers fire predictably.
3. For **daylight saving** edge cases around local wall times, the **NodaTime** **`PrimeTestClock`** constructor that accepts a **`DateTimeZone`** is the most direct way to model zone rules in tests. The SystemClock package exposes **`LocalScheduleTimeZone`** on **`IPrimeClock`** for BCL-oriented local scheduling; align that with your test scenario.

## Further reading

- [Choosing a package](choosing-a-package.md)
- [API Reference](xref:PrimeTime)
- [Cron and scheduling notes](../Reference/CronNotes.md) (ecosystem context; PrimeTime is not a cron scheduler)
