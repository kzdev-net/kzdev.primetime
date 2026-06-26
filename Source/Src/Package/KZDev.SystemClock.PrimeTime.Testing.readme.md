# KZDev.SystemClock.PrimeTime.Testing

**KZDev.SystemClock.PrimeTime.Testing** provides deterministic virtual-time testing APIs for applications that use **`KZDev.SystemClock.PrimeTime`**.

Use the namespace **`KZDev.SystemClock.PrimeTime`**.

## Requirements

- **Target frameworks:** `net10.0`, `net8.0`, and `netstandard2.0`.
- **Pair with `KZDev.SystemClock.PrimeTime`:** Pin the **same NuGet version** on both packages—the same `Version` on each `PackageReference`, or the same version in both `dotnet add package` commands (for example `1.2.3` on `KZDev.SystemClock.PrimeTime` and `1.2.3` on `KZDev.SystemClock.PrimeTime.Testing`). Mismatched versions are unsupported.
- **Direct package dependencies (all target frameworks):** `Microsoft.Extensions.DependencyInjection` is declared on **`KZDev.SystemClock.PrimeTime.Testing`** itself and appears in the package dependency graph; NuGet restores it when you install this package. You do **not** need your own `PackageReference` unless your test code uses or references its types or APIs directly (check your lock file or central package management versions if you add one).
- **`netstandard2.0` polyfills:** `Microsoft.Bcl.AsyncInterfaces`, `Microsoft.Bcl.TimeProvider`, and `System.Threading.Tasks.Extensions`. NuGet restores these only when your project targets `netstandard2.0`; they are not used on `net10.0` or `net8.0`.

## Installation

```bash
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
