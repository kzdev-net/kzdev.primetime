# Copyright (c) Kevin Zehrer
# Licensed under the MIT License. See LICENSE file in the project root for full license information.

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string] $SourceReadmePath,
    [Parameter(Mandatory = $true)]
    [string] $OutputReadmePath,
    [Parameter(Mandatory = $true)]
    [string] $ProductionPackageId,
    [Parameter(Mandatory = $true)]
    [string] $TestingPackageId,
    [Parameter(Mandatory = $true)]
    [string] $DirectDependenciesPath
)

$ErrorActionPreference = 'Stop'

$PackageRoot = $PSScriptRoot
$fragmentsDirectory = Join-Path -Path $PackageRoot -ChildPath 'fragments'
$targetFrameworksPath = Join-Path -Path $fragmentsDirectory -ChildPath 'testing-readme-target-frameworks.md'
$versionPairingTemplatePath = Join-Path -Path $fragmentsDirectory -ChildPath 'testing-readme-version-pairing.template.md'
$netStandard2PolyfillsPath = Join-Path -Path $fragmentsDirectory -ChildPath 'testing-readme-netstandard2-polyfills.md'

$targetFrameworks = (Get-Content -LiteralPath $targetFrameworksPath -Raw).Trim()
$versionPairingTemplate = (Get-Content -LiteralPath $versionPairingTemplatePath -Raw).Trim()
$versionPairing = $versionPairingTemplate.
    Replace('{{ProductionPackageId}}', $ProductionPackageId).Replace('{{TestingPackageId}}', $TestingPackageId)
$directDependencies = (Get-Content -LiteralPath $DirectDependenciesPath -Raw).Trim()
$netStandard2Polyfills = (Get-Content -LiteralPath $netStandard2PolyfillsPath -Raw).Trim()

$requirementsSection = (
    @(
        '## Requirements'
        ''
        $targetFrameworks
        $versionPairing
        $directDependencies
        $netStandard2Polyfills
        ''
    ) -join [Environment]::NewLine
).TrimEnd() + [Environment]::NewLine

$sourceReadme = Get-Content -LiteralPath $SourceReadmePath -Raw
$placeholder = '{{REQUIREMENTS}}'
if (-not $sourceReadme.Contains($placeholder))
{
    throw "Source readme '$SourceReadmePath' does not contain placeholder '$placeholder'."
}

$outputReadme = $sourceReadme.Replace($placeholder, $requirementsSection)
$outputDirectory = Split-Path -Parent $OutputReadmePath
if (-not [string]::IsNullOrEmpty($outputDirectory) -and -not (Test-Path -LiteralPath $outputDirectory))
{
    New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null
}

Set-Content -LiteralPath $OutputReadmePath -Value $outputReadme -NoNewline -Encoding utf8NoBOM
