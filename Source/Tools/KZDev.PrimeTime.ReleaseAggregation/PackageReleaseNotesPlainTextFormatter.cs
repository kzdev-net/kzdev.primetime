// Copyright (c) Kevin Zehrer
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Text.RegularExpressions;

namespace KZDev.PrimeTime.ReleaseAggregation;

/// <summary>
///   Converts inline Markdown in a <c>### Package</c> release-notes excerpt to plain text for NuGet
///   <c>PackageReleaseNotes</c> / nuspec <c>releaseNotes</c>, which NuGet.org does not render as Markdown.
/// </summary>
public static class PackageReleaseNotesPlainTextFormatter
{
    /// <summary>
    ///   Upper bound on repeated regex replace passes per pattern. Each pass removes all non-overlapping
    ///   matches of that pattern; additional passes peel nested markup (for example <c>**`TypeName`**</c>)
    ///   where bold and code delimiters overlap and one pattern cannot finish in a single pass.
    /// </summary>
    private const int MaxInlineMarkdownStripPasses = 8;

    private static readonly TimeSpan RegexMatchTimeout = TimeSpan.FromSeconds(1);

    private static readonly Regex BoldSpanRegex = new(@"\*\*(.+?)\*\*", RegexOptions.CultureInvariant, RegexMatchTimeout);

    private static readonly Regex InlineCodeSpanRegex = new(@"`([^`]+)`", RegexOptions.CultureInvariant, RegexMatchTimeout);

    /// <summary>
    ///   Strips common inline Markdown emphasis from <paramref name="markdownSectionText"/> while preserving
    ///   line breaks and list punctuation. Normalizes <c>\r\n</c> and standalone <c>\r</c> line endings to <c>\n</c> before stripping.
    /// </summary>
    /// <param name="markdownSectionText">
    ///   Text extracted from the <c>### Package</c> section of a per-package release-notes file.
    /// </param>
    /// <returns>
    ///   Plain text suitable for NuGet package metadata.
    /// </returns>
    public static string ToPlainText (string markdownSectionText)
    {
        if (string.IsNullOrEmpty(markdownSectionText))
        {
            return markdownSectionText;
        }

        string normalized = markdownSectionText.Replace("\r\n", "\n").Replace("\r", "\n");
        string result = StripInlineSpans(normalized, BoldSpanRegex);
        result = StripInlineSpans(result, InlineCodeSpanRegex);
        return result;
    }

    private static string StripInlineSpans (string input, Regex spanRegex)
    {
        string result = input;
        int pass = 0;
        while (pass < MaxInlineMarkdownStripPasses)
        {
            string next = spanRegex.Replace(result, "$1");
            if (string.Equals(next, result, StringComparison.Ordinal))
            {
                break;
            }

            result = next;
            pass++;
        }

        return result;
    }
}
