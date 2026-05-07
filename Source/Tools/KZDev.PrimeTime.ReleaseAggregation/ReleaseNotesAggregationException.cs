// Copyright (c) Kevin Zehrer
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

namespace KZDev.PrimeTime.ReleaseAggregation;

/// <summary>
///   Thrown when release-note aggregation cannot produce a valid document (for example a missing version section).
/// </summary>
public sealed class ReleaseNotesAggregationException : Exception
{
    /// <summary>
    ///   Initializes a new instance of the <see cref="ReleaseNotesAggregationException" /> class.
    /// </summary>
    /// <param name="message">
    ///   The error message.
    /// </param>
    public ReleaseNotesAggregationException (string message)
        : base(message)
    {
    }

    /// <summary>
    ///   Initializes a new instance of the <see cref="ReleaseNotesAggregationException" /> class.
    /// </summary>
    /// <param name="message">
    ///   The error message.
    /// </param>
    /// <param name="innerException">
    ///   The exception that caused this exception.
    /// </param>
    public ReleaseNotesAggregationException (string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
