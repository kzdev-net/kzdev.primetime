// Copyright (c) Kevin Zehrer
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Diagnostics.Tracing;
// ReSharper disable MemberCanBePrivate.Global

// ReSharper disable InconsistentNaming

#if SYSTEMCLOCK
namespace KZDev.SystemClock.PrimeTime.Observability;
#else
namespace KZDev.PrimeTime.Observability;
#endif

//################################################################################
/// <summary>
///   ETW event source for shared PrimeTime diagnostics. Event IDs and payloads are part of the
///   ETW contract; do not renumber or change payload shapes in a compatible release.
/// </summary>
#if SYSTEMCLOCK
[EventSource(Name = "KZDev.SystemClock.PrimeTime")]
#else
[EventSource(Name = "KZDev.PrimeTime")]
#endif
internal sealed class PrimeTimeEventSource : EventSource
{
    //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
    /// <summary>
    ///   Event source keywords for filtering.
    /// </summary>
    public static class Keywords
    {
        /// <summary>
        ///   Day-time and time-zone scheduling resolution.
        /// </summary>
        public const EventKeywords Scheduling = (EventKeywords)0x0001;

        /// <summary>
        ///   Timer lifecycle and misuse.
        /// </summary>
        public const EventKeywords Timer = (EventKeywords)0x0002;

        /// <summary>
        ///   Internal invariant violations (unexpected states or enum values).
        /// </summary>
        public const EventKeywords Invariant = (EventKeywords)0x0004;
    }
    //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
    /// <summary>
    ///   Event tasks for grouping.
    /// </summary>
    public static class Tasks
    {
        /// <summary>
        ///   Local wall clock day-time scheduling.
        /// </summary>
        public const EventTask DayTimeScheduling = (EventTask)0x0001;

        /// <summary>
        ///   Interval timer registration.
        /// </summary>
        public const EventTask IntervalTimer = (EventTask)0x0002;

        /// <summary>
        ///   Day-time timer registration.
        /// </summary>
        public const EventTask DayTimeTimer = (EventTask)0x0003;

        /// <summary>
        ///   Shared clock timer surface (base registration).
        /// </summary>
        public const EventTask ClockTimer = (EventTask)0x0004;
    }
    //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
    /// <summary>
    ///   Custom opcodes for semantic filtering.
    /// </summary>
    public static class Opcodes
    {
        /// <summary>
        ///   Scheduling resolution could not complete as requested.
        /// </summary>
        public const EventOpcode SchedulingFault = (EventOpcode)11;

        /// <summary>
        ///   Exhausted bounded search for a valid schedule instant.
        /// </summary>
        public const EventOpcode SchedulingSearchExhausted = (EventOpcode)12;

        /// <summary>
        ///   Member invoked after the timer was disposed.
        /// </summary>
        public const EventOpcode UseAfterDispose = (EventOpcode)13;

        /// <summary>
        ///   Invalid state transition on a timer.
        /// </summary>
        public const EventOpcode InvalidTimerTransition = (EventOpcode)14;

        /// <summary>
        ///   Callback dispatch path does not support the registered callback kind.
        /// </summary>
        public const EventOpcode UnsupportedCallbackKind = (EventOpcode)15;

        /// <summary>
        ///   User timer callback threw an exception on a thread-pool thread.
        /// </summary>
        public const EventOpcode CallbackUnhandledException = (EventOpcode)16;
    }
    //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
    /// <summary>
    ///   Singleton event source for the PrimeTime shared library.
    /// </summary>
    public static PrimeTimeEventSource Log { [DebuggerStepThrough] get; } = new();
    //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~

    private const int EventId_DayTimeSchedulingResolutionFault = 1;
    private const int EventId_DayTimeSchedulingFireInstantNotFound = 2;
    private const int EventId_ClockTimerUseAfterDispose = 3;
    private const int EventId_IntervalTimerInvalidRepeatTransition = 4;
    private const int EventId_ClockTimerUnsupportedCallbackKind = 5;
    private const int EventId_ClockTimerCallbackException = 6;

    //--------------------------------------------------------------------------------
    /// <summary>
    ///   Fault code for <see cref="DayTimeSchedulingResolutionFault"/> when the calendar date has no
    ///   invalid local wall-time window where one was required for resolution.
    /// </summary>
    public const int FaultCode_ExpectedInvalidWindowMissing = 1;

    /// <summary>
    ///   Fault code when RunAfter could not resolve an instant after a spring-forward gap.
    /// </summary>
    public const int FaultCode_RunAfterResolutionFailed = 2;

    /// <summary>
    ///   Fault code when RunBefore could not resolve an instant before a spring-forward gap.
    /// </summary>
    public const int FaultCode_RunBeforeResolutionFailed = 3;
    //--------------------------------------------------------------------------------

    //--------------------------------------------------------------------------------
    /// <summary>
    ///   Raised when local day-time scheduling cannot resolve an expected invalid wall-time window
    ///   or a RunAfter/RunBefore instant around a spring-forward gap.
    /// </summary>
    /// <param name="zoneId">Time zone identifier.</param>
    /// <param name="faultCode">
    ///   One of <see cref="FaultCode_ExpectedInvalidWindowMissing"/>,
    ///   <see cref="FaultCode_RunAfterResolutionFailed"/>, or <see cref="FaultCode_RunBeforeResolutionFailed"/>.
    /// </param>
    /// <param name="detail">Additional diagnostic text (may be empty).</param>
    [Event(EventId_DayTimeSchedulingResolutionFault,
        Keywords = Keywords.Scheduling,
        Task = Tasks.DayTimeScheduling,
        Opcode = Opcodes.SchedulingFault,
        Level = EventLevel.Warning)]
    public void DayTimeSchedulingResolutionFault (string zoneId, int faultCode, string detail)
    {
        if (IsEnabled(EventLevel.Warning, Keywords.Scheduling))
        {
            WriteEvent(EventId_DayTimeSchedulingResolutionFault, zoneId, faultCode, detail);
        }
    }
    //--------------------------------------------------------------------------------
    /// <summary>
    ///   Raised when no qualifying local day-time fire instant exists within the bounded search window.
    /// </summary>
    /// <param name="zoneId">Time zone identifier.</param>
    /// <param name="targetTimeOfDay">Target local time of day.</param>
    /// <param name="searchWindowDays">Maximum calendar days searched.</param>
    [Event(EventId_DayTimeSchedulingFireInstantNotFound,
        Keywords = Keywords.Scheduling,
        Task = Tasks.DayTimeScheduling,
        Opcode = Opcodes.SchedulingSearchExhausted,
        Level = EventLevel.Error)]
    public void DayTimeSchedulingFireInstantNotFound (string zoneId, string targetTimeOfDay, int searchWindowDays)
    {
        if (IsEnabled(EventLevel.Error, Keywords.Scheduling))
        {
            WriteEvent(EventId_DayTimeSchedulingFireInstantNotFound, zoneId, targetTimeOfDay, searchWindowDays);
        }
    }
    //--------------------------------------------------------------------------------
    /// <summary>
    ///   Raised when a timer surface is used after disposal.
    /// </summary>
    /// <param name="operation">Logical operation name (for example, SetEnabled, Start, ChangeNextAndRepeat).</param>
    [Event(EventId_ClockTimerUseAfterDispose,
        Keywords = Keywords.Timer,
        Task = Tasks.ClockTimer,
        Opcode = Opcodes.UseAfterDispose,
        Level = EventLevel.Warning)]
    public void ClockTimerUseAfterDispose (string operation)
    {
        if (IsEnabled(EventLevel.Warning, Keywords.Timer))
        {
            WriteEvent(EventId_ClockTimerUseAfterDispose, operation);
        }
    }
    //--------------------------------------------------------------------------------
    /// <summary>
    ///   Raised when a one-shot interval timer is changed to repeat.
    /// </summary>
    [Event(EventId_IntervalTimerInvalidRepeatTransition,
        Keywords = Keywords.Timer,
        Task = Tasks.IntervalTimer,
        Opcode = Opcodes.InvalidTimerTransition,
        Level = EventLevel.Warning)]
    public void IntervalTimerInvalidRepeatTransition ()
    {
        if (IsEnabled(EventLevel.Warning, Keywords.Timer))
        {
            WriteEvent(EventId_IntervalTimerInvalidRepeatTransition);
        }
    }
    //--------------------------------------------------------------------------------
    /// <summary>
    ///   Raised when callback dispatch does not support the registered callback kind value.
    /// </summary>
    /// <param name="callbackKind">Numeric callback kind from the internal timer registration.</param>
    /// <param name="timerCategory"><c>Interval</c> or <c>DayTime</c>.</param>
    [Event(EventId_ClockTimerUnsupportedCallbackKind,
        Keywords = Keywords.Invariant,
        Task = Tasks.ClockTimer,
        Opcode = Opcodes.UnsupportedCallbackKind,
        Level = EventLevel.Error)]
    public void ClockTimerUnsupportedCallbackKind (int callbackKind, string timerCategory)
    {
        if (IsEnabled(EventLevel.Error, Keywords.Invariant))
        {
            WriteEvent(EventId_ClockTimerUnsupportedCallbackKind, callbackKind, timerCategory);
        }
    }
    //--------------------------------------------------------------------------------
    /// <summary>
    ///   Raised when a user timer callback throws on a thread-pool thread and the registration swallows
    ///   the exception to keep timer state consistent.
    /// </summary>
    /// <param name="timerCategory"><c>DayTime</c> or <c>Interval</c>.</param>
    /// <param name="exceptionTypeName">Full name of the exception type.</param>
    /// <param name="exceptionDetail">Exception details (typically the full string from System.Exception.ToString).</param>
    [Event(EventId_ClockTimerCallbackException,
        Keywords = Keywords.Timer,
        Task = Tasks.ClockTimer,
        Opcode = Opcodes.CallbackUnhandledException,
        Level = EventLevel.Error)]
    private void ClockTimerCallbackException (string timerCategory, string exceptionTypeName, string exceptionDetail)
    {
        if (IsEnabled(EventLevel.Error, Keywords.Timer))
        {
            WriteEvent(EventId_ClockTimerCallbackException, timerCategory, exceptionTypeName, exceptionDetail);
        }
    }
    //--------------------------------------------------------------------------------
    /// <summary>
    ///   Records a timer callback exception via
    ///   <see cref="ClockTimerCallbackException(string, string, string)"/>.
    /// </summary>
    /// <param name="timerCategory"><c>DayTime</c> or <c>Interval</c>.</param>
    /// <param name="exception">The exception to record.</param>
    [NonEvent]
    public void ClockTimerCallbackException (string timerCategory, Exception exception)
    {
        if (exception is null || string.IsNullOrEmpty(timerCategory))
        {
            return;
        }

        string exceptionTypeName = exception.GetType().FullName ?? exception.GetType().Name;
        ClockTimerCallbackException(timerCategory, exceptionTypeName, exception.ToString());
    }
    //--------------------------------------------------------------------------------
}
//################################################################################
