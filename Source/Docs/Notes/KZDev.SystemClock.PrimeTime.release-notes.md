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

## Version 1.0.0

### Added
- First public release of the **BCL foundation** production package: injectable **`IPrimeClock`** / **`PrimeClock`** backed by **`TimeProvider`** and BCL date/time types (`DateTimeOffset`, `TimeSpan`, `DateOnly`, `TimeOnly` where applicable).
- **Interval** and **time-of-day** timers with explicit daylight-saving policy.
- Schedule-zone conversion extensions and BCL-oriented persistence helpers (`LocalScheduleTimeZone`, `PrimeTimeScheduleZoneExtensions`).
- Multi-target binaries: `net10.0`, `net8.0`, and `netstandard2.0`.

### Changed
- None.

### Fixed
- None.

### Notes
- Pair with **`KZDev.SystemClock.PrimeTime.Testing`** in test projects for deterministic virtual time.
- Documentation: [PrimeTime overview](https://github.com/kzdev-net/kzdev.primetime/blob/main/Source/Docs/articles/overview.md) and [choosing a package](https://github.com/kzdev-net/kzdev.primetime/blob/main/Source/Docs/articles/concepts/choosing-a-package.md).

### Package
- **KZDev.SystemClock.PrimeTime** v1.0.0 — testable clocks and timers with minimal BCL dependencies.
