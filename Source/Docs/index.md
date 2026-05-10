---
_layout: landing
---

# PrimeTime

**PrimeTime is a suite of packages that solves real-world issues when dealing with time in applications.** If production code directly calls **`DateTime.Now`**, **`DateTime.UtcNow`**, or **`TimeProvider.GetUtcNow()`** throughout the codebase, time-dependent behavior becomes **untestable**, **unpredictable near daylight-saving transitions**, and **impossible to control** in test scenarios. PrimeTime provides **injectable clock abstractions**, **timers with explicit scheduling semantics**, and **companion testing packages** that enable tests to run against **virtual, deterministic time** instead of the real wall clock.

## SystemClock Package (BCL Based)

**[KZDev.SystemClock.PrimeTime](https://www.nuget.org/packages/KZDev.SystemClock.PrimeTime)** is the foundational package built entirely on the .NET Base Class Library (BCL). It provides:

- **`IPrimeClock`** and **`PrimeClock`** implementations using **`TimeProvider`**
- Time operations using **`DateTimeOffset`** and **`TimeSpan`**
- **Injectable abstractions** that replace direct `DateTime.Now` calls
- **Dependency injection support** for .NET applications
- **Zero additional dependencies** beyond the BCL and Microsoft.Extensions.DependencyInjection

This package is ideal when you want testable time without adding NodaTime to your dependency tree.

### SystemClock Testing Package

**[KZDev.SystemClock.PrimeTime.Testing](https://www.nuget.org/packages/KZDev.SystemClock.PrimeTime.Testing)** complements the production package with:

- **`IPrimeTestClock`** and **`PrimeTestClock`** for controlling virtual time in tests
- **Deterministic timer execution** with manual time advancement
- **Test helpers** for DI setup and time-dependent assertions
- **Full compatibility** with xUnit, NUnit, MSTest, and other test frameworks

Together, these packages give you complete control over time in both production and test code—using only BCL types.

## PrimeTime Package (NodaTime Superset)

**[KZDev.PrimeTime](https://www.nuget.org/packages/KZDev.PrimeTime)** is the **superset package** that includes everything from the SystemClock package **plus comprehensive NodaTime support**. NodaTime provides:

- **Clearer time semantics** with `Instant`, `Duration`, `LocalTime`, `ZonedDateTime`, and `Period`
- **Robust handling** of time zones and daylight-saving transitions
- **Explicit disambiguation** of ambiguous or invalid local times
- **Type-safe operations** that prevent common datetime mistakes

The PrimeTime package extends the core abstractions with:

- **NodaTime-native overloads** for `IPrimeClock` methods returning `Instant`, `Duration`, etc.
- **Zoned "now" projections** for working with local time in specific time zones
- **Timer overloads** accepting NodaTime types
- **Seamless integration** with NodaTime's `IClock` while providing richer functionality

Choose this package when you need the **precision and safety** of NodaTime for complex time zone scenarios, scheduling, or calendar-aware operations.

### PrimeTime Testing Package (NodaTime Support)

**[KZDev.PrimeTime.Testing](https://www.nuget.org/packages/KZDev.PrimeTime.Testing)** is the testing companion for the NodaTime superset package, providing:

- **`IPrimeTestClock`** and **`PrimeTestClock`** with NodaTime type support
- **Virtual time control** using `Instant` and `Duration`
- **Time zone testing** with deterministic advancement
- **Full alignment** with NodaTime semantics and types

Use this package for test scenarios requiring NodaTime's advanced time handling capabilities.

## Choosing the Right Package

Both **KZDev.SystemClock.PrimeTime** and **KZDev.PrimeTime** share the **same core design** (identical service registration patterns, `IPrimeClock` interface shape, and conceptual model) but target **different type systems**. They are **mutually exclusive**—pick **exactly one production package** per application:

- Use **SystemClock.PrimeTime** when you prefer BCL types and want minimal dependencies
- Use **PrimeTime** when you need NodaTime's superior time zone handling, clarity, and type safety (includes all SystemClock functionality)

Each production package has a **matching testing package** aligned to the same type system. See **[Testing packages](articles/testing-packages.md)** for detailed guidance on virtual time, dependency injection patterns, and examples.

## Drop-in Compatibility with Existing Code

PrimeTime packages are designed to integrate seamlessly with existing .NET code:

- **TimeProvider Bridge**: Both packages include a `TimeProvider` bridge that allows `IPrimeClock` to serve as a **drop-in replacement** for existing code that uses the BCL `TimeProvider`. This means you can adopt PrimeTime incrementally without rewriting your entire codebase.

- **ITimer Interface Support**: The PrimeTime interval timer implements the standard **`System.Threading.ITimer`** BCL interface, ensuring compatibility with existing timer-based code and allowing for easy migration of timer logic to the more testable PrimeTime timer abstractions.

These compatibility features make it easy to modernize your time-handling code gradually while maintaining backward compatibility with existing patterns.

## Documentation

- [Overview](articles/overview.md)
- [Concepts](articles/concepts/index.md)
- [System Clock track](articles/system-clock/index.md)
- [PrimeTime (NodaTime Extended) track](articles/primetime/index.md)
- [Release notes (all packages)](articles/release-notes.md)
- **API:** [System Clock stack](xref:KZDev.SystemClock.PrimeTime) · [PrimeTime / NodaTime stack](xref:KZDev.PrimeTime)

Support expectations, lifecycle, and compatibility cadence are documented in the **[repository README](https://github.com/kzdev-net/kzdev.primetime/blob/main/README.md)** and **[SUPPORT](https://github.com/kzdev-net/kzdev.primetime/blob/main/SUPPORT.md)** files—not duplicated here.

## NuGet

- [KZDev.PrimeTime](https://www.nuget.org/packages/KZDev.PrimeTime)
- [KZDev.SystemClock.PrimeTime](https://www.nuget.org/packages/KZDev.SystemClock.PrimeTime)
- [KZDev.PrimeTime.Testing](https://www.nuget.org/packages/KZDev.PrimeTime.Testing)
- [KZDev.SystemClock.PrimeTime.Testing](https://www.nuget.org/packages/KZDev.SystemClock.PrimeTime.Testing)