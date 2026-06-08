# KZDev PrimeTime 0.0.7

Aggregated per-package release notes for this version (source: `Source/Docs/Notes/*.release-notes.md`).

## KZDev.PrimeTime

## Version 0.0.7

### Added
- Internal **`ThrowHelper`** and **`ProductionStrings`** resource catalog for centralized production exception messages (timers, schedule-zone validation, and related guard paths).

### Changed
- Production inline throws in shared clock/timer code now call **`ThrowHelper`**; user-visible exception **types** are unchanged and **messages** are sourced from **`ProductionStrings`** (wording largely preserved, now centralized for consistency and future localization).

### Fixed
- None.

### Notes
- **`ThrowHelper`** remains **`internal`**; no public member signatures changed for this migration.

### Package
- KZDev.PrimeTime v0.0.7 centralizes production exception messages via **`ThrowHelper`** and **`ProductionStrings`**.

## KZDev.SystemClock.PrimeTime

## Version 0.0.7

### Added
- Internal **`ThrowHelper`** and **`ProductionStrings`** resource catalog for centralized production exception messages (timers, schedule-zone validation, and related guard paths).

### Changed
- Production inline throws in shared clock/timer code now call **`ThrowHelper`**; user-visible exception **types** are unchanged and **messages** are sourced from **`ProductionStrings`** (wording largely preserved, now centralized for consistency and future localization).

### Fixed
- None.

### Notes
- **`ThrowHelper`** remains **`internal`**; no public member signatures changed for this migration.

### Package
- KZDev.SystemClock.PrimeTime v0.0.7 centralizes production exception messages via **`ThrowHelper`** and **`ProductionStrings`**.

## KZDev.PrimeTime.Testing

## Version 0.0.7

### Added
- Internal **`ThrowHelper`** and **`TestingStrings`** catalog (ten localized messages) in **`KZDev.PrimeTime.Testing`**, with **`KZDev.SystemClock.PrimeTime.Testing`** linking the canonical implementation.
- **`PrimeTestClock`** **`ClockEvents`** discriminated event model, bounded **`RunFor`**, deadline-driven automatic runner, persist-on-read virtual time, and stricter backward virtual-time rules.
- In-repo contract coverage (**`UsingThrowHelper`**, shared **`ThrowHelperContractMessages`**) asserting exception types and resx-backed messages for overflow, backward-time guards, and start run-rate validation.

### Changed
- Migratable **`PrimeTestClock`** inline throws (inventory T-01 through T-11) now call **`ThrowHelper`**; runner stop join timeout preserves **`Exception.Data`** keys **`BlockedOperations`** and **`LikelyCause`**. DI **`AddPrimeTestClock`** null guards remain idiomatic BCL **`ArgumentNullException`** at the call site (T-12/T-13 exempt).
- Runner join failure throws format timeout seconds from **`TimeSpan.TotalSeconds`** with InvariantCulture **`g0`** (replacing **`(int)`** truncation at call sites).
- Start run-rate **`ArgumentOutOfRangeException`** resx text uses **`100 milliseconds`** where inline code previously used **`100 ms`**.

### Fixed
- None.

### Notes
- Forward-march reconcile diagnostic throws (deferred D-01) remain inline in **`PrimeTestClock`** for a later phase.
- Contract tests reference expected messages via **`ThrowHelperContractMessages`** because both Testing assemblies embed **`TestingStrings`**.

### Package
- KZDev.PrimeTime.Testing v0.0.7 ships centralized testing exception messages, **`PrimeTestClock`** throw migration, and matching contract tests.

## KZDev.SystemClock.PrimeTime.Testing

## Version 0.0.7

### Added
- Linked canonical **`ThrowHelper`** and **`TestingStrings`** from **`KZDev.PrimeTime.Testing`** (same eleven-message catalog and shared **`PrimeTestClock`** throw migration as the NodaTime testing package).
- In-repo contract coverage (**`UsingThrowHelper`** via linked tests, shared **`ThrowHelperContractMessages`**) asserting exception types and resx-backed messages on representative **`PrimeTestClock`** paths.

### Changed
- Shared **`PrimeTestClock`** migratable throws now call **`ThrowHelper`**; runner stop join timeout preserves **`Exception.Data`** keys **`BlockedOperations`** and **`LikelyCause`**.
- Runner join failure throws format timeout seconds from **`TimeSpan.TotalSeconds`** with InvariantCulture **`g0`**.
- **`AddPrimeTestClock`** null guard message is **`The service collection cannot be null.`**
- Start run-rate **`ArgumentOutOfRangeException`** resx text uses **`100 milliseconds`** where inline code previously used **`100 ms`**.

### Fixed
- None.

### Notes
- BCL-track testing shares the same shared partial **`PrimeTestClock`** sources and linked **`ThrowHelper`** as **`KZDev.PrimeTime.Testing`**.

### Package
- KZDev.SystemClock.PrimeTime.Testing v0.0.7 aligns with the NodaTime testing package on centralized throws and contract-tested exception messages.