# Local net481 testing

Test projects under `Source/Tst/` multi-target **`net10.0`**, **`net8.0`**, and **`net481`**. GitHub Actions CI on `ubuntu-latest` exercises **`net8.0`** and **`net10.0`** only; **`net481` is not run in CI**. Maintainers and contributors validate .NET Framework coverage on **Windows** before release.

## When to run net481 tests

- Before tagging a release when changes might affect `netstandard2.0` or Framework consumers.
- After edits to test or production code that compile differently on `net481`.
- When investigating a Framework-specific bug report.

## Prerequisites

- **Windows** with the .NET SDK used by this repository.
- A successful **build** of the test project (Debug or Release).

## Recommended runners

### Visual Studio or ReSharper

Open `Source/KZDev.PrimeTime.slnx` and run tests for the `net481` target framework. This is the most reliable path on Windows.

### xUnit v3 executable (command line)

Test projects use **xUnit v3** with `OutputType=Exe`. After build, invoke the in-process runner directly:

```powershell
dotnet build Source/Tst/<Project>/<Project>.csproj -c Debug
& "artifacts/bin/<Project>/debug_net481/<Project>.exe" -class "<FullyQualifiedTestClass>" -noColor
```

Examples:

```powershell
# All tests in a class
& "artifacts/bin/KZDev.PrimeTime.Testing.UnitTests/debug_net481/KZDev.PrimeTime.Testing.UnitTests.exe" `
  -class "KZDev.PrimeTime.Testing.UnitTests.UsingThrowHelper" -noColor

# Single method (wildcard supported)
& "artifacts/bin/KZDev.PrimeTime.Testing.UnitTests/debug_net481/KZDev.PrimeTime.Testing.UnitTests.exe" `
  -method "*UsingThrowHelper.ThrowInvalidOperation_RunnerStopJoinTimedOut*" -noColor
```

Use `release_net481` instead of `debug_net481` when validating Release builds.

## `dotnet test` on net481

`dotnet test` with `-f net481` can hang in some environments (VSTest protocol negotiation timeouts). If that happens, use the **`.exe` runner** or an IDE test host instead. Treat such timeouts as an environment limitation, not necessarily a product failure.

## Related documentation

- Consumer-facing supported-matrix summary: [README.md — Supported platforms and test coverage](../../README.md#supported-platforms-and-test-coverage)
- Support policy: [SUPPORT.md](../../SUPPORT.md)
