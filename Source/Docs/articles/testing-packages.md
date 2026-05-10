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

Browse the dedicated example pages:

**System Clock-specific examples**

- [Interval timer (testing)](system-clock/examples/interval-timer-testing.md) — deterministic interval timers under `PrimeTestClock` with `Advance`.
- [Time-of-day and DST (testing)](system-clock/examples/time-of-day-and-dst-testing.md) — environment-aware DST probes plus stable virtual-clock advancement.

**Cross-track testing examples** (both stacks shown side-by-side)

- [Test-clock control APIs](concepts/examples/test-clock-control-testing.md) — `Set*`, `Advance`, `RunFor`, `Start`/`Stop` reference patterns.
- [DI replacement with `AddPrimeTestClock`](concepts/examples/di-replacement-testing.md) — substitute virtual time in a Microsoft DI container.

### Daylight saving notes

The System Clock testing examples use environment-aware local probes plus virtual-time advancement patterns; the DST page above describes the shape and the host-dependent fallbacks.

Browse from the **System Clock track** sidebar (**Testing API** → **`api/system-clock/`**), or open [KZDev.SystemClock.PrimeTime.Testing](xref:KZDev.SystemClock.PrimeTime.Testing) directly.

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

Browse the dedicated example pages:

**PrimeTime-specific examples**

- [Interval timer (testing)](primetime/examples/interval-timer-testing.md) — deterministic interval timers under `PrimeTestClock` with `Advance`.
- [Time-of-day and DST (testing)](primetime/examples/time-of-day-and-dst-testing.md) — deterministic spring-forward and fall-back examples against a fixed NodaTime `DateTimeZone`.

**Cross-track testing examples** (both stacks shown side-by-side)

- [Test-clock control APIs](concepts/examples/test-clock-control-testing.md) — `Set*`, `Advance`, `RunFor`, `Start`/`Stop` reference patterns.
- [DI replacement with `AddPrimeTestClock`](concepts/examples/di-replacement-testing.md) — substitute virtual time in a Microsoft DI container.

### Daylight saving notes

The PrimeTime testing examples use deterministic zone-based setup (fixed TZDB zones via the NodaTime `DateTimeZone` constructor of `PrimeTestClock`) and optionally probe the local environment.

Browse from the **PrimeTime track** sidebar (**Testing API** → **`api/primetime/`**), or open [KZDev.PrimeTime.Testing](xref:KZDev.PrimeTime.Testing) directly.

## Related conceptual docs

- [Choosing a package](concepts/choosing-a-package.md)
- [Timers, daylight saving, and testing](concepts/concepts-timers-and-testing.md)
