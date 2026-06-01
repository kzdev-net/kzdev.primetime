# KZDev.PrimeTime.Testing

**KZDev.PrimeTime.Testing** provides deterministic virtual-time testing APIs for applications that use **`KZDev.PrimeTime`**.

Use the namespace **`KZDev.PrimeTime`**.

## Requirements

- **Target frameworks:** `net10.0`, `net8.0`, and `netstandard2.0`.
- **Pair with `KZDev.PrimeTime`:** Pin the **same NuGet version** on both packages—the same `Version` on each `PackageReference`, or the same version in both `dotnet add package` commands (for example `1.2.3` on `KZDev.PrimeTime` and `1.2.3` on `KZDev.PrimeTime.Testing`). Mismatched versions are unsupported.
- **Direct package dependencies (all target frameworks):** `Microsoft.Extensions.DependencyInjection` and `NodaTime` are declared on **`KZDev.PrimeTime.Testing`** itself and appear in the package dependency graph; NuGet restores them when you install this package. You do **not** need your own `PackageReference` to either library unless your test code uses or references their types or APIs directly (for example `NodaTime.Instant` in a signature, generic argument, or property type); if you do, add `NodaTime` explicitly and align its version with the one resolved for `KZDev.PrimeTime` / `KZDev.PrimeTime.Testing` (check your lock file or central package management versions).
- **`netstandard2.0` polyfills:** `Microsoft.Bcl.AsyncInterfaces`, `Microsoft.Bcl.TimeProvider`, and `System.Threading.Tasks.Extensions`. NuGet restores these only when your project targets `netstandard2.0`; they are not used on `net10.0` or `net8.0`.

## Installation

Install both packages:

```bash
dotnet add package KZDev.PrimeTime
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
