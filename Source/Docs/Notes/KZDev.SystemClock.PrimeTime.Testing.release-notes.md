# Release Notes: KZDev.SystemClock.PrimeTime.Testing

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

## Version 0.0.6

### Added
- DocFx **Persistence and conversions (testing)** example page referencing in-repo **`UsingPersistenceAndConversionExamples`** (BCL schedule-zone assertions with **`PrimeTestClock`**).

### Changed
- None.

### Fixed
- None.

### Notes
- Example sources live under `Source/Dev/Testing/KZDev.SystemClock.PrimeTime.Testing.Examples/`; API surface is unchanged aside from documentation cross-references.

### Package
- KZDev.SystemClock.PrimeTime.Testing v0.0.6 publishes updated package release notes aligned with persistence-and-conversions documentation and examples.

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
- KZDev.SystemClock.PrimeTime.Testing package baseline release summary for version `0.0.5`.
