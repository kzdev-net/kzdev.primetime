# Time-of-day and DST (production) — System Clock stack

This page combines three runnable demos that all touch local wall-clock time on the **System Clock** stack:

1. **Time-of-day timers** — register a callback for a future local **`TimeOnly`**.
2. **Environment-aware DST** — probe the local **`TimeZoneInfo`** for spring-forward (invalid) and fall-back (ambiguous) wall-clock samples.
3. **Local schedule zone** — read the timezone the clock uses for local scheduling decisions.

## Time-of-day timers

Schedule a one-shot callback for a future local time-of-day. The example registers both a synchronous and an asynchronous callback, then waits for both to fire.

[!code-csharp[](../../../../Dev/Production/KZDev.SystemClock.PrimeTime.Examples/Scenarios/TimeOfDayTimerScenario.cs#Snippet)]

**What to notice:**

- **`LocalTimeOfDay`** wraps a **`TimeOnly`** target. Use **`UtcTimeOfDay`** if you want UTC-anchored scheduling instead.
- **`RegisterTimeOfDay`** / **`RegisterAsyncTimeOfDay`** return an **`IClockDayTimeTimer`**; dispose it to stop pending callbacks.
- Skipped/duplicate-time behavior (DST gaps and overlaps) is configured on **`DayTimeTimerOptions`**, omitted here for brevity.

## Environment-aware DST

This demo probes **`TimeZoneInfo.Local`** for representative invalid (spring-forward gap) and ambiguous (fall-back overlap) calendar samples and prints what the BCL reports for each.

[!code-csharp[](../../../../Dev/Production/KZDev.SystemClock.PrimeTime.Examples/Scenarios/EnvironmentAwareDstScenario.cs#Snippet)]

**What to notice:**

- The example only runs the deeper checks when **`TimeZoneInfo.Local.SupportsDaylightSavingTime`** is `true` — many CI containers report `false`.
- **`IsInvalidTime`** returns `true` for clock values inside a spring-forward gap (those calendar values do not exist in the local zone).
- **`IsAmbiguousTime`** + **`GetAmbiguousTimeOffsets`** describe fall-back overlaps where the same calendar value occurs at two distinct UTC offsets.

## Local schedule zone

Confirms the timezone **`IPrimeClock.LocalScheduleTimeZone`** uses for local wall-clock scheduling. Customizing this lets you decouple DST scheduling from the host's `TimeZoneInfo.Local`.

[!code-csharp[](../../../../Dev/Production/KZDev.SystemClock.PrimeTime.Examples/Scenarios/DstScenario.cs#Snippet)]

**What to notice:**

- **`LocalScheduleTimeZone`** is the zone used to interpret local time-of-day registrations.
- This member is **specific** to the System Clock stack. The PrimeTime / NodaTime stack provides a NodaTime-based equivalent via the **`PrimeTestClock`** zone constructor and **`DayTimeTimerOptions`**.

## Related

- [Time-of-day and DST (testing)](time-of-day-and-dst-testing.md) — DST behavior under a virtual clock.
- **Concepts:** [Timers, daylight saving, and testing](../../concepts/concepts-timers-and-testing.md)
- **API:** [`IPrimeClock`](xref:KZDev.SystemClock.PrimeTime.IPrimeClock) · [`DayTimeTimerOptions`](xref:KZDev.SystemClock.PrimeTime.DayTimeTimerOptions) · [`IClockDayTimeTimer`](xref:KZDev.SystemClock.PrimeTime.IClockDayTimeTimer)
