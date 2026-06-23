# KZDev PrimeTime 1.0.0

Aggregated per-package release notes for this version (source: `Source/Docs/Notes/*.release-notes.md`).

## KZDev.PrimeTime

## Version 1.0.0

### Added
- First public release of the **NodaTime superset** production package: injectable **`IPrimeClock`** / **`PrimeClock`** with BCL and NodaTime APIs (`Instant`, `Duration`, `LocalTime`, zoned projections).
- **Interval** and **time-of-day** timers with explicit daylight-saving policy.
- Schedule-zone conversion extensions, persistence helpers, and NodaTime/BCL interop utilities (`LocalScheduleDateTimeZone`, `PrimeTimeScheduleZoneExtensions`, and related conversion types).
- Multi-target binaries: `net10.0`, `net8.0`, and `netstandard2.0`.

### Changed
- None.

### Fixed
- None.

### Notes
- Pair with **`KZDev.PrimeTime.Testing`** in test projects for deterministic virtual time.
- Documentation: [PrimeTime overview](https://github.com/kzdev-net/kzdev.primetime/blob/main/Source/Docs/articles/overview.md) and [choosing a package](https://github.com/kzdev-net/kzdev.primetime/blob/main/Source/Docs/articles/concepts/choosing-a-package.md).

### Package
- **KZDev.PrimeTime** v1.0.0 — testable clocks and timers with full NodaTime support.

## KZDev.SystemClock.PrimeTime

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

## KZDev.PrimeTime.Testing

## Version 1.0.0

### Added
- First public release of the **NodaTime-track** testing package: **`IPrimeTestClock`** / **`PrimeTestClock`** for deterministic virtual time (`Instant`, `Duration`, and related NodaTime types).
- **`ClockEvents`** discriminated event model, bounded **`RunFor`**, deadline-driven automatic runner, and persist-on-read virtual time.
- **`AddPrimeTestClock`** dependency-injection helpers for substituting virtual clocks in tests.
- Multi-target binaries: `net10.0`, `net8.0`, and `netstandard2.0`.

### Changed
- None.

### Fixed
- None.

### Notes
- Pair with **`KZDev.PrimeTime`** in production code; do not reference both production packages in the same app.
- Documentation: [Testing packages](https://github.com/kzdev-net/kzdev.primetime/blob/main/Source/Docs/articles/testing-packages.md).

### Package
- **KZDev.PrimeTime.Testing** v1.0.0 — virtual clocks and deterministic timer testing for the NodaTime superset stack.

## KZDev.SystemClock.PrimeTime.Testing

## Version 1.0.0

### Added
- First public release of the **BCL-track** testing package: **`IPrimeTestClock`** / **`PrimeTestClock`** for deterministic virtual time using **`TimeProvider`** and BCL date/time types.
- Shared **`PrimeTestClock`** implementation with the NodaTime testing package (`ClockEvents`, bounded **`RunFor`**, automatic runner, persist-on-read virtual time).
- **`AddPrimeTestClock`** dependency-injection helpers for substituting virtual clocks in tests.
- Multi-target binaries: `net10.0`, `net8.0`, and `netstandard2.0`.

### Changed
- None.

### Fixed
- None.

### Notes
- Pair with **`KZDev.SystemClock.PrimeTime`** in production code; do not reference both production packages in the same app.
- Documentation: [Testing packages](https://github.com/kzdev-net/kzdev.primetime/blob/main/Source/Docs/articles/testing-packages.md).

### Package
- **KZDev.SystemClock.PrimeTime.Testing** v1.0.0 — virtual clocks and deterministic timer testing for the BCL stack.
