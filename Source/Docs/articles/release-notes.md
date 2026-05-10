# KZDev PrimeTime 0.0.6

Aggregated per-package release notes for this version (source: `Source/Docs/Notes/*.release-notes.md`).

## KZDev.PrimeTime

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

## KZDev.SystemClock.PrimeTime

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

## KZDev.PrimeTime.Testing

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

## KZDev.SystemClock.PrimeTime.Testing

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
