# Runs wall-clock PrimeTime tests under resource constraints similar to CI (single CPU, no xUnit parallelism).
# Uses the xUnit v3 in-process runner (app host or `dotnet exec` on the test assembly) to avoid VSTest protocol issues.
#
# Usage:
#   .\scripts\test-wall-clock-constrained.ps1
#   .\scripts\test-wall-clock-constrained.ps1 -Class "KZDev.SystemClock.PrimeTime.UnitTests.UsingIPrimeClockDayTimeTimers"
#   .\scripts\test-wall-clock-constrained.ps1 -Framework net10.0 -Configuration Release -NoBuild
#
# Serial xUnit, single logical processor hint, in-process runner (avoids VSTest hangs)
#   .\scripts\test-wall-clock-constrained.ps1 -Configuration Release -Framework net10.0 -Class "KZDev.SystemClock.PrimeTime.UnitTests.UsingIPrimeClockDayTimeTimers"
#
# After a build, skip rebuild:
#   .\scripts\test-wall-clock-constrained.ps1 -NoBuild -Configuration Release -Framework net10.0
#
# Linux CI-like constraint (Docker single CPU):
#   docker run --rm --cpus=1 -v "${PWD}:/src" -w /src mcr.microsoft.com/dotnet/sdk:10.0 `
#     pwsh scripts/test-wall-clock-constrained.ps1 -Configuration Release -Framework net10.0

[CmdletBinding()]
param(
    [string]$Project = "KZDev.SystemClock.PrimeTime.UnitTests",
    [string]$Framework = "net8.0",
    [string]$Configuration = "Debug",
    [string]$Class = "",
    [int]$MaxParallelThreads = 1,
    [switch]$NoBuild
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $repoRoot "Source/Tst/$Project/$Project.csproj"
$configurationFolder = if ($Configuration -eq "Release") { "release" } else { "debug" }
$frameworkFolder = $Framework.ToLowerInvariant()
$outputDirectory = Join-Path $repoRoot "artifacts/bin/$Project/${configurationFolder}_${frameworkFolder}"

function Resolve-TestRunnerInvocation {
    param(
        [string]$Directory,
        [string]$AssemblyName
    )

    $appHostExe = Join-Path $Directory "$AssemblyName.exe"
    if (Test-Path $appHostExe) {
        return @{ Executable = $appHostExe; PrefixArgs = @() }
    }

    $appHost = Join-Path $Directory $AssemblyName
    if (Test-Path $appHost) {
        return @{ Executable = $appHost; PrefixArgs = @() }
    }

    $assemblyDll = Join-Path $Directory "$AssemblyName.dll"
    if (Test-Path $assemblyDll) {
        return @{ Executable = "dotnet"; PrefixArgs = @("exec", $assemblyDll) }
    }

    return $null
}

$testRunner = Resolve-TestRunnerInvocation -Directory $outputDirectory -AssemblyName $Project

Push-Location $repoRoot
try {
    $env:DOTNET_PROCESSOR_COUNT = "$MaxParallelThreads"
    # Do not cap CLR thread-pool worker threads: System.Threading.Timer callbacks need pool threads
    # while the test thread may be blocked in ManualResetEventSlim.Wait, which deadlocks at MaxWorkerThreads=1.

    if (-not $NoBuild) {
        Write-Host "Building $projectPath ($Configuration, $Framework)..."
        dotnet build $projectPath -c $Configuration -f $Framework
        if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    }

    if ($null -eq $testRunner) {
        Write-Error "Test runner not found under $outputDirectory. Expected $Project.exe, $Project, or $Project.dll. Build first or omit -NoBuild."
    }

    $runnerArgs = @(
        "-parallel", "none",
        "-maxThreads", "$MaxParallelThreads",
        "-parallelAlgorithm", "conservative",
        "-noColor"
    )
    if ($Class) {
        $runnerArgs += @("-class", $Class)
    }

    $commandLine = @($testRunner.PrefixArgs + $runnerArgs) -join " "
    Write-Host "Running constrained wall-clock tests via xUnit in-process runner:"
    Write-Host "  $($testRunner.Executable) $commandLine"
    if ($testRunner.PrefixArgs.Count -gt 0) {
        & $testRunner.Executable @($testRunner.PrefixArgs + $runnerArgs)
    }
    else {
        & $testRunner.Executable @runnerArgs
    }
    exit $LASTEXITCODE
}
finally {
    Pop-Location
}
