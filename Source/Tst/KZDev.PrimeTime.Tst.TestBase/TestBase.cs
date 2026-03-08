// Copyright (c) Kevin Zehrer
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Diagnostics;
using Xunit;

namespace KZDev.PrimeTime.Tests;

/// <summary>
///   The base class for all programmatic tests.
/// </summary>
public abstract class TestBase
{
    /// <summary>
    ///   The test output helper that can be used to output test messages.
    /// </summary>
    protected ITestOutputHelper XUnitTestOutputHelper { [DebuggerStepThrough] get; }

    /// <summary>
    ///   The default time box for explicit tests.
    /// </summary>
    protected static readonly TimeSpan DefaultExplicitTestTimeBox = TimeSpan.FromMinutes(5);

    /// <summary>
    ///   Writes a line of text to the output.
    /// </summary>
    /// <param name="message">The message to write to the output.</param>
    protected void TestWriteLine (string message)
    {
        XUnitTestOutputHelper.WriteLine(message);
    }

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
}
