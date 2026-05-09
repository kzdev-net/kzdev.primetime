# Support and lifecycle

The canonical **support and lifecycle** policy lives in the repository as [`SUPPORT.md`](https://github.com/kzdev-net/kzdev.primetime/blob/main/SUPPORT.md). Open that file for the full text (including any future updates).

## At a glance

- **Support:** Best-effort maintenance; **no SLA** or guaranteed response time.
- **Channels:** GitHub **Issues** (bugs, small requests) and **Discussions** (questions).
- **Versions:** SemVer; breaking changes intended only on **major** bumps. Prefer the latest **major.minor** on NuGet for each package.
- **Older releases:** Fixes may not be backported; upgrade when practical.
- **Maintenance cadence:** Release cadence is not fixed and is driven by value/need.
- **Contributing:** External pull requests are not accepted; see [**CONTRIBUTING.md**](https://github.com/kzdev-net/kzdev.primetime/blob/main/CONTRIBUTING.md) and the repository **README**.
- **Security:** Report vulnerabilities per [**SECURITY.md**](https://github.com/kzdev-net/kzdev.primetime/blob/main/SECURITY.md).

## Package compatibility expectations

Compatibility should be evaluated per package line:

- **Production packages:** `KZDev.PrimeTime` and `KZDev.SystemClock.PrimeTime` are distinct stacks. Use one production package per application.
- **Testing packages:** `KZDev.PrimeTime.Testing` and `KZDev.SystemClock.PrimeTime.Testing` follow the same line-specific expectations as their corresponding production packages.
- **Line focus:** The latest published `major.minor` line for each package is the primary maintenance target.
- **Upgrade guidance:** If you are on an older line, expect best-effort fixes only and prefer moving to the latest line.

For version-by-version changes, see [Release notes](release-notes.md) (aggregated from `Source/Docs/Notes/` for the version in `Source/Src/Directory.Build.props` when the documentation site is published).
