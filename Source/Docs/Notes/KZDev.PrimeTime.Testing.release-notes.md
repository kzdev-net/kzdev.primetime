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
