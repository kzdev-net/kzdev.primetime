// Copyright (c) Kevin Zehrer
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

namespace KZDev.PrimeTime.ReleaseAggregation;

/// <summary>
///   Identifies a NuGet package and the repository-relative path to its machine-authored release-notes markdown file.
/// </summary>
public sealed class PackageReleaseNotesSource
{
    /// <summary>
    ///   Initializes a new instance of the <see cref="PackageReleaseNotesSource" /> class.
    /// </summary>
    /// <param name="packageId">
    ///   The NuGet package id (for example <c>KZDev.PrimeTime</c>).
    /// </param>
    /// <param name="releaseNotesRelativePath">
    ///   Path to the release-notes file, relative to the repository root (resolved with
    ///   <see cref="System.IO.Path.Combine(string, string)" />).
    /// </param>
    public PackageReleaseNotesSource (string packageId, string releaseNotesRelativePath)
    {
        PackageId = packageId;
        ReleaseNotesRelativePath = releaseNotesRelativePath;
    }

    /// <summary>
    ///   Gets the NuGet package id.
    /// </summary>
    public string PackageId { get; }

    /// <summary>
    ///   Gets the repository-relative path to the release-notes markdown file.
    /// </summary>
    public string ReleaseNotesRelativePath { get; }
}
