# KZDev.PrimeTime — usage guide (NodaTime superset)

**Package ID:** `KZDev.PrimeTime`  
**Namespace:** `KZDev.PrimeTime`

This guide describes the **full** PrimeTime package: everything in the SystemClock subset **plus** **NodaTime**-centric members on **`IPrimeClock`** and NodaTime timer overloads. **NodaTime is a required dependency** of this package.

## When to use this package

- You already use **NodaTime** for instants, zones, and calendars.
- You want **`Instant`**, **`ZonedDateTime`**, **`LocalTime`**, and **`Duration`** on the same clock abstraction as your BCL projections.
- You want **`Duration`-based** interval registration and **`LocalTime`-based** time-of-day registration on **`IPrimeClock`** (see API reference).

This package includes all the BCL/`TimeProvider` functionality from SystemClock plus NodaTime extensions. If you prefer minimal dependencies and don't need NodaTime types, use **[KZDev.SystemClock.PrimeTime](../system-clock/systemclock-package.md)**.

## Install

```bash
dotnet add package KZDev.PrimeTime
```

This pulls in **`NodaTime`** transitively. Target frameworks shipped by this repository: **`net10.0`**, **`net8.0`**, **`netstandard2.0`**.

## Dependency injection

```csharp
using KZDev.PrimeTime;
using Microsoft.Extensions.DependencyInjection;

IServiceCollection services = new ServiceCollection();
services.AddPrimeClock();
```

**`AddPrimeClock`** registers:

| Service | Default implementation | Notes |
|---------|------------------------|--------|
| `NodaTime.IClock` | `SystemClock.Instance` | Skipped if you already registered `IClock`. |
| `IPrimeClock` | `PrimeClock` singleton | Uses the registered NodaTime `IClock`. |
| `IPrimeTime` | same instance as `IPrimeClock` | Resolved via factory. |

## NodaTime-first “now”

`IPrimeClock` in this assembly extends the shared contract with properties such as:

- **`NowInstant`**, **`UtcNowInstant`**, **`LocalZonedNowInstant`**
- **`LocalNowTime`**, **`UtcNowTime`**, **`LocalNowDate`**, **`UtcNowDate`**

Use these when the rest of your domain speaks NodaTime. BCL projections remain available from the shared contract where applicable for your target framework.

## Timers

- **Interval:** use **`TimeSpan`** overloads **or** **`Duration`** overloads (superset-only).
- **Time of day:** use **`LocalTime`** / **`LocalDate`** overloads on supported targets, with **`DayTimeTimerOptions`** for DST. See [Timers, daylight saving, and testing](../concepts/concepts-timers-and-testing.md).

## Virtual time tests

**`PrimeTestClock`** supports NodaTime-centric construction (for example an initial **`Instant`** and **`DateTimeZone`**). That combination is especially useful for **deterministic DST** tests. See the [testing assembly reference](xref:KZDev.PrimeTime.Testing) for exact constructors and **`IPrimeTestClock`** members.

## Runnable examples

Self-contained snippets for this stack live under [PrimeTime (NodaTime) examples](examples/index.md). Each page reproduces the relevant code in the documentation site so you do not need to navigate the repository to read it. Highlights:

- [DI registration](examples/di-registration-production.md) — what `AddPrimeClock` registers (including the NodaTime `IClock`) and how to resolve it.
- ["Now" surfaces](examples/now-surfaces-production.md) — NodaTime-first projections (`NowInstant`, `LocalZonedNowInstant`, `LocalNowTime`, `LocalNowDate`).
- [Sleep, delay, and cancellation](examples/sleep-delay-cancellation-production.md) — `Sleep`, `DelayAsync`, and time-based cancellation tokens (Duration-typed).
- [Interval timers](examples/interval-timer-production.md) and [time-of-day & DST](examples/time-of-day-and-dst-production.md) — production wall-clock scheduling.
- Testing counterparts: [Interval (testing)](examples/interval-timer-testing.md), [Time-of-day & DST (testing)](examples/time-of-day-and-dst-testing.md), and the cross-track [DI replacement](../concepts/examples/di-replacement-testing.md) and [test-clock control](../concepts/examples/test-clock-control-testing.md) pages.

## Related documentation

- [Choosing a package](../concepts/choosing-a-package.md)
- [KZDev.SystemClock.PrimeTime](../system-clock/systemclock-package.md)
- [Shared concepts](../concepts/concepts-timers-and-testing.md)
- **Production API:** [KZDev.PrimeTime](xref:KZDev.PrimeTime) · **Testing API:** [KZDev.PrimeTime.Testing](xref:KZDev.PrimeTime.Testing)
