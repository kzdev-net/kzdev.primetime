# Persistence and time conversions

This article describes **how to store** absolute and zoned time with PrimeTime, **when to use** library conversion helpers versus raw NodaTime or BCL APIs, and **where** those helpers live in each package.

## Canonical persistence shapes

- **Absolute instant (UTC timeline):** Prefer a single unambiguous point on the UTC line. In **KZDev.PrimeTime**, [`Instant`](xref:NodaTime.Instant) is the natural type; serialize as a string or as **ticks since the NodaTime epoch** (or another stable numeric encoding your team agrees on). In **KZDev.SystemClock.PrimeTime**, the same idea is a **`DateTimeOffset`** with **UTC offset zero** (or equivalent contract in your schema).
- **Zoned wall clock:** If you must recover **local calendar and clock** in a **specific** zone, store the **instant** plus a **time zone id** (for example IANA **`America/New_York`**), or store a **[`ZonedDateTime`](xref:NodaTime.ZonedDateTime)** in a format that preserves zone and offset rules. Do not persist only a local date/time without the zone id if the value is meant to be interpreted in that zone across DST changes.
- **Local time of day (no date):** Use **`LocalTimeOfDay`** / **`UtcTimeOfDay`** (or BCL **`TimeOnly`**) when the domain meaning is “this clock time every day” and you combine it with a **calendar date** separately—typically for **time-of-day timers** (see [Timers, daylight saving, and testing](concepts-timers-and-testing.md)).

## Schedule zone vs “UTC day”

For **why** public APIs use the word **Schedule** in names such as **`LocalScheduleTimeZone`** and **`ToScheduleLocalDate`**, see [Schedule zone naming (why “Schedule” in API names?)](schedule-zone-naming.md).

PrimeTime’s **local schedule** uses the same zone as **`IPrimeClock.LocalScheduleTimeZone`** (BCL) or **`IPrimeClock.LocalScheduleDateTimeZone`** (NodaTime, **KZDev.PrimeTime** only). Extensions on **`IPrimeTime`** such as **`ToScheduleLocalDate`**, **`ToScheduleDateTimeOffset`**, and related members project a **stored absolute** value into **that** zone—so “what day is it on the operations calendar?” and “what is the wall clock there?” align with how **time-of-day timers** interpret local time.

Those extensions are implemented in:

- **`KZDev.PrimeTime`:** [`PrimeTimeScheduleZoneExtensions`](xref:KZDev.PrimeTime.PrimeTimeScheduleZoneExtensions) (**[`Instant`](xref:NodaTime.Instant)** / **`ZonedDateTime`** → Noda and BCL schedule-local shapes).
- **`KZDev.SystemClock.PrimeTime`:** [`PrimeTimeScheduleZoneExtensions`](xref:KZDev.SystemClock.PrimeTime.PrimeTimeScheduleZoneExtensions) (**`DateTimeOffset`** / UTC **`DateTime`** → BCL schedule-local **`DateTimeOffset`**, **`DateOnly`**, **`TimeOnly`**, and wall **`DateTime`** helpers).

### `IPrimeTime` must be an `IPrimeClock`

Schedule-zone conversions need **`LocalScheduleTimeZone`** / **`LocalScheduleDateTimeZone`**. The extensions therefore treat the **`IPrimeTime`** receiver as **`IPrimeClock`**. If the receiver is **not** a clock, they throw **`ArgumentException`** with a message that states schedule-zone projection requires **`IPrimeClock`**. In typical applications and tests you resolve **`IPrimeTime`** from the same DI registration as **`IPrimeClock`** (or use **`PrimeTestClock`**), so this rarely surfaces.

## NodaTime-only helpers (`KZDev.PrimeTime`)

The superset package also exposes small **static** conversions that **do not** need schedule context:

- **[`PrimeTimeOfDayConversion`](xref:KZDev.PrimeTime.PrimeTimeOfDayConversion)** — between **`LocalTime`**, **`LocalTimeOfDay`** / **`UtcTimeOfDay`**, and **`TimeOnly`** (modern targets), preserving tick resolution.
- **[`NodaDurationBclConversion`](xref:KZDev.PrimeTime.NodaDurationBclConversion)** — policy-preserving mapping of **`Duration`** to **`TimeSpan`** for **delays** (values beyond BCL range are **clamped** to **`TimeSpan.MaxValue`** so timer/delay paths stay safe).
- **[`NodaDateTimeZoneBclConversion`](xref:KZDev.PrimeTime.NodaDateTimeZoneBclConversion)** — BCL **`TimeZoneInfo`** ↔ Noda **`DateTimeZone`** where the library already encodes interop rules.

For conversions that are **straightforward one-liners** in NodaTime or the BCL (for example **`Instant.ToDateTimeUtc()`**, **`LocalDate` + `LocalTime` → `LocalDateTime`**), prefer the **official** NodaTime/BCL APIs; PrimeTime does not attempt a full **N×M** type matrix.

## Runnable examples

**Production**

- [Persistence and conversions — PrimeTime (NodaTime)](../primetime/examples/persistence-and-conversions-production.md)
- [Persistence and conversions — System Clock](../system-clock/examples/persistence-and-conversions-production.md)

**Testing (virtual time)**

- [Persistence and conversions — PrimeTime (NodaTime) testing](../primetime/examples/persistence-and-conversions-testing.md)
- [Persistence and conversions — System Clock testing](../system-clock/examples/persistence-and-conversions-testing.md)

## Related

- [Choosing a package](choosing-a-package.md)
- [Schedule zone naming (why “Schedule” in API names?)](schedule-zone-naming.md)
- [Timers, daylight saving, and testing](concepts-timers-and-testing.md)
- [Event monitoring (ETW)](event-monitoring.md) — persistence shapes for monitored events, if applicable
- **API:** [System Clock stack](xref:KZDev.SystemClock.PrimeTime) · [PrimeTime / NodaTime stack](xref:KZDev.PrimeTime)
