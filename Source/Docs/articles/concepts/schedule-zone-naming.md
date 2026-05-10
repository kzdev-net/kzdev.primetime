# Schedule zone naming (why APIs say “Schedule”)

PrimeTime uses the word **Schedule** in several public names—for example **`LocalScheduleTimeZone`**, **`LocalScheduleDateTimeZone`**, **`GetLocalScheduleTimeZoneInfo`**, and extension methods like **`ToScheduleLocalDate`**. This article explains **what that word is meant to signal** and **why** the libraries do not use shorter names such as “local time zone” everywhere.

## What “schedule” means here

In PrimeTime, **schedule** is shorthand for **the clock’s zone used for local wall-clock and calendar interpretation when scheduling and reasoning about timers**.

Concretely, that zone drives:

- **Local “now” in a zoned sense** — on **KZDev.PrimeTime**, for example [`LocalZonedNowInstant`](xref:KZDev.PrimeTime.IPrimeClock.LocalZonedNowInstant). On **both** stacks, BCL **“now”** members on [`IPrimeClock`](xref:KZDev.PrimeTime.IPrimeClock) / [`IPrimeClock`](xref:KZDev.SystemClock.PrimeTime.IPrimeClock) that represent **local** calendar or wall time are interpreted using this same zone (and **`LocalScheduleTimeZone`** exposes the BCL view).
- **Local time-of-day timers** — when you register a fire time as “local” (as opposed to “UTC time of day”), the library interprets that wall time **in this same zone**, including **daylight-saving** rules for that zone.
- **Projection of a stored absolute instant** — extension methods on [`IPrimeTime`](xref:KZDev.PrimeTime.IPrimeTime) (NodaTime stack) or [`IPrimeTime`](xref:KZDev.SystemClock.PrimeTime.IPrimeTime) (System Clock stack), such as **`ToScheduleLocalDate`**, **`ToScheduleDateTimeOffset`**, **`ToScheduleZonedDateTime`**, and related overloads (see [`PrimeTimeScheduleZoneExtensions`](xref:KZDev.PrimeTime.PrimeTimeScheduleZoneExtensions) and [`PrimeTimeScheduleZoneExtensions`](xref:KZDev.SystemClock.PrimeTime.PrimeTimeScheduleZoneExtensions)), convert a **single point in time** into **calendar and clock fields in that zone**.

So **“schedule” is not a separate third time zone**; it is a **label for the zone the clock uses for those behaviors**. Production clocks usually align that zone with the **system** or **provider** default; test clocks often **pin** it so results are deterministic.

## Why not just `LocalTimeZone` or “local”?

.NET already uses **“local time zone”** in core APIs—for example [`TimeProvider.LocalTimeZone`](https://learn.microsoft.com/dotnet/api/system.timeprovider.localtimezone) and [`TimeZoneInfo.Local`](https://learn.microsoft.com/dotnet/system.timezoneinfo.local). PrimeTime deliberately uses **Schedule** in names to reduce confusion:

| Without “Schedule” | Risk |
|--------------------|------|
| A property named like **`LocalTimeZone`** on **`IPrimeClock`** | Reads as a duplicate of **`TimeProvider.LocalTimeZone`** and does not convey that the value is **the clock’s configured zone for timer and zoned-local semantics**, which may be **explicitly set** on a test clock. |
| Assuming “local” always means **OS local** | Fails for **virtual** or **injected** zones in tests and for code that treats “local” as **business** or **operations** calendar context tied to the clock instance. |

**Schedule** signals: *this is the zone used for **PrimeTime’s local scheduling model** (local zoned “now” + local day boundaries + local time-of-day timers), not a generic claim about every possible meaning of “local” in your app.*

## Where the schedule zone is exposed

- **`LocalScheduleTimeZone`** ([`IPrimeClock`](xref:KZDev.PrimeTime.IPrimeClock) / [`IPrimeClock`](xref:KZDev.SystemClock.PrimeTime.IPrimeClock)) — BCL [`TimeZoneInfo`](https://learn.microsoft.com/dotnet/api/system.timezoneinfo) view of the zone, including interop rules when the underlying NodaTime zone is not a simple BCL wrapper (see **`GetLocalScheduleTimeZoneInfo`** on [`NodaDateTimeZoneBclConversion`](xref:KZDev.PrimeTime.NodaDateTimeZoneBclConversion) in **KZDev.PrimeTime**).
- **`LocalScheduleDateTimeZone`** ([`IPrimeClock`](xref:KZDev.PrimeTime.IPrimeClock) only) — the NodaTime [`DateTimeZone`](xref:NodaTime.DateTimeZone) object used for the same semantics; avoids reconstructing the zone from BCL alone when you are already on the NodaTime stack.

When the clock is adapted to [`TimeProvider`](https://learn.microsoft.com/dotnet/api/system.timeprovider), **`LocalScheduleTimeZone`** is intended to match the provider’s **`LocalTimeZone`** where that adapter is used—see remarks on **`LocalScheduleTimeZone`** in the API reference.

## Schedule-prefixed extension methods

Methods named **`ToSchedule…`** on **`IPrimeTime`** mean: **re-project this absolute time into the clock’s schedule zone** (same zone as above). They require the receiver to be an **`IPrimeClock`** (or throw with a clear error); see [Persistence and time conversions](persistence-and-conversions.md).

## Related reading

- [Persistence and time conversions](persistence-and-conversions.md) — storing instants and using schedule-zone projections vs raw NodaTime/BCL.
- [Timers, daylight saving, and testing](concepts-timers-and-testing.md) — how local vs UTC time-of-day interacts with DST.
- **API:** [System Clock stack](xref:KZDev.SystemClock.PrimeTime) · [PrimeTime / NodaTime stack](xref:KZDev.PrimeTime)
