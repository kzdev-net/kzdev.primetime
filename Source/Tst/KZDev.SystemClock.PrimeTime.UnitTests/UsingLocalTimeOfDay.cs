// Copyright (c) Kevin Zehrer
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

#if NET

using System.Diagnostics.CodeAnalysis;

using AwesomeAssertions;
using KZDev.PrimeTime.Tests;
#pragma warning disable HAA0601

namespace KZDev.SystemClock.PrimeTime.UnitTests;

/// <summary>
///   Unit tests for <see cref="LocalTimeOfDay"/>.
///   Verifies construction, equality, and that the type is distinct from <see cref="UtcTimeOfDay"/>.
/// </summary>
[ExcludeFromCodeCoverage]
public class UsingLocalTimeOfDay : UnitTestBase
{
    #region Constructors/Finalizers

    /// <summary>
    ///   Initializes a new instance of the <see cref="UsingLocalTimeOfDay"/> class.
    /// </summary>
    /// <param name="xUnitTestOutputHelper">The xUnit test output helper.</param>
    public UsingLocalTimeOfDay (ITestOutputHelper xUnitTestOutputHelper)
        : base(xUnitTestOutputHelper)
    {
    }

    #endregion Constructors/Finalizers

    /// <summary>
    ///   Verifies that <see cref="LocalTimeOfDay"/> can be constructed from a
    ///   <see cref="TimeOnly"/> and that <see cref="LocalTimeOfDay.Value"/> returns it.
    /// </summary>
    [Fact]
    public void LocalTimeOfDay_Constructor_StoresValue ()
    {
        TimeOnly time = new(14, 30, 0);
        LocalTimeOfDay sut = new(time);
        sut.Value.Should().Be(time);
    }

    /// <summary>
    ///   Verifies that two <see cref="LocalTimeOfDay"/> instances with the same
    ///   <see cref="TimeOnly"/> are equal.
    /// </summary>
    [Fact]
    public void LocalTimeOfDay_Equality_SameTimeOnly_AreEqual ()
    {
        TimeOnly time = new(9, 0, 0);
        LocalTimeOfDay a = new(time);
        LocalTimeOfDay b = new(time);
        a.Should().Be(b);
        (a == b).Should().BeTrue();
        (a != b).Should().BeFalse();
        a.GetHashCode().Should().Be(b.GetHashCode());
    }

    /// <summary>
    ///   Verifies that two <see cref="LocalTimeOfDay"/> instances with different
    ///   <see cref="TimeOnly"/> are not equal.
    /// </summary>
    [Fact]
    public void LocalTimeOfDay_Equality_DifferentTimeOnly_AreNotEqual ()
    {
        LocalTimeOfDay a = new(new(8, 0, 0));
        LocalTimeOfDay b = new(new(17, 0, 0));
        a.Should().NotBe(b);
        (a == b).Should().BeFalse();
        (a != b).Should().BeTrue();
    }

    /// <summary>
    ///   Verifies that <see cref="LocalTimeOfDay.Equals(object)"/> returns false for
    ///   a different type (e.g. <see cref="UtcTimeOfDay"/>).
    /// </summary>
    [Fact]
    public void LocalTimeOfDay_EqualsObject_WithUtcTimeOfDay_ReturnsFalse ()
    {
        TimeOnly time = new(12, 0, 0);
        LocalTimeOfDay local = new(time);
        UtcTimeOfDay utc = new(time);
        local.Equals(utc).Should().BeFalse();
    }

    /// <summary>
    ///   Verifies that <see cref="LocalTimeOfDay.ToString"/> returns a non-empty string.
    /// </summary>
    [Fact]
    public void LocalTimeOfDay_ToString_ReturnsNonEmpty ()
    {
        LocalTimeOfDay sut = new(new(12, 30, 45));
        string s = sut.ToString();
        s.Should().NotBeNullOrEmpty();
    }

    /// <summary>
    ///   Verifies that <see cref="LocalTimeOfDay"/> and <see cref="UtcTimeOfDay"/> are
    ///   distinct types: the same <see cref="TimeOnly"/> in each yields different types,
    ///   and they are not equal when compared as objects.
    /// </summary>
    [Fact]
    public void LocalTimeOfDay_And_UtcTimeOfDay_AreDistinctTypes_SameTimeOnly ()
    {
        TimeOnly time = new(15, 45, 30);
        LocalTimeOfDay local = new(time);
        UtcTimeOfDay utc = new(time);

        local.Value.Should().Be(utc.Value, "underlying TimeOnly is the same");
        // Intentional cross-type comparison to assert distinct semantics.
        local.Should().NotBe(utc, "value types with different identity are not equal");
        local.Equals(utc).Should().BeFalse();
        utc.Equals(local).Should().BeFalse();
    }
}

#endif
