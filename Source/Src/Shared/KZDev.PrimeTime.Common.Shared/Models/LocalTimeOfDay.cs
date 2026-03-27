#if NET

using System.Diagnostics;

namespace KZDev.PrimeTime;

//################################################################################
/// <summary>
///   Represents a time of day in the local time zone, as a <see cref="TimeOnly"/> wrapper
///   with semantic meaning for use in day-time timers and scheduling.
/// </summary>
/// <remarks>
///   This type distinguishes local time-of-day from <see cref="UtcTimeOfDay"/> so that
///   day-time timer registration APIs can require the correct interpretation without ambiguity.
/// </remarks>
public readonly struct LocalTimeOfDay : IEquatable<LocalTimeOfDay>
{
    private readonly TimeOnly _value;

    #region Constructors/Finalizers

    /// <summary>
    ///   Initializes a new instance of the <see cref="LocalTimeOfDay"/> struct from a
    ///   <see cref="TimeOnly"/> value.
    /// </summary>
    /// <param name="time">The local time of day.</param>
    public LocalTimeOfDay (TimeOnly time)
    {
        _value = time;
    }

    #endregion Constructors/Finalizers

    /// <summary>
    ///   Gets the underlying <see cref="TimeOnly"/> value.
    /// </summary>
    public TimeOnly Value { [DebuggerStepThrough] get => _value; }

    #region Overrides

    /// <inheritdoc />
    public override bool Equals (object? obj) => obj is LocalTimeOfDay other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode () => _value.GetHashCode();

    /// <inheritdoc />
    public override string ToString () => _value.ToString();

    #endregion Overrides

    #region Interface Implementations

    /// <inheritdoc />
    public bool Equals (LocalTimeOfDay other) => _value == other._value;

    #endregion Interface Implementations

    #region Operators

    /// <summary>
    ///   Returns a value indicating whether two <see cref="LocalTimeOfDay"/> instances are equal.
    /// </summary>
    public static bool operator == (LocalTimeOfDay left, LocalTimeOfDay right) => left.Equals(right);

    /// <summary>
    ///   Returns a value indicating whether two <see cref="LocalTimeOfDay"/> instances are not equal.
    /// </summary>
    public static bool operator != (LocalTimeOfDay left, LocalTimeOfDay right) => !left.Equals(right);

    #endregion Operators
}
//################################################################################

#endif
