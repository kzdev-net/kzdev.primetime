# Persistence and schedule-zone conversions — System Clock, production

This scenario projects a persisted UTC **`DateTimeOffset`** into the clock’s **`LocalScheduleTimeZone`**, yielding BCL **`DateOnly`**, **`TimeOnly`**, and wall **`DateTime`** via **`DateOnly.ToDateTime(TimeOnly)`**.

For the conceptual background, see [Persistence and time conversions](../../concepts/persistence-and-conversions.md).

[!code-csharp[](../../../../Dev/Production/KZDev.SystemClock.PrimeTime.Examples/Scenarios/PersistenceAndConversionScenario.cs#Snippet)]

## What to notice

- **`ToScheduleDateTimeOffset`**, **`ToScheduleDateOnly`**, and **`ToScheduleTimeOnly`** align with **`TimeZoneInfo.ConvertTime`** for the clock’s schedule zone.
- Recombining **`DateOnly`** + **`TimeOnly`** gives an **unspecified**-kind **`DateTime`** representing the **wall** calendar and clock in that zone (not a UTC instant by itself).

## Related

- **Concepts:** [Persistence and time conversions](../../concepts/persistence-and-conversions.md)
- **Usage guide:** [KZDev.SystemClock.PrimeTime](../systemclock-package.md)
- **API:** [`IPrimeClock`](xref:KZDev.SystemClock.PrimeTime.IPrimeClock) · [`PrimeTimeScheduleZoneExtensions`](xref:KZDev.SystemClock.PrimeTime.PrimeTimeScheduleZoneExtensions)
