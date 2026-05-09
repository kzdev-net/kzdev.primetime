---
_layout: landing
---

# PrimeTime

**KZDev.PrimeTime** and **KZDev.SystemClock.PrimeTime** provide **clocks**, **timers**, and **virtual time** for .NET. Pick **one** package per application—they are **mutually exclusive**.

- **KZDev.PrimeTime** — **NodaTime superset** (`KZDev.PrimeTime`): NodaTime **`Instant`**, **`Duration`**, **`LocalTime`**, zoned “now” properties, and matching timer overloads on **`IPrimeClock`**. Ships with a **NodaTime** dependency (there is no separate NodaTime-only package).

- **KZDev.SystemClock.PrimeTime** — **BCL / `TimeProvider` subset** (`KZDev.SystemClock.PrimeTime`): overlapping contracts and the same service type **names**, without NodaTime.

## Documentation

- [Overview](articles/overview.md)
- [Concepts](articles/concepts/index.md)
- [System Clock track](articles/system-clock/index.md)
- [PrimeTime (NodaTime) track](articles/primetime/index.md)
- [Support and lifecycle](articles/support-and-lifecycle.md)
- [Security policy](https://github.com/kzdev-net/kzdev.primetime/blob/main/SECURITY.md) (repository root)
- [Contributing](https://github.com/kzdev-net/kzdev.primetime/blob/main/CONTRIBUTING.md) (repository root)
- [Release notes (all packages)](articles/release-notes.md)
- **API:** [System Clock stack](xref:KZDev.SystemClock.PrimeTime) (**`api/system-clock/`**) · [PrimeTime / NodaTime stack](xref:KZDev.PrimeTime) (**`api/primetime/`**)

## NuGet

- [KZDev.PrimeTime](https://www.nuget.org/packages/KZDev.PrimeTime)
- [KZDev.SystemClock.PrimeTime](https://www.nuget.org/packages/KZDev.SystemClock.PrimeTime)
- [KZDev.PrimeTime.Testing](https://www.nuget.org/packages/KZDev.PrimeTime.Testing)
- [KZDev.SystemClock.PrimeTime.Testing](https://www.nuget.org/packages/KZDev.SystemClock.PrimeTime.Testing)
