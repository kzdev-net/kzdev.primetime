# KZDev.SystemClock.PrimeTime

**KZDev.SystemClock.PrimeTime** is the **strict BCL subset** of PrimeTime: the same production clock model (`IPrimeClock`, `PrimeClock`, timer registrations, and options types) using **`TimeProvider`** and **`DateTimeOffset`** / **`TimeSpan`** only—**without** a NodaTime dependency.

Use the namespace **`KZDev.SystemClock.PrimeTime`**.

For **NodaTime** overloads on `IPrimeClock` (for example `Instant`, `Duration`, and `LocalTime` timer surfaces), use **[KZDev.PrimeTime](https://www.nuget.org/packages/KZDev.PrimeTime)** instead. **Do not reference both packages** in the same application.

## Requirements

- **Target frameworks:** `net10.0`, `net8.0`, and `netstandard2.0` (with BCL polyfills where needed).
- **Dependencies:** `Microsoft.Extensions.DependencyInjection`, plus `Microsoft.Bcl.TimeProvider` and related polyfills on `netstandard2.0`.

## Installation

```bash
dotnet add package KZDev.SystemClock.PrimeTime
```

Or add a `PackageReference` to **`KZDev.SystemClock.PrimeTime`** and set the **version** to the release you validate against (central package management or a fixed version).

## Quick start (dependency injection)

```csharp
using KZDev.SystemClock.PrimeTime;
using Microsoft.Extensions.DependencyInjection;

IServiceCollection services = new ServiceCollection();
services.AddPrimeClock();

ServiceProvider provider = services.BuildServiceProvider();
IPrimeClock clock = provider.GetRequiredService<IPrimeClock>();

System.DateTimeOffset utcNow = clock.UtcNowDateTimeOffset;
```

Default registration:

- **`IPrimeClock`** → **`PrimeClock`** singleton, backed by **`TimeProvider.System`** unless you register **`TimeProvider`** first.
- **`IPrimeTime`** → same instance as **`IPrimeClock`**.

## Testing package

Testing APIs are provided by **`KZDev.SystemClock.PrimeTime.Testing`**. Add that package when you need virtual-time test types such as `IPrimeTestClock` and `PrimeTestClock`.

## SystemClock-specific surface

The BCL package adds **`IPrimeClock.LocalScheduleTimeZone`** for local calendar **time-of-day** scheduling (when available on your target framework). The NodaTime superset does not declare that member; there, local scheduling follows the zoned NodaTime view (`LocalZonedNowInstant.Zone`).

## Documentation and source

- **[Product documentation](https://kzdev-net.github.io/kzdev.primetime/)** (package comparison and guides).
- **Source and issues:** [github.com/kzdev-net/kzdev.primetime](https://github.com/kzdev-net/kzdev.primetime)
- **License:** MIT (see package metadata and repository `LICENSE`).

## See also

- [KZDev.PrimeTime](https://www.nuget.org/packages/KZDev.PrimeTime) — NodaTime superset, namespace `KZDev.PrimeTime`.
- [KZDev.SystemClock.PrimeTime.Testing](https://www.nuget.org/packages/KZDev.SystemClock.PrimeTime.Testing) — testing/virtual-time APIs for `KZDev.SystemClock.PrimeTime`.
