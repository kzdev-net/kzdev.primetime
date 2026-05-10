# KZDev.SystemClock.PrimeTime — usage guide

**Package ID:** `KZDev.SystemClock.PrimeTime`  
**Namespace:** `KZDev.SystemClock.PrimeTime`

This guide describes the **BCL / `TimeProvider`** deliverable: same **names** (`IPrimeClock`, `PrimeClock`, `PrimeTestClock`, …) as the superset where they overlap, **without** NodaTime.

## When to use this package

- You standardize on **`DateTimeOffset`**, **`TimeSpan`**, and **`TimeProvider`**.
- You **must not** add a NodaTime dependency.
- You want **`IPrimeClock.LocalScheduleTimeZone`** for local calendar time-of-day behavior (see API docs).

If you need **`Instant`**, **`Duration`**, or **`LocalTime`** members on **`IPrimeClock`**, use **[KZDev.PrimeTime](../primetime/primetime-superset.md)** instead.

## Install

```bash
dotnet add package KZDev.SystemClock.PrimeTime
```

Target frameworks shipped by this repository: **`net10.0`**, **`net8.0`**, **`netstandard2.0`**.

## Dependency injection

```csharp
using KZDev.SystemClock.PrimeTime;
using Microsoft.Extensions.DependencyInjection;

IServiceCollection services = new ServiceCollection();
services.AddPrimeClock();
```

**`AddPrimeClock`** registers:

| Service | Default implementation | Notes |
|---------|------------------------|--------|
| `TimeProvider` | `TimeProvider.System` | Skipped if you already registered `TimeProvider`. |
| `IPrimeClock` | `PrimeClock` singleton | Uses the registered `TimeProvider`. |
| `IPrimeTime` | same instance as `IPrimeClock` | Resolved via factory. |

## Using the clock

Prefer **`IPrimeClock`** in application code. Typical members include UTC and local projections using BCL types (for example **`UtcNowDateTimeOffset`**), plus timer registration methods. Exact members depend on target framework; see the [API reference for this stack](xref:KZDev.SystemClock.PrimeTime) (**`api/system-clock/`**).

## Time-of-day timers

On supported targets, use **`RegisterTimeOfDay`** / **`RegisterAsyncTimeOfDay`** with **`LocalTimeOfDay`** / **`UtcTimeOfDay`** wrappers and **`DayTimeTimerOptions`** for DST policy. See [Timers, daylight saving, and testing](../concepts/concepts-timers-and-testing.md).

## Virtual time tests

Use **`PrimeTestClock`** and **`IPrimeTestClock`**. Constructors are **BCL-first** (for example starting from a **`DateTimeOffset`**). Advance virtual time to exercise timers without wall-clock delays.

## Runnable examples

Self-contained snippets for this stack live under [System Clock examples](examples/index.md). Each page reproduces the relevant code in the documentation site so you do not need to navigate the repository to read it. Highlights:

- [DI registration](examples/di-registration-production.md) — what `AddPrimeClock` registers and how to resolve it.
- ["Now" surfaces](examples/now-surfaces-production.md) — `LocalNow*` and `UtcNow*` BCL projections.
- [Sleep, delay, and cancellation](examples/sleep-delay-cancellation-production.md) — `Sleep`, `DelayAsync`, and time-based cancellation tokens.
- [Interval timers](examples/interval-timer-production.md) and [time-of-day & DST](examples/time-of-day-and-dst-production.md) — production wall-clock scheduling.
- Testing counterparts: [Interval (testing)](examples/interval-timer-testing.md), [Time-of-day & DST (testing)](examples/time-of-day-and-dst-testing.md), and the cross-track [DI replacement](../concepts/examples/di-replacement-testing.md) and [test-clock control](../concepts/examples/test-clock-control-testing.md) pages.

The production DST examples are environment-aware so they can adapt to machine timezone capabilities. The testing examples show deterministic virtual-time patterns for reliable assertions.

## Related documentation

- [Choosing a package](../concepts/choosing-a-package.md)
- [KZDev.PrimeTime (NodaTime superset)](../primetime/primetime-superset.md)
- [Shared concepts](../concepts/concepts-timers-and-testing.md)
- **Production API:** [KZDev.SystemClock.PrimeTime](xref:KZDev.SystemClock.PrimeTime) · **Testing API:** [KZDev.SystemClock.PrimeTime.Testing](xref:KZDev.SystemClock.PrimeTime.Testing) (**both under `api/system-clock/`**)
