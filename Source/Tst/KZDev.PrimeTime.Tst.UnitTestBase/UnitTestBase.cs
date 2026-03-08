// Copyright (c) Kevin Zehrer
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using Xunit;

namespace KZDev.PrimeTime.Tests;

/// <summary>
///   The base class for all unit tests.
/// </summary>
public abstract class UnitTestBase : TestBase
{
    /// <summary>
    ///   Initializes a new instance of the <see cref="UnitTestBase"/> class.
    /// </summary>
    /// <param name="xUnitTestOutputHelper">
    ///   The Xunit test output helper that can be used to output test messages.
    /// </param>
    protected UnitTestBase (ITestOutputHelper xUnitTestOutputHelper)
        : base(xUnitTestOutputHelper)
    {
    }
}
