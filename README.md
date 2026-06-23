# KZDev PrimeTime

PrimeTime is a **.NET** library family for **injectable clocks**, **deterministic test time**, and **timer** registration—including **interval** timers and, on modern frameworks, **local time-of-day** schedules with explicit **daylight-saving** behavior.

This repository builds **four NuGet packages**. It ships two production packages and two testing packages that share one **design** but differ by
**namespace**, **dependencies**, and **API surface**. **Use only one production package per application.**

## Packages

| Package | Namespace | Stack | NuGet |
|---------|-----------|--------|--------|
| **KZDev.PrimeTime** | `KZDev.PrimeTime` | **NodaTime superset** — `Instant`, `Duration`, `LocalTime`, zoned “now”, NodaTime timer overloads on `IPrimeClock`. | [![NuGet](https://img.shields.io/nuget/v/KZDev.PrimeTime.svg)](https://www.nuget.org/packages/KZDev.PrimeTime) |
| **KZDev.SystemClock.PrimeTime** | `KZDev.SystemClock.PrimeTime` | **BCL subset** — `TimeProvider`, `DateTimeOffset` / `TimeSpan`, no NodaTime. | [![NuGet](https://img.shields.io/nuget/v/KZDev.SystemClock.PrimeTime.svg)](https://www.nuget.org/packages/KZDev.SystemClock.PrimeTime) |
| **KZDev.PrimeTime.Testing** | `KZDev.PrimeTime` | **Testing** — `IPrimeTestClock` / `PrimeTestClock` for deterministic tests; pair with **KZDev.PrimeTime**. | [![NuGet](https://img.shields.io/nuget/v/KZDev.PrimeTime.Testing.svg)](https://www.nuget.org/packages/KZDev.PrimeTime.Testing) |
| **KZDev.SystemClock.PrimeTime.Testing** | `KZDev.SystemClock.PrimeTime` | **Testing** — virtual time for tests; pair with **KZDev.SystemClock.PrimeTime**. | [![NuGet](https://img.shields.io/nuget/v/KZDev.SystemClock.PrimeTime.Testing.svg)](https://www.nuget.org/packages/KZDev.SystemClock.PrimeTime.Testing) |

**Do not reference both production packages** in the same app. Use one testing package per test project, matched to your production package. See [Testing packages](Source/Docs/articles/testing-packages.md) for install, DI, and examples.

### Quick install

```bash
# NodaTime superset
dotnet add package KZDev.PrimeTime

# BCL / TimeProvider only
dotnet add package KZDev.SystemClock.PrimeTime
```

### Target frameworks

All four packages target **`net10.0`**, **`net8.0`**, and **`netstandard2.0`** (with BCL polyfills on `netstandard2.0` where required). The `netstandard2.0` binaries support .NET Framework **4.8.1** and later consumers via the standard compatibility surface.

### Supported platforms and test coverage

| Area | What is covered |
|------|-----------------|
| **Shipped package TFMs** | `net10.0`, `net8.0`, `netstandard2.0` (unchanged across all four packages) |
| **CI-validated** | Unit and integration tests on **`net8.0`** and **`net10.0`** on `ubuntu-latest` ([ci.yml](.github/workflows/ci.yml)) |
| **.NET Framework / net481** | Package consumption via `netstandard2.0` is supported; repository **test projects** also target **`net481`**, but that TFM is **not** exercised in CI. Validate on **Windows** locally (Visual Studio, ReSharper, or the xUnit v3 `.exe` runner — see [Local net481 testing](Source/Reference/local-net481-testing.md)). |

This matrix is **best-effort** for .NET Framework: there is no Windows CI job and no guarantee of automated net481 regression coverage on every change.

### Dependency injection (same entry point name, different assembly)

Each package exposes **`AddPrimeClock`** on **`IServiceCollection`**:

- **KZDev.PrimeTime** registers **`NodaTime.IClock`** (default `SystemClock.Instance`), then **`IPrimeClock`** → **`PrimeClock`**, and **`IPrimeTime`** → the same instance.
- **KZDev.SystemClock.PrimeTime** registers **`TimeProvider`** (default `TimeProvider.System`), then **`IPrimeClock`** → **`PrimeClock`**, and **`IPrimeTime`** → the same instance.

## Documentation

- **Hosted docs (DocFX):** [kzdev-net.github.io/kzdev.primetime](https://kzdev-net.github.io/kzdev.primetime/)
- **Articles in repo:** [Source/Docs/articles](Source/Docs/articles/) — start with [Choosing a package](Source/Docs/articles/concepts/choosing-a-package.md), then the guide for [SystemClock](Source/Docs/articles/system-clock/systemclock-package.md) or [PrimeTime superset](Source/Docs/articles/primetime/primetime-superset.md).

## Support, lifecycle, security, and contributing

- **Support and lifecycle policy:** [SUPPORT.md](SUPPORT.md) (best-effort support, no SLA, versioning expectations).
- **Security disclosures:** [SECURITY.md](SECURITY.md) (public GitHub Issues accepted; optional private advisory).
- **Contributing:** [CONTRIBUTING.md](CONTRIBUTING.md) (external PRs not accepted; Issues and Discussions welcome).
- **Per-package release notes** (source for NuGet `PackageReleaseNotes`):
  - [KZDev.PrimeTime](Source/Docs/Notes/KZDev.PrimeTime.release-notes.md)
  - [KZDev.SystemClock.PrimeTime](Source/Docs/Notes/KZDev.SystemClock.PrimeTime.release-notes.md)
  - [KZDev.PrimeTime.Testing](Source/Docs/Notes/KZDev.PrimeTime.Testing.release-notes.md)
  - [KZDev.SystemClock.PrimeTime.Testing](Source/Docs/Notes/KZDev.SystemClock.PrimeTime.Testing.release-notes.md)
- **Index (DocFX):** [Release notes](Source/Docs/articles/release-notes.md).

### Compatibility and maintenance cadence

- **SemVer compatibility:** Breaking API changes are intended for major version bumps only; minor/patch updates are expected to remain compatible.
- **Per-package lifecycle:** The latest published `major.minor` for each package line is the primary maintenance focus, including both production and testing packages.
- **Older package lines:** Backports to older lines are best-effort and not guaranteed; plan upgrades to current package lines.
- **Cadence expectation:** Releases are need-driven (no fixed schedule). Change details and compatibility implications are published in each package release-notes file.

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

External pull requests are not accepted. Use GitHub **Issues** and **Discussions** as described in [CONTRIBUTING.md](CONTRIBUTING.md) and [SUPPORT.md](SUPPORT.md).
