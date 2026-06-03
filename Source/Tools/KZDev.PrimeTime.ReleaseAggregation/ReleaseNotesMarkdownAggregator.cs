// Copyright (c) Kevin Zehrer
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Text;

namespace KZDev.PrimeTime.ReleaseAggregation;

/// <summary>
///   Builds a single markdown document that aggregates the <c>## Version {version}</c> section from each package
///   release-notes source file.
/// </summary>
public static class ReleaseNotesMarkdownAggregator
{
    /// <summary>
    ///   Validates that every configured package release-notes file exists and contains a
    ///   <c>## Version {version}</c> section.
    /// </summary>
    /// <param name="repositoryRootPath">
    ///   Repository root directory.
    /// </param>
    /// <param name="version">
    ///   Version to match in each file (must match <c>## Version {version}</c> headings).
    /// </param>
    /// <param name="sources">
    ///   Package id and release-notes relative path pairs (defaults to <see cref="PrimeTimePackageReleaseNotesSources" />).
    /// </param>
    /// <exception cref="ReleaseNotesAggregationException">
    ///   Thrown when a file is missing or the version section is not found.
    /// </exception>
    public static void ValidateReleaseNotesForVersion (
        string repositoryRootPath,
        string version,
        IReadOnlyList<PackageReleaseNotesSource>? sources = null)
    {
        if (string.IsNullOrWhiteSpace(repositoryRootPath))
        {
            throw new ReleaseNotesAggregationException("Repository root path is required.");
        }

        if (string.IsNullOrWhiteSpace(version))
        {
            throw new ReleaseNotesAggregationException("Version is required.");
        }

        IReadOnlyList<PackageReleaseNotesSource> packageSources = sources ?? PrimeTimePackageReleaseNotesSources;
        foreach (PackageReleaseNotesSource packageSource in packageSources)
        {
            string notesPath = Path.Combine(repositoryRootPath, packageSource.ReleaseNotesRelativePath);
            if (!File.Exists(notesPath))
            {
                throw new ReleaseNotesAggregationException(
                    $"Release notes file missing for package '{packageSource.PackageId}': '{notesPath}'.");
            }

            string fileContent = File.ReadAllText(notesPath);
            _ = ExtractVersionSection(fileContent, version, packageSource.PackageId, notesPath);
        }
    }

    /// <summary>
    ///   The four publishable PrimeTime packages and their <c>Source/Docs/Notes/*.release-notes.md</c> paths.
    /// </summary>
    public static IReadOnlyList<PackageReleaseNotesSource> PrimeTimePackageReleaseNotesSources { get; } =
        new List<PackageReleaseNotesSource>
        {
            new("KZDev.PrimeTime",
                Path.Combine("Source", "Docs", "Notes", "KZDev.PrimeTime.release-notes.md")),
            new("KZDev.SystemClock.PrimeTime",
                Path.Combine("Source", "Docs", "Notes", "KZDev.SystemClock.PrimeTime.release-notes.md")),
            new("KZDev.PrimeTime.Testing",
                Path.Combine("Source", "Docs", "Notes", "KZDev.PrimeTime.Testing.release-notes.md")),
            new("KZDev.SystemClock.PrimeTime.Testing",
                Path.Combine("Source", "Docs", "Notes", "KZDev.SystemClock.PrimeTime.Testing.release-notes.md"))
        }.AsReadOnly();

    /// <summary>
    ///   Produces markdown suitable for a GitHub release body: title line, intro, then per-package headings and
    ///   the full <c>## Version</c> section copied from each package file.
    /// </summary>
    /// <param name="repositoryRootPath">
    ///   Repository root directory.
    /// </param>
    /// <param name="version">
    ///   Version to match in each file (must match <c>## Version {version}</c> headings).
    /// </param>
    /// <param name="sources">
    ///   Package id and release-notes relative path pairs (defaults to <see cref="PrimeTimePackageReleaseNotesSources" />).
    /// </param>
    /// <returns>
    ///   Aggregated markdown.
    /// </returns>
    /// <exception cref="ReleaseNotesAggregationException">
    ///   Thrown when a file is missing or the version section is not found.
    /// </exception>
    public static string BuildAggregatedReleaseNotesDocument (
        string repositoryRootPath,
        string version,
        IReadOnlyList<PackageReleaseNotesSource>? sources = null)
    {
        ValidateReleaseNotesForVersion(repositoryRootPath, version, sources);
        IReadOnlyList<PackageReleaseNotesSource> packageSources = sources ?? PrimeTimePackageReleaseNotesSources;
        StringBuilder stringBuilder = new();
        stringBuilder.AppendLine($"# KZDev PrimeTime {version}");
        stringBuilder.AppendLine();
        stringBuilder.AppendLine(
            "Aggregated per-package release notes for this version (source: `Source/Docs/Notes/*.release-notes.md`).");
        stringBuilder.AppendLine();

        foreach (PackageReleaseNotesSource packageSource in packageSources)
        {
            string notesPath = Path.Combine(repositoryRootPath, packageSource.ReleaseNotesRelativePath);
            string fileContent = File.ReadAllText(notesPath);
            string versionSection = ExtractVersionSection(fileContent, version, packageSource.PackageId, notesPath);
            stringBuilder.AppendLine($"## {packageSource.PackageId}");
            stringBuilder.AppendLine();
            stringBuilder.AppendLine(versionSection);
            stringBuilder.AppendLine();
        }

        return stringBuilder.ToString().TrimEnd();
    }

    /// <summary>
    ///   Returns the markdown block starting at <c>## Version {version}</c> through the line before the next
    ///   <c>## Version</c> heading (or end of file).
    /// </summary>
    /// <param name="fileContent">
    ///   Full release-notes file text.
    /// </param>
    /// <param name="version">
    ///   Version string to locate.
    /// </param>
    /// <param name="packageId">
    ///   Package id for error messages.
    /// </param>
    /// <param name="filePathForErrors">
    ///   File path for error messages.
    /// </param>
    /// <returns>
    ///   The extracted section, trimmed.
    /// </returns>
    /// <exception cref="ReleaseNotesAggregationException">
    ///   Thrown when the version heading is not found.
    /// </exception>
    public static string ExtractVersionSection (
        string fileContent,
        string version,
        string packageId,
        string filePathForErrors)
    {
        string normalizedContent = fileContent.Replace("\r\n", "\n").Replace("\r", "\n");
        string versionHeading = $"## Version {version}";
        string[] lines = normalizedContent.Split('\n');
        int versionLineIndex = Array.FindIndex(
            lines,
            line => string.Equals(line.TrimEnd(), versionHeading, StringComparison.Ordinal));

        if (versionLineIndex < 0)
        {
            throw new ReleaseNotesAggregationException(
                $"Missing version heading '{versionHeading}' for package '{packageId}' in '{filePathForErrors}'.");
        }

        int nextVersionLineIndex = Array.FindIndex(
            lines,
            versionLineIndex + 1,
            line => line.StartsWith("## Version ", StringComparison.Ordinal));

        int exclusiveEnd = nextVersionLineIndex >= 0 ? nextVersionLineIndex : lines.Length;
        IEnumerable<string> sectionLines = lines.Skip(versionLineIndex).Take(exclusiveEnd - versionLineIndex);
        return string.Join("\n", sectionLines).Trim();
    }
}
