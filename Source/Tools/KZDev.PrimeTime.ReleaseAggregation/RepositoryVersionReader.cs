// Copyright (c) Kevin Zehrer
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Text.RegularExpressions;

namespace KZDev.PrimeTime.ReleaseAggregation;

/// <summary>
///   Reads the solution <c>Version</c> from <c>Source/Src/Directory.Build.props</c>.
/// </summary>
public static class RepositoryVersionReader
{
    private static readonly Regex VersionElementRegex = new(
        @"<Version>\s*(?<v>[^<\s]+)\s*</Version>",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    /// <summary>
    ///   Returns the &lt;Version&gt; value from <c>Source/Src/Directory.Build.props</c> under the repository root.
    /// </summary>
    /// <param name="repositoryRootPath">
    ///   Full path to the repository root (directory that contains <c>Source</c> and <c>README.md</c>).
    /// </param>
    /// <returns>
    ///   The version string (for example <c>0.0.5</c>).
    /// </returns>
    /// <exception cref="ReleaseNotesAggregationException">
    ///   Thrown when the file is missing, unreadable, or does not contain a &lt;Version&gt; element.
    /// </exception>
    public static string ReadVersionFromSrcDirectoryBuildProps (string repositoryRootPath)
    {
        string propsPath = Path.Combine(repositoryRootPath, "Source", "Src", "Directory.Build.props");
        if (!File.Exists(propsPath))
        {
            throw new ReleaseNotesAggregationException(
                $"Could not find '{propsPath}'. Expected repository layout with Source/Src/Directory.Build.props.");
        }

        string content;
        try
        {
            content = File.ReadAllText(propsPath);
        }
        catch (Exception ex)
        {
            throw new ReleaseNotesAggregationException($"Failed to read version from '{propsPath}'.", ex);
        }

        Match match = VersionElementRegex.Match(content);
        if (!match.Success)
        {
            throw new ReleaseNotesAggregationException(
                $"No <Version> element found in '{propsPath}'.");
        }

        return match.Groups["v"].Value;
    }
}
