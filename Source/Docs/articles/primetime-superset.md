# KZDev.PrimeTime — usage guide (NodaTime superset)

**Package ID:** `KZDev.PrimeTime`  
**Namespace:** `KZDev.PrimeTime`

This guide describes the **full** PrimeTime package: everything in the SystemClock subset **plus** **NodaTime**-centric members on **`IPrimeClock`** and NodaTime timer overloads. **NodaTime is a required dependency** of this package.

## When to use this package

- You already use **NodaTime** for instants, zones, and calendars.
- You want **`Instant`**, **`ZonedDateTime`**, **`LocalTime`**, and **`Duration`** on the same clock abstraction as your BCL projections.
- You want **`Duration`-based** interval registration and **`LocalTime`-based** time-of-day registration on **`IPrimeClock`** (see API reference).

If you want **only** BCL types and **`TimeProvider`**, use **[KZDev.SystemClock.PrimeTime](systemclock-package.md)** instead.

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
- **Time of day:** use **`LocalTime`** / **`LocalDate`** overloads on supported targets, with **`DayTimeTimerOptions`** for DST. See [Timers, daylight saving, and testing](concepts-timers-and-testing.md).

## Virtual time tests

**`PrimeTestClock`** supports NodaTime-centric construction (for example an initial **`Instant`** and **`DateTimeZone`**). That combination is especially useful for **deterministic DST** tests. See the [API Reference](xref:PrimeTime) for exact constructors and **`IPrimeTestClock`** members.

## Related documentation

- [Choosing a package](choosing-a-package.md)
- [KZDev.SystemClock.PrimeTime](systemclock-package.md)
- [Shared concepts](concepts-timers-and-testing.md)
- [API Reference](xref:PrimeTime)
