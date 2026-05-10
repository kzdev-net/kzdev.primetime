# Choosing a PrimeTime package

PrimeTime ships as **two NuGet packages**. They share one **design** (clock abstraction, timers, test clock) but differ in **namespace**, **time stack**, and **API surface**.

## Rules

1. **Pick exactly one package** per application (or bounded context). They are **not** designed to be referenced together.
2. Choose from **your time model**:
   - **BCL types preferred, minimal dependencies** → **KZDev.SystemClock.PrimeTime** (`KZDev.SystemClock.PrimeTime`).
   - **NodaTime in production** (or you want NodaTime extensions alongside BCL support) → **KZDev.PrimeTime** (`KZDev.PrimeTime`).

## Side-by-side comparison

| Topic | **KZDev.SystemClock.PrimeTime** | **KZDev.PrimeTime** |
|--------|----------------------------------|---------------------|
| **Namespace** | `KZDev.SystemClock.PrimeTime` | `KZDev.PrimeTime` |
| **NodaTime dependency** | No | Yes (bundled; no separate NodaTime-only package) |
| **Default DI backing** | `TimeProvider` (`TimeProvider.System`) | NodaTime `IClock` (`SystemClock.Instance`) |
| **`IPrimeClock` extras** | BCL-oriented (e.g. `LocalScheduleTimeZone` for local day-time scheduling when available) | NodaTime-oriented (`NowInstant`, `Duration` timers, `LocalTime` time-of-day timers, zoned “now” properties) |
| **Typical use** | Libraries and apps standardizing on `TimeProvider` / `DateTimeOffset` | Apps already using NodaTime for instants, zones, and calendars |

## Decision guide

- **You do not use NodaTime** and want the smallest dependency graph → **SystemClock** package.
- **You already depend on NodaTime** and want timer and “now” APIs expressed with `Instant`, `Duration`, `LocalTime`, and `ZonedDateTime` → **PrimeTime** superset.
- **You need `IPrimeClock.LocalScheduleTimeZone`** (explicit `TimeZoneInfo` for local wall-clock scheduling) → **SystemClock** package (this member is specific to that assembly).
- **You need NodaTime timer overloads** on the same `IPrimeClock` → **PrimeTime** superset.

## Next steps

- [KZDev.SystemClock.PrimeTime usage guide](../system-clock/systemclock-package.md)
- [KZDev.PrimeTime (NodaTime superset) usage guide](../primetime/primetime-superset.md)
- [Timers, daylight saving, and testing](concepts-timers-and-testing.md)
- **API:** [System Clock stack](xref:KZDev.SystemClock.PrimeTime) · [PrimeTime / NodaTime stack](xref:KZDev.PrimeTime)
