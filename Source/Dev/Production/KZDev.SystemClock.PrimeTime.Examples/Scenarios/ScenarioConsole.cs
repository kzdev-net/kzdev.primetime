// Copyright (c) Kevin Zehrer
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

namespace KZDev.SystemClock.PrimeTime.Examples.Scenarios;

/// <summary>
/// Provides standardized scenario console output formatting.
/// </summary>
internal static class ScenarioConsole
{
    /// <summary>
    /// Writes a message to the console prefixed with the current local timestamp.
    /// </summary>
    /// <param name="message">
    /// The message to write.
    /// </param>
    public static void WriteLine(string message)
    {
        DateTimeOffset now = DateTimeOffset.Now;
        Console.WriteLine($"[{now:HH:mm:ss.ff}] {message}");
    }
}
