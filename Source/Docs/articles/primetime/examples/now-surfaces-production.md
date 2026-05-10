# "Now" surfaces — PrimeTime (NodaTime) stack

The PrimeTime (NodaTime) stack adds NodaTime-first "now" projections on **`IPrimeClock`**: **`Instant`**, **`ZonedDateTime`**, **`LocalTime`**, and **`LocalDate`** — alongside the BCL surfaces from the System Clock subset.

[!code-csharp[](../../../../Dev/Production/KZDev.PrimeTime.Examples/Scenarios/NowSurfaceScenario.cs#Snippet)]

## What to notice

- **`NowInstant`** is the canonical NodaTime UTC instant; it is identical to **`UtcNowInstant`** and is the simplest "now" projection.
- **`LocalNowInstant`** is the same instant viewed in the configured local zone; **`LocalZonedNowInstant`** returns a full **`ZonedDateTime`** that carries zone and offset.
- **`LocalNowTime`** / **`LocalNowDate`** return NodaTime **`LocalTime`** and **`LocalDate`**, useful when scheduling against wall-clock components.
- BCL projections like **`LocalNowDateTimeOffset`** remain available from the shared contract for interop with code that speaks BCL types.

## Related

- **Usage guide:** [KZDev.PrimeTime](../primetime-superset.md)
- **Concepts:** [Choosing a package](../../concepts/choosing-a-package.md)
- **API:** [`IPrimeClock`](xref:KZDev.PrimeTime.IPrimeClock)
