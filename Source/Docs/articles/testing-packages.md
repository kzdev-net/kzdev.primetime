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

## Installation

Install your production package and the matching testing package:

```bash
dotnet add package KZDev.PrimeTime
dotnet add package KZDev.PrimeTime.Testing
```

```bash
dotnet add package KZDev.SystemClock.PrimeTime
dotnet add package KZDev.SystemClock.PrimeTime.Testing
```

## Dependency injection for tests

Both testing packages provide DI helpers for test scenarios where you replace runtime clock services with virtual time.

- NodaTime production stack: use extension methods from `KZDev.PrimeTime.Testing`.
- BCL/SystemClock stack: use extension methods from `KZDev.SystemClock.PrimeTime.Testing`.

## Runnable testing examples

Use the in-repo example test projects for copy/paste-friendly scenarios:

- NodaTime stack: `Source/Dev/Testing/KZDev.PrimeTime.Testing.Examples`
- BCL/SystemClock stack: `Source/Dev/Testing/KZDev.SystemClock.PrimeTime.Testing.Examples`

Each project demonstrates:

- Test clock control APIs (`Set*`, `Advance`, `RunFor`, `Start`/`Stop`)
- Deterministic interval timers (synchronous and asynchronous callbacks)
- DI replacement with `AddPrimeTestClock`
- Daylight saving scenarios

## Daylight saving guidance for tests

Both testing example projects include DST-focused scenarios, but with different emphasis:

- `KZDev.PrimeTime.Testing.Examples` uses deterministic zone-based setup (e.g. fixed TZDB zones) and optionally probes the local environment.
- `KZDev.SystemClock.PrimeTime.Testing.Examples` uses environment-aware local probes plus virtual-time advancement patterns.

These patterns keep tests stable across machines while still showing realistic local-time behavior.

## API links

- **API:** [System Clock stack](xref:KZDev.SystemClock.PrimeTime.Testing) / [PrimeTime / NodaTime stack](xref:KZDev.PrimeTime.Testing) (testing assemblies match each production stack)
- [Choosing a package](concepts/choosing-a-package.md)
- [Timers, daylight saving, and testing](concepts/concepts-timers-and-testing.md)
