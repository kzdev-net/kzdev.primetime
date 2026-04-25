// Copyright (c) Kevin Zehrer
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

namespace KZDev.PrimeTime.Examples.Helpers;

/// <summary>
/// Parses command-line arguments into a demo runtime mode.
/// </summary>
public static class DemoRunModeParser
{
    /// <summary>
    /// Parses command-line arguments and resolves the runtime mode.
    /// </summary>
    /// <param name="args">
    /// Command-line arguments passed to the application.
    /// </param>
    /// <returns>
    /// The selected <see cref="DemoRunMode"/> value.
    /// </returns>
    public static DemoRunMode Parse(string[] args)
    {
        if (args is null)
        {
            return DemoRunMode.Short;
        }

        foreach (string argument in args)
        {
            if (string.Equals(argument, "--long", StringComparison.OrdinalIgnoreCase))
            {
                return DemoRunMode.Long;
            }
        }

        return DemoRunMode.Short;
    }
}
