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

The workflow does **not** push packages to nuget.org. A published GitHub release with `.nupkg` assets is **not** a completed public release until the steps in [Manual NuGet.org publish checklist](#manual-nugetorg-publish-checklist) are done.

## Manual NuGet.org publish checklist

Complete this **after** Release assist has produced packages and (if applicable) the GitHub release is no longer a draft. Skipping these steps leaves consumers without the new version on [nuget.org](https://www.nuget.org/).

### One-time NuGet.org setup

1. **Account and ownership** — Sign in at [nuget.org](https://www.nuget.org/). Confirm your account is an **owner** (or has push rights via a trusted publisher) on all four package IDs:
   - `KZDev.PrimeTime`
   - `KZDev.SystemClock.PrimeTime`
   - `KZDev.PrimeTime.Testing`
   - `KZDev.SystemClock.PrimeTime.Testing`
2. **API key** — Create a key at [nuget.org/account/apikeys](https://www.nuget.org/account/apikeys):
   - **Glob pattern:** limit to `KZDev.*` (or list each package id).
   - **Scopes:** at minimum **Push** existing package versions; for a **first-ever** publish of a new package id, include **Push new package and package version**.
   - **Expiry:** set explicitly; rotate before expiry.
3. **Store the key securely** — Use a password manager or OS secret store. **Never** commit the key, paste it into Issues, or log it in CI output.

### Before you push

- [ ] Release assist **Pack** succeeded and you have **eight** files for the release version: four `.nupkg` and four `.snupkg` (from the workflow artifact `release-assist-{version}`, GitHub release assets, or a local pack to `artifacts/package/release/`).
- [ ] The **version** in the filenames matches `Source/Src/Directory.Build.props` and the GitHub tag `v{version}`.
- [ ] You are pushing the **same build** you validated in CI (prefer CI artifacts over an uncommitted local pack).
- [ ] That version is **not already listed** on nuget.org for any of the four packages (NuGet rejects duplicate version uploads).

### Push packages and symbol packages

Set the API key in your shell (substitute your key; do not commit this value):

**PowerShell (Windows):**

```powershell
$env:NUGET_API_KEY = '<paste-api-key-here>'
$nugetSource = 'https://api.nuget.org/v3/index.json'
$packageDir = 'artifacts/package/release'
```

**bash (Linux/macOS):**

```bash
export NUGET_API_KEY='<paste-api-key-here>'
nugetSource='https://api.nuget.org/v3/index.json'
packageDir='artifacts/package/release'
```

If packages came from a downloaded GitHub Actions artifact, unzip them into `artifacts/package/release/` at the repository root (or set `packageDir` to that folder).

Push **all four** `.nupkg` files, then **all four** `.snupkg` symbol packages (same ids/versions; required for debugging because projects set `SymbolPackageFormat` to `snupkg`):

**PowerShell:**

```powershell
Get-ChildItem -Path $packageDir -Filter '*.nupkg' | ForEach-Object {
  dotnet nuget push $_.FullName --api-key $env:NUGET_API_KEY --source $nugetSource --skip-duplicate
}
Get-ChildItem -Path $packageDir -Filter '*.snupkg' | ForEach-Object {
  dotnet nuget push $_.FullName --api-key $env:NUGET_API_KEY --source $nugetSource --skip-duplicate
}
```

**bash:**

```bash
set -euo pipefail
for f in "$packageDir"/*.nupkg; do
  dotnet nuget push "$f" --api-key "$NUGET_API_KEY" --source "$nugetSource" --skip-duplicate
done
for f in "$packageDir"/*.snupkg; do
  dotnet nuget push "$f" --api-key "$NUGET_API_KEY" --source "$nugetSource" --skip-duplicate
done
```

`--skip-duplicate` allows safe re-runs if one package already uploaded; remove it when you need a hard failure on duplicates.

**Alternative (nuget.exe):** `nuget push <path> -ApiKey %NUGET_API_KEY% -Source https://api.nuget.org/v3/index.json` for each `.nupkg` and `.snupkg`. Prefer `dotnet nuget push` when the .NET SDK is already installed.

### After push — verify

- [ ] Each package page on nuget.org shows the new **{version}** (allow a few minutes for indexing).
- [ ] **Manage package** → **Manage owners** still lists the expected accounts.
- [ ] Optional smoke test: `dotnet add package KZDev.PrimeTime --version <version>` (and one testing package if desired).
- [ ] GitHub release notes and [hosted release notes](https://kzdev-net.github.io/kzdev.primetime/) match what you intended (DocFX updates via the doc publish workflow, not this push).

### Common failures

| Symptom | Likely cause |
|---------|----------------|
| **403 Forbidden** | API key expired, wrong scope, or account lacks ownership on the package id. |
| **409 Conflict** / “already exists” | That version was already pushed; bump version and re-run Release assist, or confirm you meant to push a new build. |
| **400** on `.snupkg` | Symbol package id/version does not match the `.nupkg`, or the main package was not pushed first. Push `.nupkg` before `.snupkg`. |
| Push succeeds but version missing on gallery | CDN delay; refresh after a few minutes. |

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
- You have planned the [Manual NuGet.org publish checklist](#manual-nugetorg-publish-checklist) for **after** the GitHub release is finalized (API key, ownership, `.nupkg` + `.snupkg` push).

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
- Version and pack-time note extraction: `Source/Src/Directory.Build.props` (MSBuild task in `KZDev.PrimeTime.ReleaseAggregation`)
- Aggregation tests: `Source/Tst/KZDev.PrimeTime.Tools.UnitTests/UsingReleaseNotesMarkdownAggregator.cs`
- GitHub Pages (`.github/workflows/docfx-publish.yml`): runs the **same** `validate` and `aggregate` steps as
  this workflow, writing `Source/Docs/articles/release-notes.md` before `docfx build`, so hosted release notes
  stay aligned with the aggregated markdown used for GitHub release bodies here.
