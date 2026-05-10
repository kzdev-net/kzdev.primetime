# DI registration — PrimeTime (NodaTime) stack

Confirms what **`AddPrimeClock`** registers in a Microsoft DI container for the `KZDev.PrimeTime` package and that **`IPrimeClock`**, **`IPrimeTime`**, and the NodaTime **`IClock`** are all available.

[!code-csharp[](../../../../Dev/Production/KZDev.PrimeTime.Examples/Scenarios/DiRegistrationScenario.cs#Snippet)]

## What to notice

- **`AddPrimeClock`** is idempotent for **`NodaTime.IClock`** — if you already registered one (for example a fake `IClock`), `AddPrimeClock` will not overwrite it.
- **`IPrimeTime`** is registered as a factory that returns the same singleton resolved for **`IPrimeClock`** — both abstractions share state.
- **`NodaTime.IClock`** is the upstream NodaTime clock surface that the PrimeTime stack consumes; tests can inject a custom `IClock` (or use **`PrimeTestClock`**) to control time.

## Related

- [DI replacement with `AddPrimeTestClock`](../../concepts/examples/di-replacement-testing.md) — how tests substitute virtual time via DI.
- **Concepts:** [Choosing a package](../../concepts/choosing-a-package.md)
- **Usage guide:** [KZDev.PrimeTime](../primetime-superset.md)
- **API:** [`IPrimeClock`](xref:KZDev.PrimeTime.IPrimeClock) · [`IPrimeTime`](xref:KZDev.PrimeTime.IPrimeTime)
