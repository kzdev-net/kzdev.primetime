// Copyright (c) Kevin Zehrer
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

namespace KZDev.PrimeTime.ReleaseAggregation;

/// <summary>
///   Reads per-package release-notes markdown and extracts the <c>### Package</c> section for the requested
///   version as plain text for NuGet <c>PackageReleaseNotes</c>.
/// </summary>
public static class PackageReleaseNotesPackageSectionExtractor
{
    private static readonly string[] ExpectedCategoryHeadings =
    [
        "### Added",
        "### Changed",
        "### Fixed",
        "### Notes",
        "### Package"
    ];

    /// <summary>
    ///   Extracts the <c>### Package</c> body for <paramref name="packageVersion"/> and returns plain text
    ///   suitable for NuGet package metadata.
    /// </summary>
    /// <param name="releaseNotesFilePath">
    ///   Absolute or relative path to the <c>*.release-notes.md</c> file.
    /// </param>
    /// <param name="packageVersion">
    ///   Version whose <c>## Version</c> section is read (must match packaging version).
    /// </param>
    /// <param name="packageId">
    ///   Package id used only for error messages.
    /// </param>
    /// <returns>
    ///   Trimmed plain-text <c>### Package</c> section for NuGet <c>PackageReleaseNotes</c>.
    /// </returns>
    /// <exception cref="FileNotFoundException">
    ///   Thrown when <paramref name="releaseNotesFilePath"/> does not exist.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    ///   Thrown when the file structure does not match the required release-notes conventions.
    /// </exception>
    public static string ExtractPlainTextPackageSection (
        string releaseNotesFilePath,
        string packageVersion,
        string packageId)
    {
        string packageSectionMarkdown = ExtractPackageSectionMarkdown(
            releaseNotesFilePath,
            packageVersion,
            packageId);
        return PackageReleaseNotesPlainTextFormatter.ToPlainText(packageSectionMarkdown);
    }

    /// <summary>
    ///   Extracts the raw <c>### Package</c> markdown body for <paramref name="packageVersion"/> without
    ///   stripping inline Markdown.
    /// </summary>
    /// <param name="releaseNotesFilePath">
    ///   Absolute or relative path to the <c>*.release-notes.md</c> file.
    /// </param>
    /// <param name="packageVersion">
    ///   Version whose <c>## Version</c> section is read.
    /// </param>
    /// <param name="packageId">
    ///   Package id used only for error messages.
    /// </param>
    /// <returns>
    ///   Trimmed <c>### Package</c> section markdown.
    /// </returns>
    /// <exception cref="FileNotFoundException">
    ///   Thrown when <paramref name="releaseNotesFilePath"/> does not exist.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    ///   Thrown when the file structure does not match the required release-notes conventions.
    /// </exception>
    public static string ExtractPackageSectionMarkdown (string releaseNotesFilePath, string packageVersion,
        string packageId)
    {
        string normalizedReleaseNotesPath = Path.GetFullPath(releaseNotesFilePath);
        if (!File.Exists(normalizedReleaseNotesPath))
        {
            throw new FileNotFoundException(
                $"Release notes file does not exist for package '{packageId}': '{normalizedReleaseNotesPath}'.",
                normalizedReleaseNotesPath);
        }

        string fileContent = File.ReadAllText(normalizedReleaseNotesPath).Replace("\r\n", "\n").Replace("\r", "\n");
        string normalizedVersionHeading = $"## Version {packageVersion}";
        string[] allLines = fileContent.Split('\n');
        int versionHeadingIndex = Array.FindIndex(
            allLines,
            line => string.Equals(line.TrimEnd(), normalizedVersionHeading, StringComparison.Ordinal));
        if (versionHeadingIndex < 0)
        {
            throw new InvalidOperationException(
                $"Missing version heading '{normalizedVersionHeading}' for package '{packageId}' in '{normalizedReleaseNotesPath}'.");
        }

        int nextVersionHeadingIndex = Array.FindIndex(
            allLines,
            versionHeadingIndex + 1,
            line => line.StartsWith("## Version ", StringComparison.Ordinal));
        int versionSectionEndIndex = nextVersionHeadingIndex >= 0 ? nextVersionHeadingIndex : allLines.Length;
        string[] versionLines = allLines
            .Skip(versionHeadingIndex + 1)
            .Take(versionSectionEndIndex - versionHeadingIndex - 1)
            .ToArray();

        int previousHeadingIndex = -1;
        foreach (string heading in ExpectedCategoryHeadings)
        {
            int headingIndex = Array.FindIndex(
                versionLines,
                line => string.Equals(line.TrimEnd(), heading, StringComparison.Ordinal));
            if (headingIndex < 0)
            {
                throw new InvalidOperationException(
                    $"Version section '{normalizedVersionHeading}' for package '{packageId}' in '{normalizedReleaseNotesPath}' is missing required heading '{heading}'.");
            }

            if (headingIndex <= previousHeadingIndex)
            {
                throw new InvalidOperationException(
                    $"Version section '{normalizedVersionHeading}' for package '{packageId}' in '{normalizedReleaseNotesPath}' is not in the required heading order.");
            }

            previousHeadingIndex = headingIndex;
        }

        int packageHeadingIndex = Array.FindIndex(
            versionLines,
            line => string.Equals(line.TrimEnd(), "### Package", StringComparison.Ordinal));
        int packageContentStartIndex = packageHeadingIndex + 1;
        while (packageContentStartIndex < versionLines.Length
               && string.IsNullOrWhiteSpace(versionLines[packageContentStartIndex]))
        {
            packageContentStartIndex++;
        }

        int packageContentEndIndex = versionLines.Length;
        for (int index = packageContentStartIndex; index < versionLines.Length; index++)
        {
            if (!versionLines[index].StartsWith("### ", StringComparison.Ordinal))
            {
                continue;
            }

            packageContentEndIndex = index;
            break;
        }

        string packageSectionText = string.Join(
                "\n",
                versionLines.Skip(packageContentStartIndex).Take(packageContentEndIndex - packageContentStartIndex))
            .Trim();
        if (string.IsNullOrWhiteSpace(packageSectionText))
        {
            throw new InvalidOperationException(
                $"Version section '{normalizedVersionHeading}' for package '{packageId}' has an empty '### Package' section.");
        }

        return packageSectionText;
    }
}
