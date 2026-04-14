# KZDev.PrimeTime

**KZDev.PrimeTime** is the full **PrimeTime** stack for .NET: a testable production clock abstraction (`IPrimeClock`), interval and (on modern .NET) local **time-of-day** timers with explicit daylight-saving behavior, and **NodaTime**-first APIs (`Instant`, `Duration`, `LocalTime`, `ZonedDateTime`, and timer overloads).

Use the namespace **`KZDev.PrimeTime`**. This package **pulls in [NodaTime](https://nodatime.org/)** (see your lock file for the resolved version). There is **no** separate `KZDev.PrimeTime.NodaTime` package.

If you must avoid a NodaTime dependency, use **[KZDev.SystemClock.PrimeTime](https://www.nuget.org/packages/KZDev.SystemClock.PrimeTime)** instead. **Do not reference both packages** in the same application.

## Requirements

- **Target frameworks:** `net10.0`, `net8.0`, and `netstandard2.0` (with BCL polyfills where needed).
- **Dependencies:** `Microsoft.Extensions.DependencyInjection`, `NodaTime`, plus `Microsoft.Bcl.TimeProvider` and related polyfills on `netstandard2.0`.

## Installation

```bash
dotnet add package KZDev.PrimeTime
```

Or add a `PackageReference` to **`KZDev.PrimeTime`** and set the **version** to the release you validate against (central package management or a fixed version).

## Quick start (dependency injection)

```csharp
using KZDev.PrimeTime;
using Microsoft.Extensions.DependencyInjection;

IServiceCollection services = new ServiceCollection();
services.AddPrimeClock();

ServiceProvider provider = services.BuildServiceProvider();
IPrimeClock clock = provider.GetRequiredService<IPrimeClock>();

// NodaTime-first “now”
NodaTime.Instant now = clock.NowInstant;
```

Default registration:

- **`IPrimeClock`** → **`PrimeClock`** singleton, backed by NodaTime’s **`IClock`** (`SystemClock.Instance` unless you replace `IClock` first).
- **`IPrimeTime`** → same instance as **`IPrimeClock`**.

## Testing package

Testing APIs are provided by **`KZDev.PrimeTime.Testing`**. Add that package when you need virtual-time test types such as `IPrimeTestClock` and `PrimeTestClock`.

## Why NodaTime here?

The superset package exposes **additional members** on **`IPrimeClock`** (for example `NowInstant`, `LocalZonedNowInstant`, `Duration`-based `RegisterTimer`, and `LocalTime`-based `RegisterTimeOfDay`) that are not present on the **SystemClock** assembly. That keeps calendar and wall-time semantics aligned with NodaTime where you already use it.

## Documentation and source

- **[Product documentation](https://kzdev-net.github.io/kzdev.primetime/)** (DocFX site, including package comparison and guides).
- **Source and issues:** [github.com/kzdev-net/kzdev.primetime](https://github.com/kzdev-net/kzdev.primetime)
- **License:** MIT (see package metadata and repository `LICENSE`).

## See also

- [KZDev.SystemClock.PrimeTime](https://www.nuget.org/packages/KZDev.SystemClock.PrimeTime) — BCL / `TimeProvider` subset, namespace `KZDev.SystemClock.PrimeTime`.
- [KZDev.PrimeTime.Testing](https://www.nuget.org/packages/KZDev.PrimeTime.Testing) — testing/virtual-time APIs for `KZDev.PrimeTime`.
