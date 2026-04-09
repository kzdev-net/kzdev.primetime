# KZDev.SystemClock.PrimeTime — usage guide

**Package ID:** `KZDev.SystemClock.PrimeTime`  
**Namespace:** `KZDev.SystemClock.PrimeTime`

This guide describes the **BCL / `TimeProvider`** deliverable: same **names** (`IPrimeClock`, `PrimeClock`, `PrimeTestClock`, …) as the superset where they overlap, **without** NodaTime.

## When to use this package

- You standardize on **`DateTimeOffset`**, **`TimeSpan`**, and **`TimeProvider`**.
- You **must not** add a NodaTime dependency.
- You want **`IPrimeClock.LocalScheduleTimeZone`** for local calendar time-of-day behavior (see API docs).

If you need **`Instant`**, **`Duration`**, or **`LocalTime`** members on **`IPrimeClock`**, use **[KZDev.PrimeTime](primetime-superset.md)** instead.

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

Prefer **`IPrimeClock`** in application code. Typical members include UTC and local projections using BCL types (for example **`UtcNowDateTimeOffset`**), plus timer registration methods. Exact members depend on target framework; see the [API Reference](xref:PrimeTime).

## Time-of-day timers

On supported targets, use **`RegisterTimeOfDay`** / **`RegisterAsyncTimeOfDay`** with **`LocalTimeOfDay`** / **`UtcTimeOfDay`** wrappers and **`DayTimeTimerOptions`** for DST policy. See [Timers, daylight saving, and testing](concepts-timers-and-testing.md).

## Virtual time tests

Use **`PrimeTestClock`** and **`IPrimeTestClock`**. Constructors are **BCL-first** (for example starting from a **`DateTimeOffset`**). Advance virtual time to exercise timers without wall-clock delays.

## Related documentation

- [Choosing a package](choosing-a-package.md)
- [KZDev.PrimeTime (NodaTime superset)](primetime-superset.md)
- [Shared concepts](concepts-timers-and-testing.md)
- [API Reference](xref:PrimeTime)
