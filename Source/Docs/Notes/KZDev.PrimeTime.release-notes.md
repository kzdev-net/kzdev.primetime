# Release Notes: KZDev.PrimeTime

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
- Schedule-zone conversion extensions on **`IPrimeTime`** (**`PrimeTimeScheduleZoneExtensions`**) for **`Instant`** and **`ZonedDateTime`**, using **`IPrimeClock.LocalScheduleDateTimeZone`**.
- **`PrimeTimeOfDayConversion`**, **`NodaDurationBclConversion`**, and **`NodaDateTimeZoneBclConversion`** for NodaTime/BCL interop where PrimeTime encodes non-obvious policy (for example delay **`TimeSpan`** clamping).
- **`IPrimeClock.LocalScheduleDateTimeZone`** (**`DateTimeZone`**) alongside the existing BCL schedule zone.
- DocFx concept article **Persistence and time conversions** and PrimeTime track example pages (production and testing) with runnable snippets.

### Changed
- None.

### Fixed
- None.

### Notes
- See `Source/Docs/articles/concepts/persistence-and-conversions.md` for persistence shapes and when to use these helpers versus raw NodaTime APIs.

### Package
- KZDev.PrimeTime v0.0.6 adds public conversion helpers, **`LocalScheduleDateTimeZone`**, and documentation for persistence and schedule-zone projections.

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
- KZDev.PrimeTime package baseline release summary for version `0.0.5`.
