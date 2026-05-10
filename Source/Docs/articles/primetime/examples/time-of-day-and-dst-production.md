# Time-of-day and DST (production) — PrimeTime (NodaTime) stack

This page combines three runnable demos that all touch local wall-clock time on the **PrimeTime** (NodaTime) stack:

1. **Time-of-day timers** — register a callback for a future local **`LocalTime`**.
2. **Environment-aware DST** — probe the local **`TimeZoneInfo`** for spring-forward (invalid) and fall-back (ambiguous) wall-clock samples.
3. **Local schedule zone** — read the timezone the clock uses for local scheduling decisions.

## Time-of-day timers

Schedule a one-shot callback for a future local **`LocalTime`**. The example registers both a synchronous and an asynchronous callback, then waits for both to fire.

[!code-csharp[](../../../../Dev/Production/KZDev.PrimeTime.Examples/Scenarios/TimeOfDayTimerScenario.cs#Snippet)]

**What to notice:**

- **`primeClock.LocalNowTime`** returns a NodaTime **`LocalTime`**; adding a **`Period`** moves the target into the future.
- **`RegisterTimeOfDay(LocalTime, …)`** is a NodaTime-typed overload supplied by the superset; **`UtcTimeOfDay`** equivalents are also available.
- Skipped/duplicate-time behavior (DST gaps and overlaps) is configured on **`DayTimeTimerOptions`**, omitted here for brevity.

## Environment-aware DST

This demo probes **`TimeZoneInfo.Local`** for representative invalid (spring-forward gap) and ambiguous (fall-back overlap) calendar samples and prints what the BCL reports for each. NodaTime is also available for richer zone math, but this scenario stays on BCL types so the output mirrors the System Clock variant on the same host.

[!code-csharp[](../../../../Dev/Production/KZDev.PrimeTime.Examples/Scenarios/EnvironmentAwareDstScenario.cs#Snippet)]

**What to notice:**

- The example only runs the deeper checks when **`TimeZoneInfo.Local.SupportsDaylightSavingTime`** is `true` — many CI containers report `false`.
- **`IsInvalidTime`** returns `true` for clock values inside a spring-forward gap (those calendar values do not exist in the local zone).
- **`IsAmbiguousTime`** + **`GetAmbiguousTimeOffsets`** describe fall-back overlaps where the same calendar value occurs at two distinct UTC offsets.

## Local schedule zone

Confirms the timezone **`IPrimeClock.LocalScheduleTimeZone`** uses for local wall-clock scheduling. Customizing this lets you decouple DST scheduling from the host's `TimeZoneInfo.Local`.

[!code-csharp[](../../../../Dev/Production/KZDev.PrimeTime.Examples/Scenarios/DstScenario.cs#Snippet)]

**What to notice:**

- **`LocalScheduleTimeZone`** is the BCL view of the zone used for local time-of-day registrations.
- For deterministic NodaTime-zone tests, see the testing variant of this page — `PrimeTestClock` accepts a NodaTime **`DateTimeZone`** directly.

## Related

- [Time-of-day and DST (testing)](time-of-day-and-dst-testing.md) — DST behavior under a virtual clock.
- **Concepts:** [Timers, daylight saving, and testing](../../concepts/concepts-timers-and-testing.md)
- **API:** [`IPrimeClock`](xref:KZDev.PrimeTime.IPrimeClock) · [`DayTimeTimerOptions`](xref:KZDev.PrimeTime.DayTimeTimerOptions) · [`IClockDayTimeTimer`](xref:KZDev.PrimeTime.IClockDayTimeTimer)
