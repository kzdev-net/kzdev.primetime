# Release Notes: KZDev.SystemClock.PrimeTime

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
- Schedule-zone conversion extensions on **`IPrimeTime`** (**`PrimeTimeScheduleZoneExtensions`**) for **`DateTimeOffset`** and UTC **`DateTime`**, using **`IPrimeClock.LocalScheduleTimeZone`** (BCL **`DateOnly`**, **`TimeOnly`**, and wall **`DateTime`** helpers).
- DocFx concept article **Persistence and time conversions** and System Clock track example pages (production and testing) with runnable snippets.

### Changed
- None.

### Fixed
- None.

### Notes
- See `Source/Docs/articles/concepts/persistence-and-conversions.md` for persistence shapes and BCL-oriented schedule-zone projection.

### Package
- KZDev.SystemClock.PrimeTime v0.0.6 adds public BCL schedule-zone conversion helpers and matching documentation.

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
- KZDev.SystemClock.PrimeTime package baseline release summary for version `0.0.5`.
