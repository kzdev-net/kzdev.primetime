# Persistence and schedule-zone conversions — PrimeTime (NodaTime), testing

These tests use **`PrimeTestClock`** with a fixed **[`DateTimeZone`](xref:NodaTime.DateTimeZone)** so schedule-zone projections stay **deterministic**. They also assert **`NodaDurationBclConversion`** clamping for delays beyond **`TimeSpan`** range.

For the conceptual background, see [Persistence and time conversions](../../concepts/persistence-and-conversions.md).

[!code-csharp[](../../../../Dev/Testing/KZDev.PrimeTime.Testing.Examples/UsingPersistenceAndConversionExamples.cs#Snippet)]

## What to notice

- **`ToScheduleLocalDate`** matches **`Instant.InZone(zone).Date`** for the same zone the test clock uses.
- **`LocalTimeOfDay`** preserves wall **`LocalTime`** when round-tripping through **`PrimeTimeOfDayConversion`**.
- Delay **`TimeSpan`** conversion clamps to **`TimeSpan.MaxValue`** for durations larger than BCL can represent.

## Related

- **Concepts:** [Persistence and time conversions](../../concepts/persistence-and-conversions.md)
- **Cross-track:** [Test-clock control APIs](../../concepts/examples/test-clock-control-testing.md)
- **API:** [`IPrimeTestClock`](xref:KZDev.PrimeTime.Testing.IPrimeTestClock) · [`PrimeTimeScheduleZoneExtensions`](xref:KZDev.PrimeTime.PrimeTimeScheduleZoneExtensions)
