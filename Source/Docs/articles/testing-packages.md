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

## API links

- [API Reference](xref:PrimeTime)
- [Choosing a package](choosing-a-package.md)
- [Timers, daylight saving, and testing](concepts-timers-and-testing.md)
