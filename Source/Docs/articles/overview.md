# PrimeTime overview

**PrimeTime** is a pair of .NET libraries for **testable clocks**, **virtual time**, and **timer** registration (interval and, on modern targets, **time-of-day** with explicit **daylight-saving** policy).

## Two packages, one design

| Package | Namespace | Role |
|---------|-----------|------|
| **[KZDev.PrimeTime](https://www.nuget.org/packages/KZDev.PrimeTime)** | `KZDev.PrimeTime` | **Superset:** shared model **plus** **NodaTime** APIs on `IPrimeClock` (for example `Instant`, `Duration`, `LocalTime` timers). Includes a NodaTime dependency. |
| **[KZDev.SystemClock.PrimeTime](https://www.nuget.org/packages/KZDev.SystemClock.PrimeTime)** | `KZDev.SystemClock.PrimeTime` | **BCL subset:** same core type **names** and overlapping contracts using **`TimeProvider`** and BCL date/time types **only**—no NodaTime. |

Reference **exactly one** of these in a given app. They are **not** intended to be used together.

## Where to read next

1. [Choosing a package](choosing-a-package.md) — short comparison and decision table.
2. [KZDev.SystemClock.PrimeTime usage guide](systemclock-package.md) — `TimeProvider`, DI defaults, BCL-focused workflow.
3. [KZDev.PrimeTime usage guide](primetime-superset.md) — NodaTime-first workflow and superset-only APIs.
4. [Timers, daylight saving, and testing](concepts-timers-and-testing.md) — concepts that apply to both packages.

## API reference and external notes

- [API Reference](xref:PrimeTime) — generated from both assemblies published on this site.
- [Cron and scheduling notes](../Reference/CronNotes.md) — background on cron libraries in .NET (PrimeTime itself is a **clock/timer** library, not a cron engine).

## Source and license

Repository: [github.com/kzdev-net/kzdev.primetime](https://github.com/kzdev-net/kzdev.primetime)  
License: **MIT** (see repository `LICENSE`).
