---
_layout: landing
---

# PrimeTime

**KZDev.PrimeTime** (superset) and **KZDev.SystemClock.PrimeTime** (strict BCL subset) provide time and scheduling utilities for .NET. The subset reuses the same core type names and matching contract signatures for the shared surface; the superset adds NodaTime overloads on the same interfaces.

See the [API Reference](xref:PrimeTime) and [Reference](Reference/CronNotes.md) for details.

## Packages

Choose **one** of the following per app; the two packages are **not** meant to be used together:

- **KZDev.PrimeTime** — Core contracts and abstractions, clocks and timers backed by NodaTime (`KZDev.PrimeTime` namespace). There is no separate NodaTime-only package; NodaTime is part of this deliverable.

- **KZDev.SystemClock.PrimeTime** — BCL / `TimeProvider` clocks and timers only, with the same type names in the **`KZDev.SystemClock.PrimeTime`** namespace.

## Documentation

- [Cron and scheduling notes](Reference/CronNotes.md)
- [API Reference](xref:PrimeTime)
- [Overview](articles/overview.md)
