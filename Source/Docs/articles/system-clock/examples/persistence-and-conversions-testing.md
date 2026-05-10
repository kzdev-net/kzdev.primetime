# Persistence and schedule-zone conversions — System Clock, testing

These tests compare **`IPrimeTime`** schedule-zone projections against **`TimeZoneInfo.ConvertTime`** and verify **`DateOnly`** / **`TimeOnly`** recombination matches the **wall** **`DateTime`** from **`ToScheduleDateTimeOffset`**.

For the conceptual background, see [Persistence and time conversions](../../concepts/persistence-and-conversions.md).

[!code-csharp[](../../../../Dev/Testing/KZDev.SystemClock.PrimeTime.Testing.Examples/UsingPersistenceAndConversionExamples.cs#Snippet)]

## What to notice

- **`ToScheduleDateTimeOffset(persistedUtc)`** matches **`TimeZoneInfo.ConvertTime(persistedUtc, clock.LocalScheduleTimeZone)`**.
- **`DateOnly`** + **`TimeOnly`** round-trip to the same **local wall** **`DateTime`** as the zoned offset’s **`DateTime`** property.

## Related

- **Concepts:** [Persistence and time conversions](../../concepts/persistence-and-conversions.md)
- **Cross-track:** [Test-clock control APIs](../../concepts/examples/test-clock-control-testing.md)
- **API:** [`IPrimeTestClock`](xref:KZDev.SystemClock.PrimeTime.Testing.IPrimeTestClock) · [`PrimeTimeScheduleZoneExtensions`](xref:KZDev.SystemClock.PrimeTime.PrimeTimeScheduleZoneExtensions)
