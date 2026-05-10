# DI replacement with `AddPrimeTestClock` (testing)

In tests you typically want application code to run unchanged but receive a **virtual** clock. Both PrimeTime testing packages add an **`AddPrimeTestClock`** extension method that registers a single **`PrimeTestClock`** as the implementation for **`IPrimeTestClock`**, **`IPrimeClock`**, and **`IPrimeTime`**, so any consumer of the regular abstractions resolves the same instance the test controls.

The two stacks differ only in the clock construction style and the "now" surfaces they verify:

- **System Clock stack** stays on **`DateTimeOffset`** and **`UtcNowDateTimeOffset`**.
- **PrimeTime (NodaTime) stack** uses **`Instant`** and **`NowInstant`**.

## System Clock stack

[!code-csharp[](../../../../Dev/Testing/KZDev.SystemClock.PrimeTime.Testing.Examples/UsingPrimeTestClockDependencyInjectionExamples.cs#Snippet)]

## PrimeTime (NodaTime) stack

[!code-csharp[](../../../../Dev/Testing/KZDev.PrimeTime.Testing.Examples/UsingPrimeTestClockDependencyInjectionExamples.cs#Snippet)]

## What to notice (both stacks)

- **`services.AddPrimeTestClock()`** registers one `PrimeTestClock` as the **singleton** implementation behind the test, production, and "time" abstractions. Calling **`testClock.SetTime(...)`** / **`testClock.SetInstant(...)`** is observed by every consumer that resolved `IPrimeClock`.
- The sample `*TimestampService` consumer accepts the **production** abstraction (**`IPrimeClock`**) — it is not test-aware. That is the property that lets production code stay unchanged while tests inject virtual time.
- After **`SetInstant`** / **`SetTime`**, every read of `NowInstant` / `UtcNowDateTimeOffset` returns the marker until the test advances the clock.

## Related

- [Test-clock control APIs](test-clock-control-testing.md) — `Set*`, `Advance`, `RunFor`, `Start` / `Stop` patterns shared by both stacks.
- **System Clock track:** [DI registration (production)](../../system-clock/examples/di-registration-production.md)
- **PrimeTime track:** [DI registration (production)](../../primetime/examples/di-registration-production.md)
- **API:** [`KZDev.SystemClock.PrimeTime.Testing`](xref:KZDev.SystemClock.PrimeTime.Testing) · [`KZDev.PrimeTime.Testing`](xref:KZDev.PrimeTime.Testing)
