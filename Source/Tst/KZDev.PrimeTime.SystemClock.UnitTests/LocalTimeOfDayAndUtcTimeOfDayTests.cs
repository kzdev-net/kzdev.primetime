// Copyright (c) Kevin Zehrer
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

#if NET

using AwesomeAssertions;
using KZDev.PrimeTime.Tests;

namespace KZDev.PrimeTime.SystemClock.UnitTests;

/// <summary>
///   Unit tests for <see cref="LocalTimeOfDay"/> and <see cref="UtcTimeOfDay"/> (Phase 4).
///   Verifies construction, equality, semantics, and that the types are distinct.
/// </summary>
public class LocalTimeOfDayAndUtcTimeOfDayTests : UnitTestBase
{
    /// <summary>
    ///   Initializes a new instance of the <see cref="LocalTimeOfDayAndUtcTimeOfDayTests"/> class.
    /// </summary>
    /// <param name="xUnitTestOutputHelper">The xUnit test output helper.</param>
    public LocalTimeOfDayAndUtcTimeOfDayTests (ITestOutputHelper xUnitTestOutputHelper)
        : base(xUnitTestOutputHelper)
    {
    }

    #region LocalTimeOfDay

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
        LocalTimeOfDay a = new(new TimeOnly(8, 0, 0));
        LocalTimeOfDay b = new(new TimeOnly(17, 0, 0));
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
        LocalTimeOfDay sut = new(new TimeOnly(12, 30, 45));
        string s = sut.ToString();
        s.Should().NotBeNullOrEmpty();
    }

    #endregion LocalTimeOfDay

    #region UtcTimeOfDay

    /// <summary>
    ///   Verifies that <see cref="UtcTimeOfDay"/> can be constructed from a
    ///   <see cref="TimeOnly"/> and that <see cref="UtcTimeOfDay.Value"/> returns it.
    /// </summary>
    [Fact]
    public void UtcTimeOfDay_Constructor_StoresValue ()
    {
        TimeOnly time = new(23, 59, 59);
        UtcTimeOfDay sut = new(time);
        sut.Value.Should().Be(time);
    }

    /// <summary>
    ///   Verifies that two <see cref="UtcTimeOfDay"/> instances with the same
    ///   <see cref="TimeOnly"/> are equal.
    /// </summary>
    [Fact]
    public void UtcTimeOfDay_Equality_SameTimeOnly_AreEqual ()
    {
        TimeOnly time = new(0, 0, 0);
        UtcTimeOfDay a = new(time);
        UtcTimeOfDay b = new(time);
        a.Should().Be(b);
        (a == b).Should().BeTrue();
        (a != b).Should().BeFalse();
        a.GetHashCode().Should().Be(b.GetHashCode());
    }

    /// <summary>
    ///   Verifies that two <see cref="UtcTimeOfDay"/> instances with different
    ///   <see cref="TimeOnly"/> are not equal.
    /// </summary>
    [Fact]
    public void UtcTimeOfDay_Equality_DifferentTimeOnly_AreNotEqual ()
    {
        UtcTimeOfDay a = new(new TimeOnly(0, 0, 0));
        UtcTimeOfDay b = new(new TimeOnly(12, 0, 0));
        a.Should().NotBe(b);
        (a == b).Should().BeFalse();
        (a != b).Should().BeTrue();
    }

    /// <summary>
    ///   Verifies that <see cref="UtcTimeOfDay.Equals(object)"/> returns false for
    ///   a different type (e.g. <see cref="LocalTimeOfDay"/>).
    /// </summary>
    [Fact]
    public void UtcTimeOfDay_EqualsObject_WithLocalTimeOfDay_ReturnsFalse ()
    {
        TimeOnly time = new(12, 0, 0);
        UtcTimeOfDay utc = new(time);
        LocalTimeOfDay local = new(time);
        utc.Equals(local).Should().BeFalse();
    }

    /// <summary>
    ///   Verifies that <see cref="UtcTimeOfDay.ToString"/> returns a non-empty string.
    /// </summary>
    [Fact]
    public void UtcTimeOfDay_ToString_ReturnsNonEmpty ()
    {
        UtcTimeOfDay sut = new(new TimeOnly(1, 2, 3));
        string s = sut.ToString();
        s.Should().NotBeNullOrEmpty();
    }

    #endregion UtcTimeOfDay

    #region Semantics (distinct types)

    /// <summary>
    ///   Verifies that <see cref="LocalTimeOfDay"/> and <see cref="UtcTimeOfDay"/> are
    ///   distinct types: the same <see cref="TimeOnly"/> in each yields different types
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

    #endregion Semantics (distinct types)
}

#endif
