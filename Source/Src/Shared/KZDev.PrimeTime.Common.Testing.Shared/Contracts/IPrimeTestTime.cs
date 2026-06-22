// Copyright (c) Kevin Zehrer
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

#if SYSTEMCLOCK
namespace KZDev.SystemClock.PrimeTime.Testing;
#else
namespace KZDev.PrimeTime.Testing;
#endif

//################################################################################
/// <summary>
///   Extends <see cref="IPrimeTime"/> with test-controlled time support for clock implementations used in tests.
/// </summary>
public interface IPrimeTestTime : IPrimeTime
{
    //----------------------------------------------------------------------------
    /// <summary>
    ///   Gets a value indicating whether the test clock's automatic runner is active.
    /// </summary>
    /// <remarks>
    ///   <para>
    ///     When <c>true</c>, a background thread advances virtual time toward the next deadline at the configured
    ///     run rate, "now" surfaces may project from a committed instant and monotonic anchor, and persist-on-read
    ///     may march due work when "now" is observed. When <c>false</c>, virtual time changes only through explicit
    ///     operations such as <see cref="IPrimeTestClock.SetTime(System.DateTimeOffset)"/>,
    ///     <see cref="IPrimeTestClock.Advance(System.TimeSpan)"/>, or Noda equivalents on
    ///     <see cref="IPrimeTestClock"/>.
    ///   </para>
    /// </remarks>
    bool IsRunning { get; }
    //----------------------------------------------------------------------------
}
//################################################################################
