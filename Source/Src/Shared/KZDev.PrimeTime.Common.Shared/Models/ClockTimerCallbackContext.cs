// Copyright (c) Kevin Zehrer
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

#if SYSTEMCLOCK
namespace KZDev.SystemClock.PrimeTime;
#else
namespace KZDev.PrimeTime;
//################################################################################
#endif

/// <summary>
///   Context passed to clock timer callbacks that accept state and registration.
/// </summary>
[DebuggerDisplay("{" + nameof(DisplayValue) + ",nq}")]
[StructLayout(LayoutKind.Auto)]
public readonly struct ClockTimerCallbackContext : IEquatable<ClockTimerCallbackContext>
{
    //----------------------------------------------------------------------------
    /// <summary>
    ///   Gets the debugger display string for this context.
    /// </summary>
    [ExcludeFromCodeCoverage]
    private string DisplayValue => $"{Registration.Id} (Registered @ {Registration.RegisteredTime})";
    //----------------------------------------------------------------------------

    #region Constructors/Finalizers

    //----------------------------------------------------------------------------
    /// <summary>
    ///   Initializes a new instance of the <see cref="ClockTimerCallbackContext"/> struct.
    /// </summary>
    /// <param name="registration">
    ///   The timer registration for this callback.
    /// </param>
    /// <param name="callbackState">
    ///   The state passed when the timer was registered, or <c>null</c>.
    /// </param>
    /// <exception cref="ArgumentNullException">
    ///   <paramref name="registration"/> is <c>null</c>.
    /// </exception>
    public ClockTimerCallbackContext (IClockTimer registration, object? callbackState)
    {
        Registration = registration ?? throw new ArgumentNullException(nameof(registration));
        CallbackState = callbackState;
    }
    //----------------------------------------------------------------------------

    #endregion Constructors/Finalizers

    //----------------------------------------------------------------------------
    /// <summary>
    ///   Gets the optional state object passed when the timer was registered.
    /// </summary>
    public object? CallbackState { [DebuggerStepThrough] get; }
    //----------------------------------------------------------------------------

    //----------------------------------------------------------------------------
    /// <summary>
    ///   Gets the timer registration instance for this callback.
    /// </summary>
    public IClockTimer Registration { [DebuggerStepThrough] get; }
    //----------------------------------------------------------------------------

    #region Overrides

    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public override bool Equals (object? obj) =>
        obj is ClockTimerCallbackContext other && Equals(other);
    //----------------------------------------------------------------------------

    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public override int GetHashCode () => Registration.GetHashCode();
    //----------------------------------------------------------------------------

    #endregion Overrides

    #region Interface Implementations

    //----------------------------------------------------------------------------
    /// <inheritdoc />
    public bool Equals (ClockTimerCallbackContext other) =>
        ReferenceEquals(Registration, other.Registration) &&
        Equals(CallbackState, other.CallbackState);
    //----------------------------------------------------------------------------

    #endregion Interface Implementations

    #region Operators

    //----------------------------------------------------------------------------
    /// <summary>
    ///   Equality operator.
    /// </summary>
    public static bool operator == (ClockTimerCallbackContext left, ClockTimerCallbackContext right) =>
        left.Equals(right);
    //----------------------------------------------------------------------------

    //----------------------------------------------------------------------------
    /// <summary>
    ///   Inequality operator.
    /// </summary>
    public static bool operator != (ClockTimerCallbackContext left, ClockTimerCallbackContext right) =>
        !left.Equals(right);
    //----------------------------------------------------------------------------

    #endregion Operators
}
//################################################################################
