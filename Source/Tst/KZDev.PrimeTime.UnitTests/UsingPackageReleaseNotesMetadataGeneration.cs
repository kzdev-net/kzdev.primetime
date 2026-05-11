// Copyright (c) Kevin Zehrer
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using System.Diagnostics;
using System.Xml.Linq;

using AwesomeAssertions;

namespace KZDev.PrimeTime.UnitTests;

//################################################################################
/// <summary>
///   Validates package release-note metadata generation for all publishable PrimeTime packages.
/// </summary>
[ExcludeFromCodeCoverage]
public class UsingPackageReleaseNotesMetadataGeneration
{
    private static readonly IReadOnlyDictionary<string, string> PackageProjectRelativePaths =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["KZDev.PrimeTime"] = Path.Combine("Source", "Src", "KZDev.PrimeTime", "KZDev.PrimeTime.csproj"),
            ["KZDev.SystemClock.PrimeTime"] = Path.Combine("Source", "Src", "KZDev.SystemClock.PrimeTime", "KZDev.SystemClock.PrimeTime.csproj"),
            ["KZDev.PrimeTime.Testing"] = Path.Combine("Source", "Src", "KZDev.PrimeTime.Testing", "KZDev.PrimeTime.Testing.csproj"),
            ["KZDev.SystemClock.PrimeTime.Testing"] = Path.Combine("Source", "Src", "KZDev.SystemClock.PrimeTime.Testing", "KZDev.SystemClock.PrimeTime.Testing.csproj")
        };

    private static readonly IReadOnlyDictionary<string, string> PackageReleaseNotesRelativePaths =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["KZDev.PrimeTime"] = Path.Combine("Source", "Docs", "Notes", "KZDev.PrimeTime.release-notes.md"),
            ["KZDev.SystemClock.PrimeTime"] = Path.Combine("Source", "Docs", "Notes", "KZDev.SystemClock.PrimeTime.release-notes.md"),
            ["KZDev.PrimeTime.Testing"] = Path.Combine("Source", "Docs", "Notes", "KZDev.PrimeTime.Testing.release-notes.md"),
            ["KZDev.SystemClock.PrimeTime.Testing"] =
                Path.Combine("Source", "Docs", "Notes", "KZDev.SystemClock.PrimeTime.Testing.release-notes.md")
        };

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
    ///   Reads the packaging <c>Version</c> from <c>Source/Src/Directory.Build.props</c>, matching the
    ///   value MSBuild passes to <c>ExtractPackageReleaseNotesTask</c> as <c>PackageVersion</c>.
    /// </summary>
    /// <returns>
    ///   The trimmed version string (for example <c>0.0.6</c>).
    /// </returns>
    private static string GetPackagingVersionFromSourceDirectoryBuildProps ()
    {
        string propsPath = Path.Combine(RepositoryRootPath, "Source", "Src", "Directory.Build.props");
        XDocument document = XDocument.Load(propsPath, LoadOptions.PreserveWhitespace);
        XElement? versionElement = document.Root?
            .Elements("PropertyGroup")
            .SelectMany(propertyGroup => propertyGroup.Elements("Version"))
            .FirstOrDefault();
        if (versionElement is null || string.IsNullOrWhiteSpace(versionElement.Value))
        {
            throw new InvalidOperationException($"Could not read non-empty Version from '{propsPath}'.");
        }

        return versionElement.Value.Trim();
    }

    /// <summary>
    ///   Extracts the <c>### Package</c> body for <paramref name="packageVersion"/> from a per-package
    ///   release-notes markdown file, using the same rules as <c>ExtractPackageReleaseNotesTask</c> in
    ///   <c>Source/Src/Directory.Build.props</c>.
    /// </summary>
    /// <param name="releaseNotesFilePath">
    ///   Absolute path to the <c>*.release-notes.md</c> file.
    /// </param>
    /// <param name="packageVersion">
    ///   Version whose <c>## Version</c> section is read (must match packaging version).
    /// </param>
    /// <param name="packageId">
    ///   Package id used only for exception messages.
    /// </param>
    /// <returns>
    ///   Trimmed <c>### Package</c> section text assigned to NuGet <c>PackageReleaseNotes</c>.
    /// </returns>
    private static string ExtractPackageReleaseNotesFromReleaseNotesFile (
        string releaseNotesFilePath,
        string packageVersion,
        string packageId)
    {
        string normalizedReleaseNotesPath = Path.GetFullPath(releaseNotesFilePath);
        if (!File.Exists(normalizedReleaseNotesPath))
        {
            throw new FileNotFoundException(
                $"Release notes file does not exist for package '{packageId}': '{normalizedReleaseNotesPath}'.",
                normalizedReleaseNotesPath);
        }

        string fileContent = File.ReadAllText(normalizedReleaseNotesPath).Replace("\r\n", "\n");
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

        string[] expectedHeadings = ["### Added", "### Changed", "### Fixed", "### Notes", "### Package"];
        int previousHeadingIndex = -1;
        foreach (string heading in expectedHeadings)
        {
            int headingIndex = Array.FindIndex(
                versionLines,
                line => string.Equals(line.TrimEnd(), heading, StringComparison.Ordinal));
            if (headingIndex < 0)
            {
                throw new InvalidOperationException(
                    $"Version section '{normalizedVersionHeading}' for package '{packageId}' is missing required heading '{heading}'.");
            }

            if (headingIndex <= previousHeadingIndex)
            {
                throw new InvalidOperationException(
                    $"Version section '{normalizedVersionHeading}' for package '{packageId}' is not in the required heading order.");
            }

            previousHeadingIndex = headingIndex;
        }

        int packageHeadingIndex = Array.FindIndex(
            versionLines,
            line => string.Equals(line.TrimEnd(), "### Package", StringComparison.Ordinal));
        int packageContentStartIndex = packageHeadingIndex + 1;
        while (packageContentStartIndex < versionLines.Length && string.IsNullOrWhiteSpace(versionLines[packageContentStartIndex]))
        {
            packageContentStartIndex++;
        }

        int packageContentEndIndex = versionLines.Length;
        for (int index = packageContentStartIndex; index < versionLines.Length; index++)
        {
            if (versionLines[index].StartsWith("### ", StringComparison.Ordinal))
            {
                packageContentEndIndex = index;
                break;
            }
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

    /// <summary>
    ///   Runs dotnet msbuild to get PackageReleaseNotes for a package project.
    /// </summary>
    /// <param name="projectPath">
    ///   The full package project path.
    /// </param>
    /// <returns>
    ///   The generated PackageReleaseNotes value.
    /// </returns>
    private static string GetPackageReleaseNotesFromMsbuild (string projectPath)
    {
        ProcessStartInfo processStartInfo = new()
        {
            FileName = "dotnet",
            WorkingDirectory = RepositoryRootPath,
            Arguments =
                $"msbuild \"{projectPath}\" -nologo -t:GenerateNuspec -p:Configuration=Package -getProperty:PackageReleaseNotes",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };

        using Process? process = Process.Start(processStartInfo);
        process.Should().NotBeNull("expected dotnet process to start");
        process!.WaitForExit();

        string standardOutput = process.StandardOutput.ReadToEnd();
        string standardError = process.StandardError.ReadToEnd();
        process.ExitCode.Should().Be(0,
            $"expected msbuild to succeed for '{projectPath}'. stderr: {standardError}");

        string[] outputLines = standardOutput.Replace("\r\n", "\n").Split('\n');
        for (int index = outputLines.Length - 1; index >= 0; index--)
        {
            string candidateLine = outputLines[index].Trim();
            if (!string.IsNullOrWhiteSpace(candidateLine))
            {
                return candidateLine;
            }
        }

        throw new InvalidOperationException($"Could not find PackageReleaseNotes output for '{projectPath}'.");
    }

    /// <summary>
    ///   Verifies package metadata generation returns non-empty PackageReleaseNotes for each package.
    /// </summary>
    [Fact]
    public void PackageMetadataGeneration_ForEachPackage_GeneratesNonEmptyPackageReleaseNotes ()
    {
        foreach (KeyValuePair<string, string> packageProject in PackageProjectRelativePaths)
        {
            string fullProjectPath = Path.Combine(RepositoryRootPath, packageProject.Value);
            string packageReleaseNotes = GetPackageReleaseNotesFromMsbuild(fullProjectPath);
            packageReleaseNotes.Should().NotBeNullOrWhiteSpace(
                $"expected PackageReleaseNotes value for package '{packageProject.Key}'");
        }
    }

    /// <summary>
    ///   Verifies each package's generated <c>PackageReleaseNotes</c> matches the <c>### Package</c> body
    ///   for the current packaging version in that package's release-notes markdown file (same extraction
    ///   rules as the MSBuild task), so the test does not hard-code release-note prose.
    /// </summary>
    [Fact]
    public void PackageMetadataGeneration_ForEachPackage_MapsToExpectedPackageSection ()
    {
        string packagingVersion = GetPackagingVersionFromSourceDirectoryBuildProps();
        foreach (KeyValuePair<string, string> packageProject in PackageProjectRelativePaths)
        {
            string packageId = packageProject.Key;
            string fullProjectPath = Path.Combine(RepositoryRootPath, packageProject.Value);
            string releaseNotesRelativePath = PackageReleaseNotesRelativePaths[packageId];
            string fullReleaseNotesPath = Path.Combine(RepositoryRootPath, releaseNotesRelativePath);
            string expectedPackageReleaseNotes = ExtractPackageReleaseNotesFromReleaseNotesFile(
                fullReleaseNotesPath,
                packagingVersion,
                packageId);
            string packageReleaseNotes = GetPackageReleaseNotesFromMsbuild(fullProjectPath);
            packageReleaseNotes.Should().Be(expectedPackageReleaseNotes,
                $"expected package '{packageId}' to map to its own ### Package summary for version {packagingVersion}");
        }
    }
}
//################################################################################
