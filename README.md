# KZDev PrimeTime

PrimeTime is a **.NET** library family for **injectable clocks**, **deterministic test time**, and **timer** registration—including **interval** timers and, on modern frameworks, **local time-of-day** schedules with explicit **daylight-saving** behavior.

This repository builds **two NuGet packages**. They share one **design**; they differ by **namespace**, **dependencies**, and **API surface**. **Use only one package per application.**

## Packages

| Package | Namespace | Stack | NuGet |
|---------|-----------|--------|--------|
| **KZDev.PrimeTime** | `KZDev.PrimeTime` | **NodaTime superset** — `Instant`, `Duration`, `LocalTime`, zoned “now”, NodaTime timer overloads on `IPrimeClock`. | [![NuGet](https://img.shields.io/nuget/v/KZDev.PrimeTime.svg)](https://www.nuget.org/packages/KZDev.PrimeTime) |
| **KZDev.SystemClock.PrimeTime** | `KZDev.SystemClock.PrimeTime` | **BCL subset** — `TimeProvider`, `DateTimeOffset` / `TimeSpan`, no NodaTime. | [![NuGet](https://img.shields.io/nuget/v/KZDev.SystemClock.PrimeTime.svg)](https://www.nuget.org/packages/KZDev.SystemClock.PrimeTime) |

**Do not reference both packages** in the same app.

### Quick install

```bash
# NodaTime superset
dotnet add package KZDev.PrimeTime

# BCL / TimeProvider only
dotnet add package KZDev.SystemClock.PrimeTime
```

### Target frameworks

Both libraries target **`net10.0`**, **`net8.0`**, and **`netstandard2.0`** (with BCL polyfills on `netstandard2.0` where required).

### Dependency injection (same entry point name, different assembly)

Each package exposes **`AddPrimeClock`** on **`IServiceCollection`**:

- **KZDev.PrimeTime** registers **`NodaTime.IClock`** (default `SystemClock.Instance`), then **`IPrimeClock`** → **`PrimeClock`**, and **`IPrimeTime`** → the same instance.
- **KZDev.SystemClock.PrimeTime** registers **`TimeProvider`** (default `TimeProvider.System`), then **`IPrimeClock`** → **`PrimeClock`**, and **`IPrimeTime`** → the same instance.

## Documentation

- **Hosted docs (DocFX):** [kzdev-net.github.io/kzdev.primetime](https://kzdev-net.github.io/kzdev.primetime/)
- **Articles in repo:** [Source/Docs/articles](Source/Docs/articles/) — start with [Choosing a package](Source/Docs/articles/choosing-a-package.md), then the guide for [SystemClock](Source/Docs/articles/systemclock-package.md) or [PrimeTime superset](Source/Docs/articles/primetime-superset.md).

## Features (high level)

- **`IPrimeClock` / `PrimeClock`** — production clock; timer registration and “now” projections (exact members depend on package and TFM).
- **`IPrimeTestClock` / `PrimeTestClock`** — virtual time for tests (`Advance`, `RunFor`, …).
- **Day-time timers** — `SkippedTimeBehavior` and `DuplicateTimeBehavior` on `DayTimeTimerOptions` for local wall-clock scheduling near DST transitions (see docs).
- **`ToTimeProvider`** — adapt an `IPrimeClock` to `TimeProvider` for interoperability.

## Repository and license

- **Source:** [github.com/kzdev-net/kzdev.primetime](https://github.com/kzdev-net/kzdev.primetime)
- **License:** [MIT](LICENSE)

## Contributing

At this time, external pull requests are not accepted. Feedback and bug reports are welcome via GitHub **Discussions** and **Issues**; see the hosted overview for the maintainer’s preferred channels.
