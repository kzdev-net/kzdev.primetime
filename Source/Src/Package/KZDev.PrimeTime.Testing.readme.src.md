# KZDev.PrimeTime.Testing

**KZDev.PrimeTime.Testing** provides deterministic virtual-time testing APIs for applications that use **`KZDev.PrimeTime`**.

Use the namespace **`KZDev.PrimeTime`**.

{{REQUIREMENTS}}
## Installation

```bash
dotnet add package KZDev.PrimeTime.Testing
```

## What this package provides

- `IPrimeTestClock`
- `IPrimeTestTime`
- `PrimeTestClock`
- Test-focused DI registration helpers

## Quick start (dependency injection)

```csharp
using KZDev.PrimeTime;
using Microsoft.Extensions.DependencyInjection;

IServiceCollection services = new ServiceCollection();
services.AddPrimeTestClock();
```

## Documentation and source

- **[Product documentation](https://kzdev-net.github.io/kzdev.primetime/)** (includes testing package guidance).
- **Source and issues:** [github.com/kzdev-net/kzdev.primetime](https://github.com/kzdev-net/kzdev.primetime)
- **License:** MIT
