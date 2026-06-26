// Copyright (c) Kevin Zehrer
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using System.Runtime.ExceptionServices;

namespace KZDev.PrimeTime.Tests;

//################################################################################
/// <summary>
///   Captures assertion failures raised on timer or thread-pool callback threads so tests can
///   rethrow them on the test thread during normal assertion steps.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed class CallbackAssertionCapture : IDisposable
{
    //----------------------------------------------------------------------------
    /// <summary>
    ///   The first captured failure, if any.
    /// </summary>
    private ExceptionDispatchInfo? _firstFailure;
    //----------------------------------------------------------------------------
    /// <summary>
    ///   Records <paramref name="exception"/> as a callback assertion failure when no failure was
    ///   captured yet.
    /// </summary>
    /// <param name="exception">The exception to capture.</param>
    private void RecordFailure (Exception exception)
    {
        if (exception is null)
        {
            throw new ArgumentNullException(nameof(exception));
        }

        ExceptionDispatchInfo captured = ExceptionDispatchInfo.Capture(exception);
        Interlocked.CompareExchange(ref _firstFailure, captured, null);
    }
    //----------------------------------------------------------------------------
    /// <summary>
    ///   Determines whether <paramref name="exception"/> represents a test assertion failure rather
    ///   than an operational or cancellation fault.
    /// </summary>
    /// <param name="exception">The exception to classify.</param>
    /// <returns>
    ///   <c>true</c> when the exception should be captured for later rethrow on the test thread;
    ///   otherwise, <c>false</c>.
    /// </returns>
    private static bool IsAssertionFailure (Exception exception)
    {
        Type exceptionType = exception.GetType();
        string typeFullName = exceptionType.FullName ?? exceptionType.Name;
        if (typeFullName.StartsWith("Xunit.Sdk.", StringComparison.Ordinal))
        {
            return true;
        }

        if (exceptionType.Name.EndsWith("Exception", StringComparison.Ordinal)
            && typeFullName.IndexOf("Assertions", StringComparison.Ordinal) >= 0)
        {
            return true;
        }

        return false;
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Gets a value indicating whether an assertion failure was captured.
    /// </summary>
    public bool HasFailure => Volatile.Read(ref _firstFailure) is not null;

    //----------------------------------------------------------------------------
    /// <summary>
    ///   Runs <paramref name="assertion"/> and records the first assertion failure it throws.
    /// </summary>
    /// <param name="assertion">Assertion logic to execute on a callback thread.</param>
    /// <remarks>
    ///   <see cref="OperationCanceledException"/> and other non-assertion exceptions propagate
    ///   immediately so cancellation and unexpected faults are not deferred.
    /// </remarks>
    public void Record (Action assertion)
    {
        if (assertion is null)
        {
            throw new ArgumentNullException(nameof(assertion));
        }

        try
        {
            assertion();
        }
        catch (Exception ex)
        {
            if (ex is OperationCanceledException || !IsAssertionFailure(ex))
            {
                throw;
            }

            RecordFailure(ex);
        }
    }
    //----------------------------------------------------------------------------
    /// <summary>
    ///   Rethrows the first captured assertion failure on the calling thread.
    /// </summary>
    public void ThrowIfRecorded ()
    {
        ExceptionDispatchInfo? failure = Interlocked.Exchange(ref _firstFailure, null);
        failure?.Throw();
    }
    //----------------------------------------------------------------------------

    #region IDisposable Implementation

    //----------------------------------------------------------------------------
    /// <summary>
    ///   Rethrows any captured assertion failure that was not already surfaced by
    ///   <see cref="ThrowIfRecorded"/>.
    /// </summary>
    public void Dispose ()
    {
        ThrowIfRecorded();
    }
    //----------------------------------------------------------------------------

    #endregion IDisposable Implementation
}
//################################################################################
