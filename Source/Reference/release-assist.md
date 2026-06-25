# Release assist workflow

This document describes how maintainers use the **Release assist** GitHub Actions workflow to validate the solution, produce NuGet packages, aggregate per-package release notes into a single markdown body, optionally open a **draft** GitHub release with those assets, and (after explicit approval) mark that release as published.

The workflow definition lives at [.github/workflows/release-assist.yml](https://github.com/kzdev-net/kzdev.primetime/blob/main/.github/workflows/release-assist.yml) in the repository (path from repo root: `/.github/workflows/release-assist.yml`).

## What the workflow does

1. **Resolve version** — Uses the `version` workflow input if you provide one; otherwise reads `<Version>` from `Source/Src/Directory.Build.props`. The resolved value must match a `## Version …` section in each package release-notes file under `Source/Docs/Notes/`.
2. **Guardrails before build** — Validates governance docs exist (`SECURITY.md`, `CONTRIBUTING.md`, `SUPPORT.md`) and validates all package release-notes sources for the resolved version.
3. **Restore, build, and test** — Same solution as CI: `Source/KZDev.PrimeTime.slnx` in `Release`.
4. **Pack** — Builds all four publishable packages with `IsPacking=true` and `ContinuousIntegrationBuild=true`. Testing packages compose their NuGet README via `pwsh` during this step (see [Testing package README composition](#testing-package-readme-composition-powershell-7)). Packages and symbol packages are written under `artifacts/package/release/`.
5. **Aggregate release notes** — Runs `KZDev.PrimeTime.ReleaseAggregation.Cli` to generate `release-body.md` at the workspace root (one combined document with each package’s version section).
6. **Upload artifacts** — Always uploads NuGet packages, symbol packages, and `release-body.md` as a workflow artifact (name includes the resolved version).
7. **Draft GitHub release** — Only when **dry run** is off: creates a **draft** release with tag `v{version}`, attaches all `.nupkg` and `.snupkg` files from `artifacts/package/release/`, and uses `release-body.md` as the release notes.
8. **Finalize publish** — Only when **dry run** is off: a second job, gated by the **`primetime-release`** GitHub Environment, runs `gh release edit … --draft=false --latest` so a human approves before the release stops being a draft.

The workflow does **not** push packages to nuget.org. A published GitHub release with `.nupkg` assets is **not** a completed public release until the steps in [Manual NuGet.org publish checklist](#manual-nugetorg-publish-checklist) are done.

## Testing package README composition (PowerShell 7)

The two testing packages (`KZDev.PrimeTime.Testing`, `KZDev.SystemClock.PrimeTime.Testing`) ship a composed NuGet README built from source templates, direct-dependency snippets, and shared fragments. During **Release** pack with `IsPacking=true`, MSBuild runs the **`ComposeTestingPackageReadme` target** (gated by the homonymous MSBuild property in [TestingPackageReadme.targets](https://github.com/kzdev-net/kzdev.primetime/blob/main/Source/Src/Package/TestingPackageReadme.targets)) **before** `Pack`, which requires **PowerShell 7+** (`pwsh`).

### Compose inputs and outputs

| Role | Path (from repo root) |
|------|------------------------|
| Source template | `Source/Src/Package/{PackageId}.readme.src.md` |
| Direct dependencies snippet | `Source/Src/Package/{PackageId}.requirements.direct-dependencies.md` |
| Shared fragments | `Source/Src/Package/fragments/*.md` |
| Compose script | `Source/Src/Package/ComposeTestingPackageReadme.ps1` |
| **Committed output** (packed into the `.nupkg`) | `Source/Src/Package/{PackageId}.readme.md` |

The MSBuild property **`ComposeTestingPackageReadme`** is defined in `Source/Src/Package/TestingPackageReadme.targets` (imported via `TestingPackageReadme.props` under testing projects—not in individual `.csproj` files). It defaults to **false** for **`Debug`** and **true** for **`Release`** and **`Package`**. Solution build configurations are **`Debug`**, **`Package`**, and **`Release`** (`Source/KZDev.PrimeTime.slnx`). Override with `-p:ComposeTestingPackageReadme=true` or `false`. Release assist packs with `-c Release` and `-p:IsPacking=true`; **`Package`** configuration also sets `IsPacking=true` via `Source/Src/Directory.Build.props` without passing that property explicitly.

### PowerShell requirement

- **Default:** `pwsh` on `PATH` (MSBuild property `TestingReadmePwshPath` defaults to `pwsh`).
- **Custom install location:** pass an explicit path when invoking pack, for example:
  ```text
  dotnet pack <testing-csproj> -c Release -p:IsPacking=true -p:TestingReadmePwshPath="C:\Program Files\PowerShell\7\pwsh.exe"
  ```
- **GitHub-hosted runners:** `ubuntu-latest` includes PowerShell 7; Release assist **Pack** uses it without extra setup.
- **Windows:** install [PowerShell 7](https://learn.microsoft.com/powershell/scripting/install/installing-powershell-on-windows) or set `TestingReadmePwshPath`. The compose script runs with `-NoProfile -File`; your execution policy must allow running this repo-local script (for example `RemoteSigned` on Windows).

If `pwsh` is missing, pack fails with an MSBuild error naming the testing project.

### Keep composed READMEs committed

The composed `*.readme.md` files under `Source/Src/Package/` are **source-controlled**. When you change any compose input (`.readme.src.md`, `.requirements.direct-dependencies.md`, or `fragments/*.md`), regenerate and **commit** the outputs **before** tagging a release:

1. Run Release assist with **dry_run = true**, or pack the testing projects locally (see [Local parity](#local-parity-optional)).
2. Review `git diff` on `Source/Src/Package/KZDev.*.Testing.readme.md`.
3. Commit output changes together with your compose-input edits.

Pushing without committing stale composed READMEs ships outdated NuGet package readmes even when sources were updated.

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
   - **version** (optional): e.g. `1.0.0`. Leave empty to use `Source/Src/Directory.Build.props`.
   - **dry_run**:
     - **`true`** (default): restore, build, test, pack, aggregate notes, upload artifacts only. **No** GitHub release and **no** finalize job.
     - **`false`**: same as above, then creates a **draft** release and runs the finalize job after environment approval.

4. Open the workflow run: confirm **Test** and **Pack packages** succeeded; download the **release-assist-{version}** artifact if you want to inspect packages or `release-body.md` locally.

## Before `dry_run = false`

Confirm all of the following to avoid a failed run or a misleading draft:

- **`Source/Src/Directory.Build.props`** `<Version>` matches the release you intend (or matches the **version** input if you set it).
- Each file under `Source/Docs/Notes/*.release-notes.md` contains a **`## Version {same version}`** block with the expected structure (same rules as pack-time `PackageReleaseNotes` extraction).
- `SECURITY.md`, `CONTRIBUTING.md`, and `SUPPORT.md` are present at repository root.
- The **`primetime-release`** environment is configured if you want the second job to require approval.
- The tag **`v{version}`** does not already point to a different release you care about; `gh release create` will fail if the release or tag already exists in a conflicting way.
- **PowerShell 7** (`pwsh`) is available on the runner (included on `ubuntu-latest`) or on your machine for local pack; see [Testing package README composition](#testing-package-readme-composition-powershell-7).
- If compose inputs changed, **`Source/Src/Package/KZDev.*.Testing.readme.md`** are committed and match a fresh Release pack (or a **dry_run** artifact build).
- You have planned the [Manual NuGet.org publish checklist](#manual-nugetorg-publish-checklist) for **after** the GitHub release is finalized (API key, ownership, `.nupkg` + `.snupkg` push). **NuGet.org push remains manual** — this workflow never uploads to the registry.

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

Packing locally matches `Source/Src/package.cmd` (all four packages) or individual `dotnet pack` invocations:

```text
dotnet pack Source/Src/KZDev.PrimeTime/KZDev.PrimeTime.csproj -c Release -p:IsPacking=true -p:ContinuousIntegrationBuild=true
dotnet pack Source/Src/KZDev.SystemClock.PrimeTime/KZDev.SystemClock.PrimeTime.csproj -c Release -p:IsPacking=true -p:ContinuousIntegrationBuild=true
dotnet pack Source/Src/Testing/KZDev.PrimeTime.Testing/KZDev.PrimeTime.Testing.csproj -c Release -p:IsPacking=true -p:ContinuousIntegrationBuild=true
dotnet pack Source/Src/Testing/KZDev.SystemClock.PrimeTime.Testing/KZDev.SystemClock.PrimeTime.Testing.csproj -c Release -p:IsPacking=true -p:ContinuousIntegrationBuild=true
```

The two testing projects require `pwsh` for README compose during pack. Outputs land under `artifacts/package/release/` (`.nupkg` and `.snupkg`).

## Related sources

- Per-package release notes: `Source/Docs/Notes/`
- Version and pack-time note extraction: `Source/Src/Directory.Build.props` (MSBuild task in `KZDev.PrimeTime.ReleaseAggregation`)
- Aggregation tests: `Source/Tst/KZDev.PrimeTime.Tools.UnitTests/UsingReleaseNotesMarkdownAggregator.cs`
- Testing README compose: `Source/Src/Package/TestingPackageReadme.props`, `TestingPackageReadme.targets`, `ComposeTestingPackageReadme.ps1`, `fragments/`
- GitHub Pages (`.github/workflows/docfx-publish.yml`): runs the **same** `validate` and `aggregate` steps as
  this workflow, writing `Source/Docs/articles/release-notes.md` before `docfx build`, so hosted release notes
  stay aligned with the aggregated markdown used for GitHub release bodies here.
