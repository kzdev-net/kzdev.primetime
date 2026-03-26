// Copyright (c) Kevin Zehrer
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

#if NET

using AwesomeAssertions;
using KZDev.PrimeTime.Tests;
#pragma warning disable HAA0601

namespace KZDev.SystemClock.PrimeTime.UnitTests;

/// <summary>
///   Unit tests for <see cref="UtcTimeOfDay"/>.
///   Verifies construction, equality, and that the type is distinct from <see cref="LocalTimeOfDay"/>.
/// </summary>
public class UsingUtcTimeOfDay : UnitTestBase
{
    #region Constructors/Finalizers

    /// <summary>
    ///   Initializes a new instance of the <see cref="UsingUtcTimeOfDay"/> class.
    /// </summary>
    /// <param name="xUnitTestOutputHelper">The xUnit test output helper.</param>
    public UsingUtcTimeOfDay (ITestOutputHelper xUnitTestOutputHelper)
        : base(xUnitTestOutputHelper)
    {
    }

    #endregion Constructors/Finalizers

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
        UtcTimeOfDay a = new(new(0, 0, 0));
        UtcTimeOfDay b = new(new(12, 0, 0));
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
        UtcTimeOfDay sut = new(new(1, 2, 3));
        string s = sut.ToString();
        s.Should().NotBeNullOrEmpty();
    }
}

#endif
