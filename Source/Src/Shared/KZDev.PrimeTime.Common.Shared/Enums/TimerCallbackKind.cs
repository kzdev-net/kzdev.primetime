// Copyright (c) Kevin Zehrer
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

#if SYSTEMCLOCK
namespace KZDev.SystemClock.PrimeTime;
#else
namespace KZDev.PrimeTime;
#endif

//################################################################################
/// <summary>
///   Internal callback type for interval and day-time timer invocations.
/// </summary>
internal enum TimerCallbackKind
{
    //----------------------------------------------------------------------------
    /// <summary>
    ///   Parameterless <see cref="Action"/> callback.
    /// </summary>
    SimpleAction,

    /// <summary>
    ///   Callback that receives <see cref="ClockTimerCallbackContext"/>.
    /// </summary>
    ContextAction,

    /// <summary>
    ///   Callback that receives <see cref="ClockTimerCallbackContext"/> and
    ///   <see cref="CancellationToken"/>.
    /// </summary>
    ContextActionWithToken,

    /// <summary>
    ///   Async callback that receives <see cref="CancellationToken"/> and returns
    ///   <see cref="ValueTask"/>.
    /// </summary>
    SimpleAsync,

    /// <summary>
    ///   Async callback that receives <see cref="ClockTimerCallbackContext"/> and
    ///   <see cref="CancellationToken"/>, and returns <see cref="ValueTask"/>.
    /// </summary>
    ContextAsync
    //----------------------------------------------------------------------------
}
//################################################################################
