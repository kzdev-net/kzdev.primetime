// Copyright (c) Kevin Zehrer
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using Microsoft.Build.Framework;
// ReSharper disable UnusedType.Global : MSBuild task types must be public and are instantiated via reflection by MSBuild, so they may appear unused in the codebase.
// ReSharper disable MemberCanBePrivate.Global : MSBuild task properties must be public for MSBuild to set them, so they may appear to be able to be private in the codebase.
// ReSharper disable UnusedAutoPropertyAccessor.Global : MSBuild task properties must have public getters and setters for MSBuild to set them, so they may appear to have unused accessors in the codebase.

namespace KZDev.PrimeTime.ReleaseAggregation;

/// <summary>
///   MSBuild task that sets <c>PackageReleaseNotes</c> from the <c>### Package</c> section of a per-package
///   release-notes markdown file for the packaging version.
/// </summary>
public sealed class ExtractPackageReleaseNotesTask : Microsoft.Build.Utilities.Task
{
    /// <summary>
    ///   Gets or sets the path to the <c>*.release-notes.md</c> source file.
    /// </summary>
    [Required]
    public string ReleaseNotesFilePath { get; set; } = string.Empty;

    /// <summary>
    ///   Gets or sets the package version (must match a <c>## Version</c> heading in the file).
    /// </summary>
    [Required]
    public string PackageVersion { get; set; } = string.Empty;

    /// <summary>
    ///   Gets or sets the NuGet package id (used in log messages).
    /// </summary>
    [Required]
    public string PackageId { get; set; } = string.Empty;

    /// <summary>
    ///   Gets the generated plain-text release notes for the nuspec.
    /// </summary>
    [Output]
    public string PackageReleaseNotes { get; private set; } = string.Empty;

    /// <inheritdoc />
    public override bool Execute ()
    {
        try
        {
            PackageReleaseNotes = PackageReleaseNotesPackageSectionExtractor.ExtractPlainTextPackageSection(
                ReleaseNotesFilePath,
                PackageVersion,
                PackageId);
            return true;
        }
        catch (Exception exception)
        {
            Log.LogError($"Failed to extract PackageReleaseNotes for package '{PackageId}' version '{PackageVersion}' from '{ReleaseNotesFilePath}'.");
            Log.LogErrorFromException(exception, showStackTrace: true);
            return false;
        }
    }
}
