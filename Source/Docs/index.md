---
_layout: landing
---

# PrimeTime

**KZDev.PrimeTime** and **KZDev.SystemClock.PrimeTime** provide **clocks**, **timers**, and **virtual time** for .NET. Pick **one** package per application—they are **mutually exclusive**.

- **KZDev.PrimeTime** — **NodaTime superset** (`KZDev.PrimeTime`): NodaTime **`Instant`**, **`Duration`**, **`LocalTime`**, zoned “now” properties, and matching timer overloads on **`IPrimeClock`**. Ships with a **NodaTime** dependency (there is no separate NodaTime-only package).

- **KZDev.SystemClock.PrimeTime** — **BCL / `TimeProvider` subset** (`KZDev.SystemClock.PrimeTime`): overlapping contracts and the same service type **names**, without NodaTime.

## Documentation

- [Overview](articles/overview.md)
- [Choosing a package](articles/choosing-a-package.md)
- [Timers, DST, and testing](articles/concepts-timers-and-testing.md)
- [KZDev.SystemClock.PrimeTime guide](articles/systemclock-package.md)
- [KZDev.PrimeTime guide](articles/primetime-superset.md)
- [Cron and scheduling notes](Reference/CronNotes.md)
- [API Reference](xref:PrimeTime)

## NuGet

- [KZDev.PrimeTime](https://www.nuget.org/packages/KZDev.PrimeTime)
- [KZDev.SystemClock.PrimeTime](https://www.nuget.org/packages/KZDev.SystemClock.PrimeTime)
