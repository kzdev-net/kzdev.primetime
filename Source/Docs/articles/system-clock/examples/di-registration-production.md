# DI registration — System Clock stack

Confirms what **`AddPrimeClock`** registers in a Microsoft DI container for the `KZDev.SystemClock.PrimeTime` package and that **`IPrimeClock`** and **`IPrimeTime`** resolve to the same singleton instance.

[!code-csharp[](../../../../Dev/Production/KZDev.SystemClock.PrimeTime.Examples/Scenarios/DiRegistrationScenario.cs#Snippet)]

## What to notice

- **`AddPrimeClock`** is idempotent for **`TimeProvider`** — if you already registered one (for example a fake), `AddPrimeClock` will not overwrite it.
- **`IPrimeTime`** is registered as a factory that returns the same singleton resolved for **`IPrimeClock`** — both abstractions share state.
- **`TimeProvider`** is the BCL clock surface this stack consumes; tests can inject a custom `TimeProvider` (or use **`PrimeTestClock`**) to control time.

## Related

- [DI replacement with `AddPrimeTestClock`](../../concepts/examples/di-replacement-testing.md) — how tests substitute virtual time via DI.
- **Concepts:** [Choosing a package](../../concepts/choosing-a-package.md)
- **Usage guide:** [KZDev.SystemClock.PrimeTime](../systemclock-package.md)
- **API:** [`IPrimeClock`](xref:KZDev.SystemClock.PrimeTime.IPrimeClock) · [`IPrimeTime`](xref:KZDev.SystemClock.PrimeTime.IPrimeTime)
