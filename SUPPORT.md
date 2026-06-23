# Support and lifecycle

## Support commitment

This project is maintained on a **best-effort** basis. There is **no SLA**, guaranteed response time, or commercial support obligation.

## How to get help

- **Bug reports and small feature ideas:** GitHub **Issues** on this repository.
- **Questions and usage discussion:** GitHub **Discussions**.

Responses depend on maintainer availability.

## Versioning and compatibility

Packages follow **Semantic Versioning** (SemVer). Breaking API changes are intended to arrive only in **major** version bumps; **minor** and **patch** releases should remain compatible for consumers following normal semver expectations. Consult each package’s release notes on NuGet and in this repository for shipped changes.

## Supported platforms and test coverage

All four packages ship **`net10.0`**, **`net8.0`**, and **`netstandard2.0`** binaries. GitHub Actions CI on `ubuntu-latest` runs the test suite on **`net8.0`** and **`net10.0`** only.

**.NET Framework consumers** use the `netstandard2.0` assemblies (for example on .NET Framework 4.8.1). Repository test projects also target **`net481`**, but that TFM is **not** run in CI; net481 validation is **local and best-effort on Windows**. Do not assume full automated Framework regression coverage on every change.

For the full matrix and how to run net481 tests locally, see [Supported platforms and test coverage](README.md#supported-platforms-and-test-coverage) in [README.md](README.md) and [Local net481 testing](Source/Reference/local-net481-testing.md).

## Release support boundaries

- **Current line:** The latest **major.minor** published on NuGet for a given package is the primary focus for fixes on a best-effort basis.
- **Older lines:** Older major or minor lines may receive fixes only when practical; migrate to the latest release when you can.
- **Prereleases:** Prerelease versions are unstable by definition; do not assume long-term support.

## Maintenance expectations

Release cadence is not fixed. New versions ship when there is sufficient value or need.

## Security

Report security vulnerabilities according to [SECURITY.md](SECURITY.md).

## Contributing

External pull requests are not accepted. See [CONTRIBUTING.md](CONTRIBUTING.md) and [README.md](README.md) for feedback channels and repository norms.
