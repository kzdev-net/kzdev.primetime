# Persistence and schedule-zone conversions — PrimeTime (NodaTime), production

This scenario uses **`IPrimeTime`** extension methods to project a persisted **[`Instant`](xref:NodaTime.Instant)** into the clock’s **schedule zone**, and shows **[`PrimeTimeOfDayConversion`](xref:KZDev.PrimeTime.PrimeTimeOfDayConversion)** plus **[`NodaDurationBclConversion`](xref:KZDev.PrimeTime.NodaDurationBclConversion)** for storage-friendly shapes and delay **`TimeSpan`** clamping.

For the conceptual background, see [Persistence and time conversions](../../concepts/persistence-and-conversions.md).

[!code-csharp[](../../../../Dev/Production/KZDev.PrimeTime.Examples/Scenarios/PersistenceAndConversionScenario.cs#Snippet)]

## What to notice

- **`ToScheduleZonedDateTime`**, **`ToScheduleLocalDate`**, **`ToScheduleLocalTime`**, and **`ToScheduleDateTimeOffset`** all interpret the instant in **`LocalScheduleDateTimeZone`** (see [`IPrimeClock.LocalScheduleDateTimeZone`](xref:KZDev.PrimeTime.IPrimeClock.LocalScheduleDateTimeZone)).
- **`LocalTimeOfDay`** round-trips with **`LocalTime`** via **`PrimeTimeOfDayConversion`** using midnight-based ticks.
- **`NodaDurationBclConversion.ToTimeSpanForDelay`** clamps oversized **`Duration`** values so BCL delays remain representable.

## Related

- **Concepts:** [Persistence and time conversions](../../concepts/persistence-and-conversions.md)
- **Usage guide:** [KZDev.PrimeTime](../primetime-superset.md)
- **API:** [`IPrimeClock`](xref:KZDev.PrimeTime.IPrimeClock) · [`PrimeTimeScheduleZoneExtensions`](xref:KZDev.PrimeTime.PrimeTimeScheduleZoneExtensions)
