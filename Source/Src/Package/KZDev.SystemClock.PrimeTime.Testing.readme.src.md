# KZDev.SystemClock.PrimeTime.Testing

**KZDev.SystemClock.PrimeTime.Testing** provides deterministic virtual-time testing APIs for applications that use **`KZDev.SystemClock.PrimeTime`**.

Use the namespace **`KZDev.SystemClock.PrimeTime`**.

{{REQUIREMENTS}}
## Installation

Install both packages:

```bash
dotnet add package KZDev.SystemClock.PrimeTime
dotnet add package KZDev.SystemClock.PrimeTime.Testing
```

## What this package provides

- `IPrimeTestClock`
- `IPrimeTestTime`
- `PrimeTestClock`
- Test-focused DI registration helpers

## Quick start (dependency injection)

```csharp
using KZDev.SystemClock.PrimeTime;
using Microsoft.Extensions.DependencyInjection;

IServiceCollection services = new ServiceCollection();
services.AddPrimeTestClock();
```

## Documentation and source

- **[Product documentation](https://kzdev-net.github.io/kzdev.primetime/)** (includes testing package guidance).
- **Source and issues:** [github.com/kzdev-net/kzdev.primetime](https://github.com/kzdev-net/kzdev.primetime)
- **License:** MIT
