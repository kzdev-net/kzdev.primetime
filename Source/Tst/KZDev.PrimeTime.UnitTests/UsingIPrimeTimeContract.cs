// Copyright (c) Kevin Zehrer
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using AwesomeAssertions;
using KZDev.PrimeTime;
using KZDev.PrimeTime.Tests;

namespace KZDev.PrimeTime.UnitTests;

//################################################################################
/// <summary>
///   Unit tests for the <see cref="IPrimeTime"/> contract on the NodaTime implementation assembly
///   (<c>KZDev.PrimeTime</c>). The BCL subset under <c>KZDev.SystemClock.PrimeTime</c> is covered by
///   <see cref="UsingSystemClockSubsetContract"/>.
/// </summary>
[ExcludeFromCodeCoverage]
public class UsingIPrimeTimeContract : UnitTestBase
{
    #region Constructors/Finalizers

    //----------------------------------------------------------------------------
    /// <summary>
    ///   Initializes a new instance of the <see cref="UsingIPrimeTimeContract"/> class.
    /// </summary>
    /// <param name="xUnitTestOutputHelper">
    ///   The Xunit test output helper that can be used to output test messages.
    /// </param>
    public UsingIPrimeTimeContract (ITestOutputHelper xUnitTestOutputHelper)
        : base(xUnitTestOutputHelper)
    {
    }
    //----------------------------------------------------------------------------

    #endregion Constructors/Finalizers

    //----------------------------------------------------------------------------
    /// <summary>
    ///   Resolves the runtime <see cref="Type"/> for <see cref="IPrimeTime"/> from the assembly referenced by this test project.
    /// </summary>
    /// <returns>
    ///   The <see cref="Type"/> of <see cref="IPrimeTime"/>.
    /// </returns>
    private static Type GetIPrimeTimeType ()
    {
        return typeof(IPrimeTime);
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies that the referenced PrimeTime assembly exposes the <see cref="IPrimeTime"/> interface type.
    /// </summary>
    [Fact]
    public void ContractAssembly_ExposesIPrimeTimeInterface ()
    {
        Type primeTimeInterfaceType = GetIPrimeTimeType();
        primeTimeInterfaceType.IsInterface.Should().BeTrue();
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies that <see cref="IPrimeTime"/> declares Sleep and DelayAsync methods.
    /// </summary>
    [Fact]
    public void IPrimeTime_DeclaresDelayMethods ()
    {
        Type primeTimeInterfaceType = GetIPrimeTimeType();
        MethodInfo[] methods = primeTimeInterfaceType.GetMethods();
        bool hasSleep = methods.Any(m => m.Name == "Sleep");
        bool hasDelayAsync = methods.Any(m => m.Name == "DelayAsync");
        hasSleep.Should().BeTrue();
        hasDelayAsync.Should().BeTrue();
    }
    //----------------------------------------------------------------------------
}
//################################################################################
