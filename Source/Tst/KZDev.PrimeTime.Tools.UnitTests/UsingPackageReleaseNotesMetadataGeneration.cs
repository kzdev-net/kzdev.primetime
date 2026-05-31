// Copyright (c) Kevin Zehrer
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using System.Diagnostics;

using AwesomeAssertions;

namespace KZDev.PrimeTime.Tools.UnitTests;

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
            ["KZDev.PrimeTime.Testing"] = Path.Combine("Source", "Src", "Testing", "KZDev.PrimeTime.Testing", "KZDev.PrimeTime.Testing.csproj"),
            ["KZDev.SystemClock.PrimeTime.Testing"] = Path.Combine("Source", "Src", "Testing", "KZDev.SystemClock.PrimeTime.Testing", "KZDev.SystemClock.PrimeTime.Testing.csproj")
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
    ///   Extraction correctness is covered by <see cref="UsingPackageReleaseNotesPackageSectionExtractor"/>.
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
}
//################################################################################
