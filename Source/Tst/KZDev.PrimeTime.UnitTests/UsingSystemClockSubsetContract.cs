// Copyright (c) Kevin Zehrer. All rights reserved.
// This file is part of the PrimeTime project.

using System.Reflection;

using AwesomeAssertions;

using KZDev.PrimeTime.Tests;

namespace KZDev.PrimeTime.UnitTests;

//################################################################################
/// <summary>
///   Contract tests ensuring every BCL-facing member declared on the subset assembly
///   (<c>KZDev.SystemClock.PrimeTime</c>) is also present on the full-package assembly
///   (<c>KZDev.PrimeTime</c>, built from the same shared sources plus NodaTime overload partials)
///   with the same name, return type, and parameter types (subset / superset parity for shared surface).
/// </summary>
public class UsingSystemClockSubsetContract : UnitTestBase
{
    #region Constructors/Finalizers

    //----------------------------------------------------------------------------
    /// <summary>
    ///   Initializes a new instance of the <see cref="UsingSystemClockSubsetContract"/> class.
    /// </summary>
    /// <param name="xUnitTestOutputHelper">
    ///   The Xunit test output helper that can be used to output test messages.
    /// </param>
    public UsingSystemClockSubsetContract (ITestOutputHelper xUnitTestOutputHelper)
        : base(xUnitTestOutputHelper)
    {
    }
    //----------------------------------------------------------------------------

    #endregion Constructors/Finalizers

    #region Private helpers

    //----------------------------------------------------------------------------
    /// <summary>
    ///   Asserts that for every public instance method declared on <paramref name="subsetInterface"/>,
    ///   <paramref name="supersetInterface"/> exposes a method with the same signature shape.
    /// </summary>
    /// <param name="subsetInterface">The SystemClock (<c>KZDev.SystemClock.PrimeTime</c>) contract interface type.</param>
    /// <param name="supersetInterface">The full package (<c>KZDev.PrimeTime</c>) contract interface type.</param>
    private static void AssertInterfaceMethodsSubset (Type subsetInterface, Type supersetInterface)
    {
        const BindingFlags flags = BindingFlags.Public | BindingFlags.Instance;
        MethodInfo[] subsetMethods = subsetInterface.GetMethods(flags);
        foreach (MethodInfo subsetMethod in subsetMethods)
        {
            MethodInfo? match = FindMatchingMethod(supersetInterface, subsetMethod, flags);
            match.Should().NotBeNull(
                $"Full package {supersetInterface.Name} should declare a method matching SystemClock {subsetInterface.Name}.{DescribeMethod(subsetMethod)}");
        }
    }
    //----------------------------------------------------------------------------

    //----------------------------------------------------------------------------
    /// <summary>
    ///   Asserts that every parameterless public instance property on <paramref name="subsetInterface"/>
    ///   exists on <paramref name="supersetInterface"/> with the same property type.
    /// </summary>
    /// <param name="subsetInterface">The SystemClock contract interface type.</param>
    /// <param name="supersetInterface">The full package contract interface type.</param>
    private static void AssertInterfacePropertiesSubset (Type subsetInterface, Type supersetInterface)
    {
        const BindingFlags flags = BindingFlags.Public | BindingFlags.Instance;
        foreach (PropertyInfo subsetProp in subsetInterface.GetProperties(flags))
        {
            ParameterInfo[] indexParameters = subsetProp.GetIndexParameters();
            if (indexParameters.Length > 0)
            {
                continue;
            }

            PropertyInfo? superProp = supersetInterface.GetProperty(subsetProp.Name, flags);
            superProp.Should().NotBeNull(
                $"Full package {supersetInterface.Name} should declare property {subsetProp.Name} present on SystemClock {subsetInterface.Name}");
            TypesMatchAcrossProductAssemblies(subsetProp.PropertyType, superProp!.PropertyType).Should().BeTrue(
                $"property {subsetProp.Name} type should match between SystemClock and full-package contracts");
        }
    }
    //----------------------------------------------------------------------------

    //----------------------------------------------------------------------------
    /// <summary>
    ///   Finds a method on <paramref name="supersetInterface"/> that matches <paramref name="subsetMethod"/>.
    /// </summary>
    /// <param name="supersetInterface">The interface to search.</param>
    /// <param name="subsetMethod">The reference method from the subset interface.</param>
    /// <param name="flags">Binding flags used when enumerating methods.</param>
    /// <returns>
    ///   A matching <see cref="MethodInfo"/>, or <see langword="null"/> when none match.
    /// </returns>
    private static MethodInfo? FindMatchingMethod (Type supersetInterface, MethodInfo subsetMethod, BindingFlags flags)
    {
        MethodInfo[] candidates = supersetInterface.GetMethods(flags);
        foreach (MethodInfo candidate in candidates)
        {
            if (MethodsMatch(subsetMethod, candidate))
            {
                return candidate;
            }
        }

        return null;
    }
    //----------------------------------------------------------------------------

    //----------------------------------------------------------------------------
    /// <summary>
    ///   Determines whether two interface methods represent the same contract shape (name, generic arity,
    ///   return type, and parameter types).
    /// </summary>
    /// <param name="subsetMethod">The method from the SystemClock interface.</param>
    /// <param name="supersetMethod">The candidate method from the full-package interface.</param>
    /// <returns>
    ///   <see langword="true"/> when the methods match; otherwise <see langword="false"/>.
    /// </returns>
    private static bool MethodsMatch (MethodInfo subsetMethod, MethodInfo supersetMethod)
    {
        if (subsetMethod.Name != supersetMethod.Name)
        {
            return false;
        }

        if (subsetMethod.IsGenericMethod != supersetMethod.IsGenericMethod)
        {
            return false;
        }

        MethodInfo a = subsetMethod;
        MethodInfo b = supersetMethod;
        if (a.IsGenericMethod)
        {
            Type[] aGen = a.GetGenericArguments();
            Type[] bGen = b.GetGenericArguments();
            if (aGen.Length != bGen.Length)
            {
                return false;
            }

            a = a.GetGenericMethodDefinition();
            b = b.GetGenericMethodDefinition();
        }

        if (!TypesMatchAcrossProductAssemblies(a.ReturnType, b.ReturnType))
        {
            return false;
        }

        ParameterInfo[] aParams = a.GetParameters();
        ParameterInfo[] bParams = b.GetParameters();
        if (aParams.Length != bParams.Length)
        {
            return false;
        }

        for (int i = 0; i < aParams.Length; i++)
        {
            if (!TypesMatchAcrossProductAssemblies(aParams[i].ParameterType, bParams[i].ParameterType))
            {
                return false;
            }
        }

        return true;
    }
    //----------------------------------------------------------------------------

    //----------------------------------------------------------------------------
    /// <summary>
    ///   Returns whether two types are the same reference or the same logical PrimeTime contract type
    ///   compiled into <c>KZDev.SystemClock.PrimeTime</c> versus <c>KZDev.PrimeTime</c> (shared sources).
    /// </summary>
    /// <param name="subsetType">A type from the SystemClock assembly or BCL.</param>
    /// <param name="supersetType">A type from the Noda assembly or BCL.</param>
    /// <returns>
    ///   <see langword="true"/> when the types match for subset-contract comparison.
    /// </returns>
    private static bool TypesMatchAcrossProductAssemblies (Type subsetType, Type supersetType)
    {
        if (subsetType == supersetType)
        {
            return true;
        }

        string? subsetNs = subsetType.Namespace;
        string? superNs = supersetType.Namespace;
        if (subsetType.Name != supersetType.Name)
        {
            return false;
        }

        const string systemClockNs = "KZDev.SystemClock.PrimeTime";
        const string fullPackageNs = "KZDev.PrimeTime";
        bool subsetIsSystemClock = string.Equals(subsetNs, systemClockNs, StringComparison.Ordinal);
        bool superIsFullPackage = string.Equals(superNs, fullPackageNs, StringComparison.Ordinal);
        if (subsetIsSystemClock && superIsFullPackage)
        {
            return true;
        }

        return string.Equals(subsetNs, superNs, StringComparison.Ordinal);
    }
    //----------------------------------------------------------------------------

    //----------------------------------------------------------------------------
    /// <summary>
    ///   Builds a short diagnostic string for a <see cref="MethodInfo"/>.
    /// </summary>
    /// <param name="method">The method to describe.</param>
    /// <returns>
    ///   A human-readable signature description.
    /// </returns>
    private static string DescribeMethod (MethodInfo method)
    {
        ParameterInfo[] parameters = method.GetParameters();
        string parameterList = string.Join(", ", parameters.Select(static p => p.ParameterType.Name));
        return $"{method.ReturnType.Name} {method.Name}({parameterList})";
    }
    //----------------------------------------------------------------------------

    #endregion Private helpers

    /// <summary>
    ///   Verifies that each public instance method on <see cref="KZDev.SystemClock.PrimeTime.IPrimeTime"/> has a matching
    ///   method on <see cref="IPrimeTime"/>.
    /// </summary>
    [Fact]
    public void SystemClock_IPrimeTimeSubset_OnNodaIPrimeTime_AllMethodsMatch ()
    {
        AssertInterfaceMethodsSubset(typeof(KZDev.SystemClock.PrimeTime.IPrimeTime), typeof(IPrimeTime));
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies that each public instance method on <see cref="KZDev.SystemClock.PrimeTime.IPrimeClock"/> has a matching
    ///   method on <see cref="IPrimeClock"/>.
    /// </summary>
    [Fact]
    public void SystemClock_IPrimeClockSubset_OnNodaIPrimeClock_AllMethodsMatch ()
    {
        AssertInterfaceMethodsSubset(typeof(KZDev.SystemClock.PrimeTime.IPrimeClock), typeof(IPrimeClock));
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies that each public instance property on <see cref="KZDev.SystemClock.PrimeTime.IPrimeTime"/> exists on
    ///   <see cref="IPrimeTime"/> with the same type.
    /// </summary>
    [Fact]
    public void SystemClock_IPrimeTimeSubset_OnNodaIPrimeTime_AllPropertiesMatch ()
    {
        AssertInterfacePropertiesSubset(typeof(KZDev.SystemClock.PrimeTime.IPrimeTime), typeof(IPrimeTime));
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies that each public instance property on <see cref="KZDev.SystemClock.PrimeTime.IPrimeClock"/> exists on
    ///   <see cref="IPrimeClock"/> with the same type.
    /// </summary>
    [Fact]
    public void SystemClock_IPrimeClockSubset_OnNodaIPrimeClock_AllPropertiesMatch ()
    {
        AssertInterfacePropertiesSubset(typeof(KZDev.SystemClock.PrimeTime.IPrimeClock), typeof(IPrimeClock));
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies that each public instance method on <see cref="KZDev.SystemClock.PrimeTime.IClockIntervalTimer"/> has a matching
    ///   method on <see cref="IClockIntervalTimer"/>.
    /// </summary>
    [Fact]
    public void SystemClock_IClockIntervalTimerSubset_OnFullIClockIntervalTimer_AllMethodsMatch ()
    {
        AssertInterfaceMethodsSubset(typeof(KZDev.SystemClock.PrimeTime.IClockIntervalTimer), typeof(IClockIntervalTimer));
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies that each public instance property on <see cref="KZDev.SystemClock.PrimeTime.IClockIntervalTimer"/> exists on
    ///   <see cref="IClockIntervalTimer"/> with the same type.
    /// </summary>
    [Fact]
    public void SystemClock_IClockIntervalTimerSubset_OnFullIClockIntervalTimer_AllPropertiesMatch ()
    {
        AssertInterfacePropertiesSubset(typeof(KZDev.SystemClock.PrimeTime.IClockIntervalTimer), typeof(IClockIntervalTimer));
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies that each public instance method on <see cref="KZDev.SystemClock.PrimeTime.IClockTimer"/> has a matching
    ///   method on <see cref="IClockTimer"/>.
    /// </summary>
    [Fact]
    public void SystemClock_IClockTimerSubset_OnFullIClockTimer_AllMethodsMatch ()
    {
        AssertInterfaceMethodsSubset(typeof(KZDev.SystemClock.PrimeTime.IClockTimer), typeof(IClockTimer));
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies that each public instance property on <see cref="KZDev.SystemClock.PrimeTime.IClockTimer"/> exists on
    ///   <see cref="IClockTimer"/> with the same type.
    /// </summary>
    [Fact]
    public void SystemClock_IClockTimerSubset_OnFullIClockTimer_AllPropertiesMatch ()
    {
        AssertInterfacePropertiesSubset(typeof(KZDev.SystemClock.PrimeTime.IClockTimer), typeof(IClockTimer));
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies that each public instance method on <see cref="KZDev.SystemClock.PrimeTime.IPrimeTestClock"/> has a matching
    ///   method on <see cref="IPrimeTestClock"/>.
    /// </summary>
    [Fact]
    public void SystemClock_IPrimeTestClockSubset_OnFullIPrimeTestClock_AllMethodsMatch ()
    {
        AssertInterfaceMethodsSubset(typeof(KZDev.SystemClock.PrimeTime.IPrimeTestClock), typeof(IPrimeTestClock));
    }
    //----------------------------------------------------------------------------

    /// <summary>
    ///   Verifies that each public instance property on <see cref="KZDev.SystemClock.PrimeTime.IPrimeTestClock"/> exists on
    ///   <see cref="IPrimeTestClock"/> with the same type.
    /// </summary>
    [Fact]
    public void SystemClock_IPrimeTestClockSubset_OnFullIPrimeTestClock_AllPropertiesMatch ()
    {
        AssertInterfacePropertiesSubset(typeof(KZDev.SystemClock.PrimeTime.IPrimeTestClock), typeof(IPrimeTestClock));
    }
    //----------------------------------------------------------------------------

#if NET8_0_OR_GREATER
    /// <summary>
    ///   Verifies that each public instance method on <see cref="KZDev.SystemClock.PrimeTime.IClockDayTimeTimer"/> has a matching
    ///   method on <see cref="IClockDayTimeTimer"/> (BCL time-of-day surface is <c>#if NET</c> in shared sources; not present on
    ///   <c>netstandard2.0</c> builds).
    /// </summary>
    [Fact]
    public void SystemClock_IClockDayTimeTimerSubset_OnFullIClockDayTimeTimer_AllMethodsMatch ()
    {
        AssertInterfaceMethodsSubset(typeof(KZDev.SystemClock.PrimeTime.IClockDayTimeTimer), typeof(IClockDayTimeTimer));
    }

    /// <summary>
    ///   Verifies that each public instance property on <see cref="KZDev.SystemClock.PrimeTime.IClockDayTimeTimer"/> exists on
    ///   <see cref="IClockDayTimeTimer"/> with the same type.
    /// </summary>
    [Fact]
    public void SystemClock_IClockDayTimeTimerSubset_OnFullIClockDayTimeTimer_AllPropertiesMatch ()
    {
        AssertInterfacePropertiesSubset(typeof(KZDev.SystemClock.PrimeTime.IClockDayTimeTimer), typeof(IClockDayTimeTimer));
    }
#endif
}
//################################################################################
