// Copyright (c) Kevin Zehrer
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Diagnostics;

using NodaTime;

namespace KZDev.PrimeTime.Testing;

//################################################################################
/// <summary>
///   NodaTime virtual instant storage, zone mapping, and Noda-specific surface for
///   <see cref="PrimeTestClock"/>.
/// </summary>
public sealed partial class PrimeTestClock
{
    #region Nested types

    //============================================================================
    /// <summary>
    ///   Noda <see cref="IClockDayTimeTimer"/> change overloads for shared virtual day-time timers.
    /// </summary>
    private abstract partial class VirtualDayTimeTimerBase
    {
        #region Interface Implementations

        #region IClockTimer (Noda) Implementation

        //------------------------------------------------------------------------
        /// <inheritdoc />
        public Instant RegisteredInstant { [DebuggerStepThrough] get => Instant.FromDateTimeOffset(RegisteredTime); }
        //------------------------------------------------------------------------

        #endregion IClockTimer (Noda) Implementation

        #region IClockDayTimeTimer (Noda) Implementation

        //------------------------------------------------------------------------
        /// <inheritdoc />
        public bool Change (LocalTime targetTimeOfDay)
        {
            if (!IsLocal)
                return false;

            lock (Gate)
            {
                if (Disposed || State == TimerState.Cancelled)
                    return false;
                TargetTimeOfDay = LocalTimeToTargetTimeOfDay(targetTimeOfDay);
                if (Enabled)
                    NextDueUtc = ComputeNextDue(Clock.UtcNowDateTimeOffset);

                return true;
            }
        }
        //------------------------------------------------------------------------

        //------------------------------------------------------------------------
        /// <inheritdoc />
        public bool Change (Duration interval) => false;
        //------------------------------------------------------------------------

        #endregion IClockDayTimeTimer (Noda) Implementation

        #endregion Interface Implementations
    }
    //============================================================================

    #endregion Nested types
}
//################################################################################
