// Copyright (c) Kevin Zehrer
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using KZDev.PrimeTime.ReleaseAggregation;

namespace KZDev.PrimeTime.ReleaseAggregation.Cli;

/// <summary>
///   Command-line entry point for release-assist aggregation.
/// </summary>
internal static class Program
{
    /// <summary>
    ///   Application entry point.
    /// </summary>
    /// <param name="args">
    ///   Command-line arguments.
    /// </param>
    /// <returns>
    ///   Process exit code (0 success, 1 usage error, 2 aggregation error).
    /// </returns>
    private static int Main (string[] args)
    {
        if (args.Length == 0)
        {
            Console.Error.WriteLine(
                "Usage: KZDev.PrimeTime.ReleaseAggregation.Cli aggregate --repository-root <path> [--version <x.y.z>] --output <file.md>");
            Console.Error.WriteLine(
                "       KZDev.PrimeTime.ReleaseAggregation.Cli validate --repository-root <path> [--version <x.y.z>]");
            Console.Error.WriteLine(
                "       If --version is omitted, reads <Version> from Source/Src/Directory.Build.props.");
            return 1;
        }

        if (string.Equals(args[0], "help", StringComparison.OrdinalIgnoreCase)
            || string.Equals(args[0], "--help", StringComparison.OrdinalIgnoreCase))
        {
            Console.Error.WriteLine(
                "Usage: KZDev.PrimeTime.ReleaseAggregation.Cli aggregate --repository-root <path> [--version <x.y.z>] --output <file.md>");
            Console.Error.WriteLine(
                "       KZDev.PrimeTime.ReleaseAggregation.Cli validate --repository-root <path> [--version <x.y.z>]");
            Console.Error.WriteLine(
                "       If --version is omitted, reads <Version> from Source/Src/Directory.Build.props.");
            return 0;
        }

        if (!string.Equals(args[0], "aggregate", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(args[0], "validate", StringComparison.OrdinalIgnoreCase))
        {
            Console.Error.WriteLine($"Unknown command '{args[0]}'.");
            return 1;
        }

        try
        {
            string? repositoryRoot = null;
            string? version = null;
            string? outputPath = null;

            for (int index = 1; index < args.Length; index++)
            {
                string arg = args[index];
                if (string.Equals(arg, "--repository-root", StringComparison.OrdinalIgnoreCase))
                {
                    repositoryRoot = RequireNext(args, ref index, "--repository-root");
                }
                else if (string.Equals(arg, "--version", StringComparison.OrdinalIgnoreCase))
                {
                    version = RequireNext(args, ref index, "--version");
                }
                else if (string.Equals(arg, "--output", StringComparison.OrdinalIgnoreCase))
                {
                    outputPath = RequireNext(args, ref index, "--output");
                }
                else
                {
                    Console.Error.WriteLine($"Unexpected argument '{arg}'.");
                    return 1;
                }
            }

            if (string.IsNullOrWhiteSpace(repositoryRoot))
            {
                Console.Error.WriteLine("--repository-root is required.");
                return 1;
            }

            bool isValidateCommand = string.Equals(args[0], "validate", StringComparison.OrdinalIgnoreCase);
            if (!isValidateCommand && string.IsNullOrWhiteSpace(outputPath))
            {
                Console.Error.WriteLine("--output is required.");
                return 1;
            }

            if (string.IsNullOrWhiteSpace(version))
            {
                version = RepositoryVersionReader.ReadVersionFromSrcDirectoryBuildProps(repositoryRoot);
            }

            if (isValidateCommand)
            {
                ReleaseNotesMarkdownAggregator.ValidateReleaseNotesForVersion(repositoryRoot, version);
                Console.WriteLine(
                    $"Validated release notes for version '{version}' across {ReleaseNotesMarkdownAggregator.PrimeTimePackageReleaseNotesSources.Count} packages.");
                return 0;
            }

            ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
            string aggregateOutputPath = outputPath;
            string markdown = ReleaseNotesMarkdownAggregator.BuildAggregatedReleaseNotesDocument(repositoryRoot, version);
            string? outputDirectory = Path.GetDirectoryName(Path.GetFullPath(aggregateOutputPath));
            if (!string.IsNullOrEmpty(outputDirectory))
            {
                Directory.CreateDirectory(outputDirectory);
            }

            File.WriteAllText(aggregateOutputPath, markdown);
            return 0;
        }
        catch (ReleaseNotesAggregationException ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 2;
        }
    }

    /// <summary>
    ///   Returns the argument that follows an option token (for example <c>--output</c>).
    /// </summary>
    /// <param name="args">
    ///   The full argument array.
    /// </param>
    /// <param name="index">
    ///   The current index of the option token; incremented to the value token.
    /// </param>
    /// <param name="optionName">
    ///   Option name for error messages.
    /// </param>
    /// <returns>
    ///   The option value.
    /// </returns>
    /// <exception cref="ReleaseNotesAggregationException">
    ///   Thrown when no value follows the option.
    /// </exception>
    private static string RequireNext (string[] args, ref int index, string optionName)
    {
        if (index + 1 >= args.Length)
        {
            throw new ReleaseNotesAggregationException($"Missing value for {optionName}.");
        }

        index++;
        return args[index];
    }
}
