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
