# KZDev PrimeTime

PrimeTime is a **.NET** library family for **injectable clocks**, **deterministic test time**, and **timer** registration—including **interval** timers and, on modern frameworks, **local time-of-day** schedules with explicit **daylight-saving** behavior.

This repository builds **four NuGet packages**. It ships two production packages and two testing packages that share one **design** but differ by
**namespace**, **dependencies**, and **API surface**. **Use only one production package per application.**

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

## Support, lifecycle, security, and contributing

- **Support and lifecycle policy:** [SUPPORT.md](SUPPORT.md) (best-effort support, no SLA, versioning expectations).
- **Security disclosures:** [SECURITY.md](SECURITY.md) (public GitHub Issues accepted; optional private advisory).
- **Contributing:** [CONTRIBUTING.md](CONTRIBUTING.md) (external PRs not accepted; Issues and Discussions welcome).
- **Hosted summary:** [Support and lifecycle](Source/Docs/articles/support-and-lifecycle.md) in docs (links to the canonical `SUPPORT.md` on GitHub).
- **Per-package release notes** (source for NuGet `PackageReleaseNotes`):
  - [KZDev.PrimeTime](Source/Docs/Notes/KZDev.PrimeTime.release-notes.md)
  - [KZDev.SystemClock.PrimeTime](Source/Docs/Notes/KZDev.SystemClock.PrimeTime.release-notes.md)
  - [KZDev.PrimeTime.Testing](Source/Docs/Notes/KZDev.PrimeTime.Testing.release-notes.md)
  - [KZDev.SystemClock.PrimeTime.Testing](Source/Docs/Notes/KZDev.SystemClock.PrimeTime.Testing.release-notes.md)
- **Index (DocFX):** [Release notes](Source/Docs/articles/release-notes.md).

## Features (high level)

- **`IPrimeClock` / `PrimeClock`** — production clock; timer registration and “now” projections (exact members depend on package and TFM).
- **Testing packages** — virtual time for tests (`IPrimeTestClock` / `PrimeTestClock`) is provided by `KZDev.PrimeTime.Testing` or `KZDev.SystemClock.PrimeTime.Testing`.
- **Day-time timers** — `SkippedTimeBehavior` and `DuplicateTimeBehavior` on `DayTimeTimerOptions` for local wall-clock scheduling near DST transitions (see docs).
- **`ToTimeProvider`** — adapt an `IPrimeClock` to `TimeProvider` for interoperability.

## Repository and license

- **Source:** [github.com/kzdev-net/kzdev.primetime](https://github.com/kzdev-net/kzdev.primetime)
- **License:** [MIT](LICENSE)
- **Governance:** [SECURITY.md](SECURITY.md), [CONTRIBUTING.md](CONTRIBUTING.md)

## Contributing

External pull requests are not accepted. Use GitHub **Issues** and **Discussions** as described in [CONTRIBUTING.md](CONTRIBUTING.md) and [SUPPORT.md](SUPPORT.md). The hosted overview is in [Support and lifecycle](Source/Docs/articles/support-and-lifecycle.md).
