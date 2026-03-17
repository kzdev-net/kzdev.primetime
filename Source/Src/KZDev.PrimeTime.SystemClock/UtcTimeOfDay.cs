#if NET

using System.Diagnostics;

namespace KZDev.PrimeTime;

//################################################################################
    /// <summary>
    ///   Represents a time of day in UTC, as a <see cref="TimeOnly"/> wrapper with semantic
    ///   meaning for use in day-time timers and scheduling.
    /// </summary>
    /// <remarks>
    ///   This type distinguishes UTC time-of-day from <see cref="LocalTimeOfDay"/> so that
    ///   day-time timer registration APIs can require the correct interpretation without ambiguity.
    /// </remarks>
    public readonly struct UtcTimeOfDay : IEquatable<UtcTimeOfDay>
    {
        private readonly TimeOnly _value;

        #region Constructors/Finalizers

        /// <summary>
        ///   Initializes a new instance of the <see cref="UtcTimeOfDay"/> struct from a
        ///   <see cref="TimeOnly"/> value.
        /// </summary>
        /// <param name="time">The UTC time of day.</param>
        public UtcTimeOfDay (TimeOnly time)
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
        public override bool Equals (object? obj) => obj is UtcTimeOfDay other && Equals(other);

        /// <inheritdoc />
        public override int GetHashCode () => _value.GetHashCode();

        /// <inheritdoc />
        public override string ToString () => _value.ToString();

        #endregion Overrides

        #region Interface Implementations

        /// <inheritdoc />
        public bool Equals (UtcTimeOfDay other) => _value == other._value;

        #endregion Interface Implementations

        #region Operators

        /// <summary>
        ///   Returns a value indicating whether two <see cref="UtcTimeOfDay"/> instances are equal.
        /// </summary>
        public static bool operator == (UtcTimeOfDay left, UtcTimeOfDay right) => left.Equals(right);

        /// <summary>
        ///   Returns a value indicating whether two <see cref="UtcTimeOfDay"/> instances are not equal.
        /// </summary>
        public static bool operator != (UtcTimeOfDay left, UtcTimeOfDay right) => !left.Equals(right);

        #endregion Operators
    }
//################################################################################

#endif
