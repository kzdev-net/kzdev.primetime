# Release Notes: KZDev.PrimeTime.Testing

## Conventions
- Version heading format: `## Version <major>.<minor>.<patch>`.
- Each version section must include these categories in this order:
  - `### Added`
  - `### Changed`
  - `### Fixed`
  - `### Notes`
  - `### Package`
- Use `- None.` for categories without entries.
- In `### Package`, inline Markdown (bold, code) is allowed; NuGet pack strips it to plain text for the nuspec `releaseNotes` field. GitHub release bodies and DocFX keep full Markdown from these files.

## Version 0.0.7

### Added
- Internal **`ThrowHelper`** and **`TestingStrings`** catalog in **`KZDev.PrimeTime.Testing`**, with **`KZDev.SystemClock.PrimeTime.Testing`** linking the canonical implementation.
- **`PrimeTestClock`** **`ClockEvents`** discriminated event model, bounded **`RunFor`**, deadline-driven automatic runner, persist-on-read virtual time, and stricter backward virtual-time rules.
- In-repo contract coverage (**`UsingThrowHelper`**, shared **`ThrowHelperContractMessages`**) asserting exception types and resx-backed messages for overflow, backward-time guards, and start run-rate validation.

### Changed
- Migratable **`PrimeTestClock`** inline throws now call **`ThrowHelper`**; runner stop join timeout preserves **`Exception.Data`** keys **`BlockedOperations`** and **`LikelyCause`**.
- Runner join failure throws format timeout seconds from **`TimeSpan.TotalSeconds`** with InvariantCulture **`g0`** (replacing **`(int)`** truncation at call sites).
- **`AddPrimeTestClock`** null guard message is **`The service collection cannot be null.`** (replacing the BCL default).
- Start run-rate **`ArgumentOutOfRangeException`** resx text uses **`100 milliseconds`** where inline code previously used **`100 ms`**.

### Fixed
- None.

### Notes
- Forward-march reconcile diagnostic throws remain inline in **`PrimeTestClock`** for a later phase.
- Contract tests reference expected messages via **`ThrowHelperContractMessages`** because both Testing assemblies embed **`TestingStrings`**.

### Package
- KZDev.PrimeTime.Testing v0.0.7 ships centralized testing exception messages, **`PrimeTestClock`** throw migration, and matching contract tests.

## Version 0.0.6

### Added
- DocFx **Persistence and conversions (testing)** example page referencing in-repo **`UsingPersistenceAndConversionExamples`** (virtual clock and schedule-zone assertions).

### Changed
- None.

### Fixed
- None.

### Notes
- Example sources live under `Source/Dev/Testing/KZDev.PrimeTime.Testing.Examples/`; API surface is unchanged aside from documentation cross-references.

### Package
- KZDev.PrimeTime.Testing v0.0.6 publishes updated package release notes aligned with persistence-and-conversions documentation and examples.

## Version 0.0.5

### Added
- Initial per-package release-notes baseline established for public release traceability.

### Changed
- None.

### Fixed
- None.

### Notes
- Baseline entry represents the currently shipped package state at version `0.0.5`.

### Package
- KZDev.PrimeTime.Testing package baseline release summary for version `0.0.5`.
