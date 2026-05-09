# PrimeTime testing packages

PrimeTime ships testing APIs in dedicated packages so production packages can stay focused on runtime clock/timer usage.

## Testing package mapping

Choose the testing package that matches your production package:

| Production package | Testing package | Namespace |
|--------------------|-----------------|-----------|
| `KZDev.PrimeTime` | `KZDev.PrimeTime.Testing` | `KZDev.PrimeTime` |
| `KZDev.SystemClock.PrimeTime` | `KZDev.SystemClock.PrimeTime.Testing` | `KZDev.SystemClock.PrimeTime` |

The testing packages keep the same namespace family as their production counterparts.

## Testing APIs in these packages

Testing-focused contracts and implementations are delivered via the testing packages, including:

- `IPrimeTestClock`
- `IPrimeTestTime`
- `PrimeTestClock`
- `PrimeTestTimeBase`

## System Clock stack (BCL / TimeProvider) {#system-clock-stack}

Use **`KZDev.SystemClock.PrimeTime`** with **`KZDev.SystemClock.PrimeTime.Testing`** when your production code stays on **`TimeProvider`** and **BCL** date/time types.

### Installation

```bash
dotnet add package KZDev.SystemClock.PrimeTime
dotnet add package KZDev.SystemClock.PrimeTime.Testing
```

### Dependency injection for tests

Use extension methods from **`KZDev.SystemClock.PrimeTime.Testing`** to replace runtime clock services with virtual time in tests.

### Runnable testing examples

Use the in-repo example project `Source/Dev/Testing/KZDev.SystemClock.PrimeTime.Testing.Examples` for copy/paste-friendly scenarios. It demonstrates:

- Test clock control APIs (`Set*`, `Advance`, `RunFor`, `Start`/`Stop`)
- Deterministic interval timers (synchronous and asynchronous callbacks)
- DI replacement with `AddPrimeTestClock`
- Daylight saving scenarios

### Daylight saving notes

`KZDev.SystemClock.PrimeTime.Testing.Examples` uses environment-aware local probes plus virtual-time advancement patterns.

Browse the generated testing assembly docs in the **Testing API** section of the sidebar, or start from [KZDev.SystemClock.PrimeTime.Testing](xref:KZDev.SystemClock.PrimeTime.Testing).

## PrimeTime stack (NodaTime) {#primetime-stack}

Use **`KZDev.PrimeTime`** with **`KZDev.PrimeTime.Testing`** when your production code uses **NodaTime** (`Instant`, `Duration`, `LocalTime`, etc.) on **`IPrimeClock`**.

### Installation

```bash
dotnet add package KZDev.PrimeTime
dotnet add package KZDev.PrimeTime.Testing
```

### Dependency injection for tests

Use extension methods from **`KZDev.PrimeTime.Testing`** to replace runtime clock services with virtual time in tests.

### Runnable testing examples

Use the in-repo example project `Source/Dev/Testing/KZDev.PrimeTime.Testing.Examples` for copy/paste-friendly scenarios. It demonstrates:

- Test clock control APIs (`Set*`, `Advance`, `RunFor`, `Start`/`Stop`)
- Deterministic interval timers (synchronous and asynchronous callbacks)
- DI replacement with `AddPrimeTestClock`
- Daylight saving scenarios

### Daylight saving notes

`KZDev.PrimeTime.Testing.Examples` uses deterministic zone-based setup (for example, fixed TZDB zones) and optionally probes the local environment.

Browse the generated testing assembly docs in the **Testing API** section of the sidebar, or start from [KZDev.PrimeTime.Testing](xref:KZDev.PrimeTime.Testing).

## Related conceptual docs

- [Choosing a package](concepts/choosing-a-package.md)
- [Timers, daylight saving, and testing](concepts/concepts-timers-and-testing.md)
