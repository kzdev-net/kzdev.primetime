// Copyright (c) Kevin Zehrer
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

using AwesomeAssertions;

using KZDev.PrimeTime.ReleaseAggregation;

namespace KZDev.PrimeTime.UnitTests;

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
        const string fileContent = "## Version 1.0.0\n\n### Added\n- x\n";
        Action act = () => ReleaseNotesMarkdownAggregator.ExtractVersionSection(
            fileContent,
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
}
//################################################################################
