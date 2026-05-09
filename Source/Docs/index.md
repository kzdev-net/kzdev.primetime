---
_layout: landing
---

# PrimeTime

If production code calls **`DateTime.Now`**, **`DateTime.UtcNow`**, or **`TimeProvider.GetUtcNow()`** ad hoc, behavior tied to “wall clock” time becomes **hard to test** and **easy to get wrong** near **daylight-saving** transitions. PrimeTime gives you **injectable clocks**, **timers** with explicit scheduling semantics, and **paired testing packages** so suites can run against **virtual, deterministic time** instead of the real clock.

**KZDev.PrimeTime** and **KZDev.SystemClock.PrimeTime** share one **design** (same service registration shape and core types such as **`IPrimeClock`** / **`IPrimeTime`**) but target **different stacks**. Pick **exactly one production package** per application—they are **mutually exclusive**.

- **KZDev.PrimeTime** — **NodaTime superset** (`KZDev.PrimeTime`): NodaTime **`Instant`**, **`Duration`**, **`LocalTime`**, zoned “now” projections, and timer overloads aligned with that model. Ships with a **NodaTime** dependency (there is no separate NodaTime-only package).

- **KZDev.SystemClock.PrimeTime** — **BCL / `TimeProvider` subset** (`KZDev.SystemClock.PrimeTime`): overlapping contracts and the same registration entry point **names**, using **`TimeProvider`**, **`DateTimeOffset`**, and **`TimeSpan`**—without NodaTime.

Each production line has a **matching testing package**—**`KZDev.PrimeTime.Testing`** or **`KZDev.SystemClock.PrimeTime.Testing`**—with **`IPrimeTestClock`** / **`PrimeTestClock`** and helpers aligned to that stack’s types and dependencies. Use the tester package that matches your production choice. See **[Testing packages](articles/testing-packages.md)** for virtual time, DI patterns, and examples.

## Documentation

- [Overview](articles/overview.md)
- [Concepts](articles/concepts/index.md)
- [System Clock track](articles/system-clock/index.md)
- [PrimeTime (NodaTime) track](articles/primetime/index.md)
- [Security policy](https://github.com/kzdev-net/kzdev.primetime/blob/main/SECURITY.md) (repository root)
- [Contributing](https://github.com/kzdev-net/kzdev.primetime/blob/main/CONTRIBUTING.md) (repository root)
- [Release notes (all packages)](articles/release-notes.md)
- **API:** [System Clock stack](xref:KZDev.SystemClock.PrimeTime) (**`api/system-clock/`**) · [PrimeTime / NodaTime stack](xref:KZDev.PrimeTime) (**`api/primetime/`**)

Support expectations, lifecycle, and compatibility cadence are documented in the **[repository README](https://github.com/kzdev-net/kzdev.primetime/blob/main/README.md)** and **[SUPPORT.md](https://github.com/kzdev-net/kzdev.primetime/blob/main/SUPPORT.md)**—not duplicated here.

## NuGet

- [KZDev.PrimeTime](https://www.nuget.org/packages/KZDev.PrimeTime)
- [KZDev.SystemClock.PrimeTime](https://www.nuget.org/packages/KZDev.SystemClock.PrimeTime)
- [KZDev.PrimeTime.Testing](https://www.nuget.org/packages/KZDev.PrimeTime.Testing)
- [KZDev.SystemClock.PrimeTime.Testing](https://www.nuget.org/packages/KZDev.SystemClock.PrimeTime.Testing)
