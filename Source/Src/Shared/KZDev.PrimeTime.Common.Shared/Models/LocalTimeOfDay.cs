#if NET

using System.Diagnostics;

#if SYSTEMCLOCK
namespace KZDev.SystemClock.PrimeTime;
#else
namespace KZDev.PrimeTime;
#endif

/// <summary>
///   Represents a time of day in the local time zone, as a <see cref="TimeOnly"/> wrapper
///   with semantic meaning for use in day-time timers and scheduling.
/// </summary>
/// <remarks>
///   <para>
///     This type distinguishes local time-of-day from <see cref="UtcTimeOfDay"/> so that
///     day-time timer registration APIs can require the correct interpretation without ambiguity.
///   </para>
///   <para>
///     This type is not included when the library targets .NET Standard 2.0 or .NET Framework; it
///     requires <see cref="TimeOnly"/>.
///   </para>
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

    /// <summary>
    ///   Converts the value of the current <see cref="LocalTimeOfDay"/> instance to its equivalent short time string representation.
    /// </summary>
    /// <returns>The short time string representation of the current instance.</returns>
    public string ToShortTimeString () => _value.ToShortTimeString();

    /// <summary>
    ///   Converts the value of the current <see cref="LocalTimeOfDay"/> instance to its equivalent long time string representation.
    /// </summary>
    /// <returns>The long time string representation of the current instance.</returns>
    public string ToLongTimeString () => _value.ToLongTimeString();

    #region Overrides

    /// <inheritdoc />
    public override bool Equals (object? obj) => obj is LocalTimeOfDay other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode () => _value.GetHashCode();

    #endregion Overrides

    #region ToString

    /// <summary>
    ///   Converts the value of the current <see cref="LocalTimeOfDay"/> instance to its equivalent
    ///   short time string representation using the formatting conventions of the current culture.
    /// </summary>
    /// <returns>
    ///   The short time string representation of the current instance.
    /// </returns>
    public override string ToString () => _value.ToString();

    /// <summary>
    ///   Converts the value of the current <see cref="LocalTimeOfDay"/> instance to its equivalent
    ///   string representation using the specified culture-specific format information.
    /// </summary>
    /// <param name="provider">The culture-specific formatting information.</param>
    /// <returns>
    ///   A string representation of the current instance as specified by <paramref name="provider"/>.
    /// </returns>
    public string ToString (IFormatProvider? provider) => _value.ToString(provider);

    /// <summary>
    ///   Converts the value of the current <see cref="LocalTimeOfDay"/> instance to its equivalent
    ///   string representation using the specified format and the formatting conventions of the
    ///   current culture.
    /// </summary>
    /// <param name="format">A standard or custom time format string.</param>
    /// <returns>
    ///   A string representation of the current instance with the specified format and the
    ///   formatting conventions of the current culture.
    /// </returns>
    /// <remarks>
    ///   The accepted standard formats are 'r', 'R', 'o', 'O', 't' and 'T'.
    /// </remarks>
    public string ToString (string? format) => _value.ToString(format);

    /// <summary>
    ///   Converts the value of the current <see cref="LocalTimeOfDay"/> instance to its equivalent
    ///   string representation using the specified format and culture-specific format information.
    /// </summary>
    /// <param name="format">A standard or custom time format string.</param>
    /// <param name="provider">The culture-specific formatting information.</param>
    /// <returns>
    ///   A string representation of the value of the current instance.
    /// </returns>
    /// <remarks>
    ///   The accepted standard formats are 'r', 'R', 'o', 'O', 't' and 'T'.
    /// </remarks>
    public string ToString (string? format, IFormatProvider? provider) => _value.ToString(format, provider);

    #endregion ToString

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

#endif
