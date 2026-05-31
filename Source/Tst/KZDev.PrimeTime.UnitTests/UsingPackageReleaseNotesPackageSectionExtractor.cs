// Copyright (c) Kevin Zehrer
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;

using AwesomeAssertions;

using KZDev.PrimeTime.ReleaseAggregation;

namespace KZDev.PrimeTime.UnitTests;

//################################################################################
/// <summary>
///   Validates per-package release-notes section extraction with independent fixture expectations.
/// </summary>
[ExcludeFromCodeCoverage]
public class UsingPackageReleaseNotesPackageSectionExtractor
{
    private const string PackageId = "KZDev.Test.Package";

    private const string ValidReleaseNotesFixtureContent =
        """
        ## Version 1.2.3

        ### Added
        - added item

        ### Changed
        - changed item

        ### Fixed
        - fixed item

        ### Notes
        - notes item

        ### Package
        - **KZDev.Test** v1.2.3 adds **`FeatureName`** support.

        ## Version 0.9.0

        ### Added
        - older item

        ### Changed
        - older change

        ### Fixed
        - older fix

        ### Notes
        - older notes

        ### Package
        - older package summary.
        """;

    private const string ExpectedPackageSectionMarkdown =
        "- **KZDev.Test** v1.2.3 adds **`FeatureName`** support.";

    private const string ExpectedPackageSectionPlainText =
        "- KZDev.Test v1.2.3 adds FeatureName support.";

    /// <summary>
    ///   Verifies plain-text extraction returns an independently specified result for a valid fixture file.
    /// </summary>
    [Fact]
    public void ExtractPlainTextPackageSection_WithValidFixture_ReturnsExpectedPlainText ()
    {
        string releaseNotesPath = WriteTemporaryReleaseNotesFile(ValidReleaseNotesFixtureContent);

        try
        {
            string plainText = PackageReleaseNotesPackageSectionExtractor.ExtractPlainTextPackageSection(
                releaseNotesPath,
                "1.2.3",
                PackageId);

            plainText.Should().Be(ExpectedPackageSectionPlainText);
        }
        finally
        {
            DeleteTemporaryReleaseNotesFile(releaseNotesPath);
        }
    }

    /// <summary>
    ///   Verifies markdown extraction returns an independently specified result for a valid fixture file.
    /// </summary>
    [Fact]
    public void ExtractPackageSectionMarkdown_WithValidFixture_ReturnsExpectedMarkdown ()
    {
        string releaseNotesPath = WriteTemporaryReleaseNotesFile(ValidReleaseNotesFixtureContent);

        try
        {
            string markdown = PackageReleaseNotesPackageSectionExtractor.ExtractPackageSectionMarkdown(
                releaseNotesPath,
                "1.2.3",
                PackageId);

            markdown.Should().Be(ExpectedPackageSectionMarkdown);
        }
        finally
        {
            DeleteTemporaryReleaseNotesFile(releaseNotesPath);
        }
    }

    /// <summary>
    ///   Verifies extraction selects the requested version when multiple version sections exist.
    /// </summary>
    [Fact]
    public void ExtractPlainTextPackageSection_WithMultipleVersions_ReturnsRequestedVersionPackageSection ()
    {
        string releaseNotesPath = WriteTemporaryReleaseNotesFile(ValidReleaseNotesFixtureContent);

        try
        {
            string plainText = PackageReleaseNotesPackageSectionExtractor.ExtractPlainTextPackageSection(
                releaseNotesPath,
                "0.9.0",
                PackageId);

            plainText.Should().Be("- older package summary.");
        }
        finally
        {
            DeleteTemporaryReleaseNotesFile(releaseNotesPath);
        }
    }

    /// <summary>
    ///   Verifies a missing release-notes file surfaces <see cref="FileNotFoundException"/>.
    /// </summary>
    [Fact]
    public void ExtractPlainTextPackageSection_WhenFileNotFound_ThrowsFileNotFoundException ()
    {
        string missingPath = Path.Combine(Path.GetTempPath(), $"missing-release-notes-{Guid.NewGuid():N}.md");

        Action act = () => PackageReleaseNotesPackageSectionExtractor.ExtractPlainTextPackageSection(
            missingPath,
            "1.2.3",
            PackageId);

        act.Should().Throw<FileNotFoundException>();
    }

    /// <summary>
    ///   Verifies a missing version heading surfaces <see cref="InvalidOperationException"/>.
    /// </summary>
    [Fact]
    public void ExtractPlainTextPackageSection_WhenVersionMissing_ThrowsInvalidOperationException ()
    {
        const string FileContent =
            """
            ## Version 1.0.0

            ### Added
            - sample

            ### Changed
            - sample

            ### Fixed
            - sample

            ### Notes
            - sample

            ### Package
            - package summary.
            """;
        string releaseNotesPath = WriteTemporaryReleaseNotesFile(FileContent);

        try
        {
            Action act = () => PackageReleaseNotesPackageSectionExtractor.ExtractPlainTextPackageSection(
                releaseNotesPath,
                "2.0.0",
                PackageId);

            act.Should().Throw<InvalidOperationException>();
        }
        finally
        {
            DeleteTemporaryReleaseNotesFile(releaseNotesPath);
        }
    }

    /// <summary>
    ///   Verifies a missing required category heading surfaces <see cref="InvalidOperationException"/>.
    /// </summary>
    [Fact]
    public void ExtractPlainTextPackageSection_WhenCategoryHeadingMissing_ThrowsInvalidOperationException ()
    {
        const string FileContent =
            """
            ## Version 1.0.0

            ### Added
            - sample

            ### Package
            - package summary.
            """;
        string releaseNotesPath = WriteTemporaryReleaseNotesFile(FileContent);

        try
        {
            Action act = () => PackageReleaseNotesPackageSectionExtractor.ExtractPlainTextPackageSection(
                releaseNotesPath,
                "1.0.0",
                PackageId);

            act.Should().Throw<InvalidOperationException>();
        }
        finally
        {
            DeleteTemporaryReleaseNotesFile(releaseNotesPath);
        }
    }

    /// <summary>
    ///   Verifies an empty <c>### Package</c> section surfaces <see cref="InvalidOperationException"/>.
    /// </summary>
    [Fact]
    public void ExtractPlainTextPackageSection_WhenPackageSectionEmpty_ThrowsInvalidOperationException ()
    {
        const string FileContent =
            """
            ## Version 1.0.0

            ### Added
            - sample

            ### Changed
            - sample

            ### Fixed
            - sample

            ### Notes
            - sample

            ### Package

            """;
        string releaseNotesPath = WriteTemporaryReleaseNotesFile(FileContent);

        try
        {
            Action act = () => PackageReleaseNotesPackageSectionExtractor.ExtractPlainTextPackageSection(
                releaseNotesPath,
                "1.0.0",
                PackageId);

            act.Should().Throw<InvalidOperationException>();
        }
        finally
        {
            DeleteTemporaryReleaseNotesFile(releaseNotesPath);
        }
    }

    /// <summary>
    ///   Verifies category headings that appear out of required order surface <see cref="InvalidOperationException"/>.
    /// </summary>
    [Fact]
    public void ExtractPlainTextPackageSection_WhenCategoryHeadingsOutOfOrder_ThrowsInvalidOperationException ()
    {
        const string FileContent =
            """
            ## Version 1.0.0

            ### Added
            - sample

            ### Fixed
            - fixed before changed

            ### Changed
            - sample

            ### Notes
            - sample

            ### Package
            - package summary.
            """;
        string releaseNotesPath = WriteTemporaryReleaseNotesFile(FileContent);

        try
        {
            Action act = () => PackageReleaseNotesPackageSectionExtractor.ExtractPlainTextPackageSection(
                releaseNotesPath,
                "1.0.0",
                PackageId);

            act.Should().Throw<InvalidOperationException>()
                .WithMessage("*not in the required heading order*");
        }
        finally
        {
            DeleteTemporaryReleaseNotesFile(releaseNotesPath);
        }
    }

    /// <summary>
    ///   Writes fixture content to a temporary release-notes file and returns its full path.
    /// </summary>
    /// <param name="fileContent">
    ///   Markdown content to write.
    /// </param>
    /// <returns>
    ///   The full path to the temporary file.
    /// </returns>
    private static string WriteTemporaryReleaseNotesFile (string fileContent)
    {
        string releaseNotesPath = Path.Combine(Path.GetTempPath(), $"release-notes-{Guid.NewGuid():N}.md");
        File.WriteAllText(releaseNotesPath, fileContent);
        return releaseNotesPath;
    }

    /// <summary>
    ///   Deletes a temporary release-notes file when it exists.
    /// </summary>
    /// <param name="releaseNotesPath">
    ///   The full path to the temporary file.
    /// </param>
    private static void DeleteTemporaryReleaseNotesFile (string releaseNotesPath)
    {
        if (File.Exists(releaseNotesPath))
        {
            File.Delete(releaseNotesPath);
        }
    }
}
//################################################################################
