# Release assist workflow

This document describes how maintainers use the **Release assist** GitHub Actions workflow to validate the solution, produce NuGet packages, aggregate per-package release notes into a single markdown body, optionally open a **draft** GitHub release with those assets, and (after explicit approval) mark that release as published.

The workflow definition lives at [.github/workflows/release-assist.yml](https://github.com/kzdev-net/kzdev.primetime/blob/main/.github/workflows/release-assist.yml) in the repository (path from repo root: `/.github/workflows/release-assist.yml`).

## What the workflow does

1. **Resolve version** — Uses the `version` workflow input if you provide one; otherwise reads `<Version>` from `Source/Src/Directory.Build.props`. The resolved value must match a `## Version …` section in each package release-notes file under `Source/Docs/Notes/`.
2. **Guardrails before build** — Validates governance docs exist (`SECURITY.md`, `CONTRIBUTING.md`, `SUPPORT.md`) and validates all package release-notes sources for the resolved version.
3. **Restore, build, and test** — Same solution as CI: `Source/KZDev.PrimeTime.slnx` in `Release`.
4. **Pack** — Builds all four publishable packages with `IsPacking=true` and `ContinuousIntegrationBuild=true`. Packages and symbol packages are written under `artifacts/package/release/`.
5. **Aggregate release notes** — Runs `KZDev.PrimeTime.ReleaseAggregation.Cli` to generate `release-body.md` at the workspace root (one combined document with each package’s version section).
6. **Upload artifacts** — Always uploads NuGet packages, symbol packages, and `release-body.md` as a workflow artifact (name includes the resolved version).
7. **Draft GitHub release** — Only when **dry run** is off: creates a **draft** release with tag `v{version}`, attaches all `.nupkg` and `.snupkg` files from `artifacts/package/release/`, and uses `release-body.md` as the release notes.
8. **Finalize publish** — Only when **dry run** is off: a second job, gated by the **`primetime-release`** GitHub Environment, runs `gh release edit … --draft=false --latest` so a human approves before the release stops being a draft.

The workflow does **not** push packages to nuget.org. Publishing to a package registry remains a separate, manual or separately automated step if you use one.

## One-time repository setup

### Environment for the finalize step

The second job uses:

`environment: primetime-release`

Before you rely on that gate, configure the environment in GitHub:

1. Open the repository on GitHub → **Settings** → **Environments**.
2. Create an environment named **`primetime-release`** (name must match the workflow).
3. Add **Required reviewers** (and any other protection you want) so the **Finalize publish** job waits for an explicit approval.

Without this environment, or with no reviewers, behavior depends on your organization’s default environment rules; setting reviewers is what makes “manual final publish” meaningful.

### Permissions

The workflow requests `contents: write` so `gh` can create and update releases using `GITHUB_TOKEN`. No extra secrets are required for the default release flow on GitHub-hosted runners.

## How to run it

1. Push the commit you want to release (typically on `main` or your release branch) to GitHub.
2. Go to **Actions** → **Release assist** → **Run workflow**.
3. Choose inputs:
   - **version** (optional): e.g. `0.0.6`. Leave empty to use `Source/Src/Directory.Build.props`.
   - **dry_run**:
     - **`true`** (default): restore, build, test, pack, aggregate notes, upload artifacts only. **No** GitHub release and **no** finalize job.
     - **`false`**: same as above, then creates a **draft** release and runs the finalize job after environment approval.

4. Open the workflow run: confirm **Test** and **Pack** succeeded; download the **release assist** artifact if you want to inspect packages or `release-body.md` locally.

## Before `dry_run = false`

Confirm all of the following to avoid a failed run or a misleading draft:

- **`Source/Src/Directory.Build.props`** `<Version>` matches the release you intend (or matches the **version** input if you set it).
- Each file under `Source/Docs/Notes/*.release-notes.md` contains a **`## Version {same version}`** block with the expected structure (same rules as pack-time `PackageReleaseNotes` extraction).
- `SECURITY.md`, `CONTRIBUTING.md`, and `SUPPORT.md` are present at repository root.
- The **`primetime-release`** environment is configured if you want the second job to require approval.
- The tag **`v{version}`** does not already point to a different release you care about; `gh release create` will fail if the release or tag already exists in a conflicting way.

## Local parity (optional)

You can reproduce aggregation without GitHub by building the CLI and running:

```text
dotnet run --project Source/Tools/KZDev.PrimeTime.ReleaseAggregation.Cli/KZDev.PrimeTime.ReleaseAggregation.Cli.csproj -c Release --
  aggregate --repository-root <repo-root> [--version <x.y.z>] --output <path-to-output.md>
```

If you omit `--version`, the tool reads `<Version>` from `Source/Src/Directory.Build.props`.

You can run release-notes validation only (used by CI/release guardrails) with:

```text
dotnet run --project Source/Tools/KZDev.PrimeTime.ReleaseAggregation.Cli/KZDev.PrimeTime.ReleaseAggregation.Cli.csproj -c Release --
  validate --repository-root <repo-root> [--version <x.y.z>]
```

Packing locally matches `Source/Src/package.cmd` patterns: `dotnet pack` on each of the four package projects with `-c Release -p:IsPacking=true -p:ContinuousIntegrationBuild=true`.

## Related sources

- Per-package release notes: `Source/Docs/Notes/`
- Version and pack-time note extraction: `Source/Src/Directory.Build.props`
- Aggregation tests: `Source/Tst/KZDev.PrimeTime.UnitTests/UsingReleaseNotesMarkdownAggregator.cs`
