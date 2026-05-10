# "Now" surfaces — System Clock stack

The System Clock stack exposes BCL "now" projections on **`IPrimeClock`**: `DateTimeOffset`, `DateTime`, `TimeOnly`, and `DateOnly` for both local and UTC time.

[!code-csharp[](../../../../Dev/Production/KZDev.SystemClock.PrimeTime.Examples/Scenarios/NowSurfaceScenario.cs#Snippet)]

## What to notice

- **`LocalNowDateTimeOffset`** / **`UtcNowDateTimeOffset`** preserve UTC offset and are the recommended surfaces for time-zone-aware code.
- **`LocalNowDateTime`** returns a `DateTime` with `Kind = DateTimeKind.Local`; **`UtcNowDateTime`** returns `DateTimeKind.Utc`.
- **`LocalNowTimeOnly`** / **`LocalNowDateOnly`** are convenience projections built on the **`net8.0`** + **`net10.0`** target frameworks.
- The PrimeTime / NodaTime stack adds **`Instant`**, **`ZonedDateTime`**, **`LocalTime`**, and **`LocalDate`** projections on top of these.

## Related

- **Usage guide:** [KZDev.SystemClock.PrimeTime](../systemclock-package.md)
- **Concepts:** [Choosing a package](../../concepts/choosing-a-package.md)
- **API:** [`IPrimeClock`](xref:KZDev.SystemClock.PrimeTime.IPrimeClock)
