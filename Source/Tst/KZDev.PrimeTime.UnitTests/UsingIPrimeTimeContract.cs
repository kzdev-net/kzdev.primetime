// Copyright (c) Kevin Zehrer
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Reflection;
using AwesomeAssertions;
using KZDev.PrimeTime.Tests;
using Xunit;

namespace KZDev.PrimeTime.UnitTests;

/// <summary>
///   Unit tests for the IPrimeTime contract exposed by the core library.
/// </summary>
public class UsingIPrimeTimeContract : UnitTestBase
{
    /// <summary>
    ///   Initializes a new instance of the <see cref="UsingIPrimeTimeContract"/> class.
    /// </summary>
    /// <param name="xUnitTestOutputHelper">
    ///   The Xunit test output helper that can be used to output test messages.
    /// </param>
    public UsingIPrimeTimeContract(ITestOutputHelper xUnitTestOutputHelper)
        : base(xUnitTestOutputHelper)
    {
    }

    /// <summary>
    ///   Verifies that the core library assembly exposes the IPrimeTime interface type.
    /// </summary>
    [Fact]
    public void CoreAssembly_ExposesIPrimeTimeInterface()
    {
        Type? iPrimeTimeType = GetIPrimeTimeType();
        iPrimeTimeType.Should().NotBeNull();
        iPrimeTimeType!.IsInterface.Should().BeTrue();
    }

    /// <summary>
    ///   Verifies that IPrimeTime declares the expected delay method names.
    /// </summary>
    [Fact]
    public void IPrimeTime_DeclaresDelayMethods()
    {
        Type? iPrimeTimeType = GetIPrimeTimeType();
        iPrimeTimeType.Should().NotBeNull();
        MethodInfo[] methods = iPrimeTimeType!.GetMethods();
        bool hasSleep = methods.Any(m => m.Name == "Sleep");
        bool hasDelayAsync = methods.Any(m => m.Name == "DelayAsync");
        hasSleep.Should().BeTrue();
        hasDelayAsync.Should().BeTrue();
    }

    private static Type? GetIPrimeTimeType()
    {
        Assembly? coreAssembly = AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(a => a.GetName().Name == "KZDev.PrimeTime");
        if (coreAssembly is null)
        {
            coreAssembly = Assembly.Load(new AssemblyName("KZDev.PrimeTime"));
        }

        return coreAssembly.GetType("PrimeTime.IPrimeTime");
    }
}
