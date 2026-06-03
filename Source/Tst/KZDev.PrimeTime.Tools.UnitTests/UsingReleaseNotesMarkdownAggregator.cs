// Copyright (c) Kevin Zehrer
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

#if NET10_0_OR_GREATER
using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

using AwesomeAssertions;

using KZDev.PrimeTime.ReleaseAggregation;

namespace KZDev.PrimeTime.Tools.UnitTests;

//################################################################################
/// <summary>
///   Validates release-note aggregation used by release-assist automation.
/// </summary>
[ExcludeFromCodeCoverage]
public class UsingReleaseNotesMarkdownAggregator
{
    private static readonly string RepositoryRootPath = GetRepositoryRootPath();

    /// <summary>
    ///   Resolves the repository root directory by walking parent directories from the test output
    ///   location until a directory containing both Source and README.md is found.
    /// </summary>
    /// <returns>
    ///   The full path to the repository root directory.
    /// </returns>
    private static string GetRepositoryRootPath ()
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
                return currentDirectoryInfo.FullName;
            }

            currentDirectoryInfo = currentDirectoryInfo.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate repository root containing Source and README.md.");
    }

    /// <summary>
    ///   Verifies the repository version can be read from Source/Src/Directory.Build.props.
    /// </summary>
    [Fact]
    public void RepositoryVersionReader_ReadFromProps_ReturnsThreePartVersion ()
    {
        string version = RepositoryVersionReader.ReadVersionFromSrcDirectoryBuildProps(RepositoryRootPath);
        version.Should().NotBeNullOrWhiteSpace();
        version.Should().MatchRegex(@"^\d+\.\d+\.\d+");
    }

    /// <summary>
    ///   Verifies aggregation includes every publishable package heading for the current repository version.
    /// </summary>
    [Fact]
    public void Aggregator_ForRepositoryCurrentVersion_IncludesAllPackageHeadings ()
    {
        string version = RepositoryVersionReader.ReadVersionFromSrcDirectoryBuildProps(RepositoryRootPath);
        string document = ReleaseNotesMarkdownAggregator.BuildAggregatedReleaseNotesDocument(
            RepositoryRootPath,
            version);
        Regex.Matches(document, @"^## KZDev\.PrimeTime\s*$", RegexOptions.Multiline).Count.Should().Be(1);
        Regex.Matches(document, @"^## KZDev\.SystemClock\.PrimeTime\s*$", RegexOptions.Multiline).Count.Should().Be(1);
        Regex.Matches(document, @"^## KZDev\.PrimeTime\.Testing\s*$", RegexOptions.Multiline).Count.Should().Be(1);
        Regex.Matches(document, @"^## KZDev\.SystemClock\.PrimeTime\.Testing\s*$", RegexOptions.Multiline).Count
            .Should().Be(1);
        Regex.Matches(document, $"^## Version {Regex.Escape(version)}\\s*$", RegexOptions.Multiline).Count.Should().Be(4);
    }

    /// <summary>
    ///   Verifies a missing version heading surfaces a structured aggregation error.
    /// </summary>
    [Fact]
    public void ExtractVersionSection_WhenVersionMissing_ThrowsReleaseNotesAggregationException ()
    {
        const string FileContent = "## Version 1.0.0\n\n### Added\n- x\n";
        Action act = () => ReleaseNotesMarkdownAggregator.ExtractVersionSection(
            FileContent,
            "2.0.0",
            "TestPackage",
            "test.md");
        act.Should().Throw<ReleaseNotesAggregationException>();
    }

    /// <summary>
    ///   Verifies aggregation rejects an empty repository root path.
    /// </summary>
    [Fact]
    public void Aggregator_WhenRepositoryRootEmpty_ThrowsReleaseNotesAggregationException ()
    {
        Action act = () => ReleaseNotesMarkdownAggregator.BuildAggregatedReleaseNotesDocument(string.Empty, "1.0.0");
        act.Should().Throw<ReleaseNotesAggregationException>();
    }

    /// <summary>
    ///   Verifies validation succeeds for the repository's current version across all package release-notes sources.
    /// </summary>
    [Fact]
    public void Validator_ForRepositoryCurrentVersion_DoesNotThrow ()
    {
        string version = RepositoryVersionReader.ReadVersionFromSrcDirectoryBuildProps(RepositoryRootPath);
        Action act = () => ReleaseNotesMarkdownAggregator.ValidateReleaseNotesForVersion(RepositoryRootPath, version);
        act.Should().NotThrow();
    }

    /// <summary>
    ///   Verifies nested bold and inline code in one span is stripped for NuGet plain-text release notes.
    /// </summary>
    [Fact]
    public void PlainTextFormatter_WithBoldAndBackticks_StripsInlineMarkdown ()
    {
        const string MarkdownSection = "- adds **`LocalScheduleDateTimeZone`**, and `Bar`.";
        string plainText = PackageReleaseNotesPlainTextFormatter.ToPlainText(MarkdownSection);
        plainText.Should().Be("- adds LocalScheduleDateTimeZone, and Bar.");
    }

    /// <summary>
    ///   Verifies bold markers alone are removed while surrounding text is preserved.
    /// </summary>
    [Fact]
    public void PlainTextFormatter_WithBoldOnly_StripsBoldMarkers ()
    {
        const string MarkdownSection = "- adds **LocalScheduleDateTimeZone** support.";
        string plainText = PackageReleaseNotesPlainTextFormatter.ToPlainText(MarkdownSection);
        plainText.Should().Be("- adds LocalScheduleDateTimeZone support.");
    }

    /// <summary>
    ///   Verifies inline code backticks alone are removed while surrounding text is preserved.
    /// </summary>
    [Fact]
    public void PlainTextFormatter_WithBackticksOnly_StripsInlineCodeMarkers ()
    {
        const string MarkdownSection = "- baseline entry for version `0.0.5`.";
        string plainText = PackageReleaseNotesPlainTextFormatter.ToPlainText(MarkdownSection);
        plainText.Should().Be("- baseline entry for version 0.0.5.");
    }

    /// <summary>
    ///   Verifies multiple bold and code spans on one line are all stripped.
    /// </summary>
    [Fact]
    public void PlainTextFormatter_WithMultipleSpansOnOneLine_StripsAllSpans ()
    {
        const string MarkdownSection = "- **A** plus **B** and `C`.";
        string plainText = PackageReleaseNotesPlainTextFormatter.ToPlainText(MarkdownSection);
        plainText.Should().Be("- A plus B and C.");
    }

    /// <summary>
    ///   Verifies line breaks are preserved when stripping inline Markdown across multiple lines.
    /// </summary>
    [Fact]
    public void PlainTextFormatter_WithMultiLineInput_PreservesLineBreaksAndStripsMarkdown ()
    {
        const string MarkdownSection = "- first **line**\n- second `line`";
        string plainText = PackageReleaseNotesPlainTextFormatter.ToPlainText(MarkdownSection);
        plainText.Should().Be("- first line\n- second line");
    }

    /// <summary>
    ///   Verifies Windows-style <c>\r\n</c> line endings are normalized and inline Markdown is still stripped.
    /// </summary>
    [Fact]
    public void PlainTextFormatter_WithCrlfLineEndings_NormalizesAndStripsMarkdown ()
    {
        const string MarkdownSection = "- first **line**\r\n- second `line`";
        string plainText = PackageReleaseNotesPlainTextFormatter.ToPlainText(MarkdownSection);
        plainText.Should().Be("- first line\n- second line");
    }

    /// <summary>
    ///   Verifies classic Mac-style <c>\r</c> line endings are normalized and inline Markdown is still stripped.
    /// </summary>
    [Fact]
    public void PlainTextFormatter_WithBareCrLineEndings_NormalizesAndStripsMarkdown ()
    {
        const string MarkdownSection = "- first **line**\r- second `line`";
        string plainText = PackageReleaseNotesPlainTextFormatter.ToPlainText(MarkdownSection);
        plainText.Should().Be("- first line\n- second line");
    }

    /// <summary>
    ///   Verifies text that contains no inline Markdown is returned unchanged (idempotent).
    /// </summary>
    [Fact]
    public void PlainTextFormatter_WithAlreadyPlainText_ReturnsUnchanged ()
    {
        const string PlainSection = "- KZDev.PrimeTime v0.0.6 adds public conversion helpers.";
        string plainText = PackageReleaseNotesPlainTextFormatter.ToPlainText(PlainSection);
        plainText.Should().Be(PlainSection);
    }

    /// <summary>
    ///   Verifies applying plain-text conversion twice yields the same result (idempotent).
    /// </summary>
    [Fact]
    public void PlainTextFormatter_WhenAppliedTwice_ReturnsSameResultAsOnce ()
    {
        const string MarkdownSection = "- adds **`LocalScheduleDateTimeZone`**, and `Bar`.";
        string once = PackageReleaseNotesPlainTextFormatter.ToPlainText(MarkdownSection);
        string twice = PackageReleaseNotesPlainTextFormatter.ToPlainText(once);
        twice.Should().Be(once);
    }

    /// <summary>
    ///   Verifies validation fails when a configured source does not contain the requested version heading.
    /// </summary>
    [Fact]
    public void Validator_WhenConfiguredSourceMissingVersion_ThrowsReleaseNotesAggregationException ()
    {
        string temporaryDirectoryPath = Path.Combine(Path.GetTempPath(), $"primetime-release-notes-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(temporaryDirectoryPath);

        try
        {
            string releaseNotesPath = Path.Combine(temporaryDirectoryPath, "pkg.release-notes.md");
            File.WriteAllText(releaseNotesPath, "## Version 1.0.0\n\n### Added\n- sample\n");
            IReadOnlyList<PackageReleaseNotesSource> sources =
            [
                new("Test.Package", "pkg.release-notes.md")
            ];
            Action act = () => ReleaseNotesMarkdownAggregator.ValidateReleaseNotesForVersion(temporaryDirectoryPath, "2.0.0", sources);
            act.Should().Throw<ReleaseNotesAggregationException>();
        }
        finally
        {
            if (Directory.Exists(temporaryDirectoryPath))
            {
                Directory.Delete(temporaryDirectoryPath, recursive: true);
            }
        }
    }
}
//################################################################################
#endif
