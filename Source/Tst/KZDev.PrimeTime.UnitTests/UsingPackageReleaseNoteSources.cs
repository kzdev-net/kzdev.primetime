// Copyright (c) Kevin Zehrer
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

using AwesomeAssertions;

namespace KZDev.PrimeTime.UnitTests;

//################################################################################
/// <summary>
///   Validates repository package release-note source files required for release readiness.
/// </summary>
[ExcludeFromCodeCoverage]
public class UsingPackageReleaseNoteSources
{
    private static readonly Regex VersionHeadingRegex = new(
        @"^## Version \d+\.\d+\.\d+\s*$",
        RegexOptions.Multiline | RegexOptions.CultureInvariant);

    private static readonly IReadOnlyList<string> ExpectedCategoryHeadings =
    [
        "### Added",
        "### Changed",
        "### Fixed",
        "### Notes",
        "### Package"
    ];

    private static readonly IReadOnlyList<string> ReleaseNoteFileNames =
    [
        "KZDev.PrimeTime.release-notes.md",
        "KZDev.SystemClock.PrimeTime.release-notes.md",
        "KZDev.PrimeTime.Testing.release-notes.md",
        "KZDev.SystemClock.PrimeTime.Testing.release-notes.md"
    ];

    private static readonly string NotesDirectoryPath = GetNotesDirectoryPath();

    /// <summary>
    ///   Resolves the repository Source/Docs/Notes directory by walking parent directories from the
    ///   test output location until the repository root is found.
    /// </summary>
    /// <returns>
    ///   The full path to the Source/Docs/Notes directory.
    /// </returns>
    private static string GetNotesDirectoryPath ()
    {
        string currentDirectoryPath = AppContext.BaseDirectory;
        DirectoryInfo? currentDirectoryInfo = new(currentDirectoryPath);

        while (currentDirectoryInfo is not null)
        {
            string sourceDirectoryPath = Path.Combine(currentDirectoryInfo.FullName, "Source");
            string readmeFilePath = Path.Combine(currentDirectoryInfo.FullName, "README.md");
            bool hasSourceDirectory = Directory.Exists(sourceDirectoryPath);
            bool hasReadmeFile = File.Exists(readmeFilePath);

            if (hasSourceDirectory && hasReadmeFile)
            {
                string notesDirectoryPath = Path.Combine(sourceDirectoryPath, "Docs", "Notes");
                Directory.Exists(notesDirectoryPath).Should().BeTrue("expected Source/Docs/Notes directory to exist");
                return notesDirectoryPath;
            }

            currentDirectoryInfo = currentDirectoryInfo.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate repository root containing Source and README.md.");
    }

    /// <summary>
    ///   Verifies each required package release-notes file exists under Source/Docs/Notes.
    /// </summary>
    [Fact]
    public void ReleaseNotesFiles_Validate_ExistInDocsNotesDirectory ()
    {
        foreach (string releaseNotesFileName in ReleaseNoteFileNames)
        {
            string filePath = Path.Combine(NotesDirectoryPath, releaseNotesFileName);
            File.Exists(filePath).Should().BeTrue($"expected release-notes file '{releaseNotesFileName}' to exist");
        }
    }

    /// <summary>
    ///   Verifies each required package release-notes file contains at least one valid version
    ///   heading in the expected machine-parseable format.
    /// </summary>
    [Fact]
    public void ReleaseNotesFiles_Validate_ContainVersionSections ()
    {
        foreach (string releaseNotesFileName in ReleaseNoteFileNames)
        {
            string filePath = Path.Combine(NotesDirectoryPath, releaseNotesFileName);
            string fileContent = File.ReadAllText(filePath);
            MatchCollection versionMatches = VersionHeadingRegex.Matches(fileContent);
            versionMatches.Count.Should().BeGreaterThan(0,
                $"expected at least one version heading in '{releaseNotesFileName}'");
        }
    }

    /// <summary>
    ///   Verifies each required package release-notes file includes the expected section category
    ///   headings used for consistent automated extraction.
    /// </summary>
    [Fact]
    public void ReleaseNotesFiles_Validate_ContainExpectedCategoryHeadings ()
    {
        foreach (string releaseNotesFileName in ReleaseNoteFileNames)
        {
            string filePath = Path.Combine(NotesDirectoryPath, releaseNotesFileName);
            string fileContent = File.ReadAllText(filePath);
            string normalizedFileContent = fileContent.Replace("\r\n", "\n");

            foreach (string expectedHeading in ExpectedCategoryHeadings)
            {
                string expectedLine = expectedHeading + "\n";
                normalizedFileContent.Should().Contain(expectedLine,
                    $"expected category heading '{expectedHeading}' in '{releaseNotesFileName}'");
            }
        }
    }
}
//################################################################################
