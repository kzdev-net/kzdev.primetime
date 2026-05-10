# PrimeTime overview

**PrimeTime** is a pair of .NET libraries for **testable clocks**, **virtual time**, and **timer** registration (interval and, on modern targets, **time-of-day** with explicit **daylight-saving** policy).

## Two packages, one design

| Package | Namespace | Role |
|---------|-----------|------|
| **[KZDev.PrimeTime](https://www.nuget.org/packages/KZDev.PrimeTime)** | `KZDev.PrimeTime` | **Superset:** shared model with **BCL plus NodaTime** APIs on `IPrimeClock` (for example `Instant`, `Duration`, `LocalTime` timers). Includes all SystemClock functionality plus NodaTime extensions. |
| **[KZDev.SystemClock.PrimeTime](https://www.nuget.org/packages/KZDev.SystemClock.PrimeTime)** | `KZDev.SystemClock.PrimeTime` | **BCL foundation:** core contracts using **`TimeProvider`** and BCL date/time types. Minimal dependencies. |

Reference **exactly one** of these in a given app. They are **not** intended to be used together.

## Where to read next

1. [Choosing a package](concepts/choosing-a-package.md) — short comparison and decision table.
2. [Schedule zone naming](concepts/schedule-zone-naming.md) — why APIs use **Schedule** (`LocalScheduleTimeZone`, `ToScheduleLocalDate`, and related names) and how that differs from “local time zone” in .NET.
3. [Persistence and time conversions](concepts/persistence-and-conversions.md) — storing instants and zoned values; schedule-zone helpers vs raw NodaTime/BCL.
4. [KZDev.SystemClock.PrimeTime usage guide](system-clock/systemclock-package.md) — `TimeProvider`, DI defaults, BCL-focused workflow.
5. [KZDev.PrimeTime usage guide](primetime/primetime-superset.md) — NodaTime-first workflow and superset-only APIs.
6. [Timers, daylight saving, and testing](concepts/concepts-timers-and-testing.md) — concepts that apply to both packages.
7. **API reference**: [System Clock API](xref:KZDev.SystemClock.PrimeTime), [PrimeTime / NodaTime API](xref:KZDev.PrimeTime).

## Source and license

Repository: [github.com/kzdev-net/kzdev.primetime](https://github.com/kzdev-net/kzdev.primetime)  
License: **MIT** (see repository `LICENSE`).
