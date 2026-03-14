// Copyright (c) Kevin Zehrer. All rights reserved.
// This file is part of the PrimeTime project.

using System.Diagnostics;

using Xunit;

namespace KZDev.PrimeTime.Tests;

/// <summary>
///   The base class for all programmatic tests.
/// </summary>
public abstract class TestBase
{
    /// <summary>
    ///   The default time box for explicit tests.
    /// </summary>
    protected static readonly TimeSpan DefaultExplicitTestTimeBox = TimeSpan.FromMinutes(5);

    /// <summary>
    ///   The test output helper that can be used to output test messages.
    /// </summary>
    protected ITestOutputHelper XUnitTestOutputHelper { [DebuggerStepThrough] get; }

    #region Constructors/Finalizers

    /// <summary>
    ///   Initializes a new instance of the <see cref="TestBase"/> class.
    /// </summary>
    /// <param name="xUnitTestOutputHelper">
    ///   The Xunit test output helper that can be used to output test messages.
    /// </param>
    protected TestBase (ITestOutputHelper xUnitTestOutputHelper)
    {
        Debug.Assert(xUnitTestOutputHelper is not null, "xUnitTestOutputHelper is null");
        XUnitTestOutputHelper = xUnitTestOutputHelper;
    }

    #endregion Constructors/Finalizers

    /// <summary>
    ///   Writes a line of text to the output.
    /// </summary>
    /// <param name="message">The message to write to the output.</param>
    protected void TestWriteLine (string message)
    {
        XUnitTestOutputHelper.WriteLine(message);
    }
}
