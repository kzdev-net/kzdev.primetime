# Event monitoring

PrimeTime libraries expose **EventSource** (ETW) events for diagnosing scheduling failures, timer misuse, and internal callback dispatch issues. You can collect them with tools such as [PerfView](https://github.com/microsoft/perfview) or `dotnet-trace`.

## Events

The shared implementation compiles into two NuGet packages. Each assembly registers its own provider name; use the name that matches the package your process references. Event **IDs**, **payload shapes**, and semantics are the same in both assemblies—only the provider string differs.

| Package | Assembly | EventSource name |
| --- | --- | --- |
| **KZDev.PrimeTime** | `KZDev.PrimeTime.dll` | `KZDev.PrimeTime` |
| **KZDev.SystemClock.PrimeTime** | `KZDev.SystemClock.PrimeTime.dll` | `KZDev.SystemClock.PrimeTime` |

The following events are available.

---

### DayTimeSchedulingResolutionFault event

This event is raised when local wall-clock day-time scheduling cannot resolve an expected invalid wall-time window, or cannot resolve a RunAfter/RunBefore instant around a spring-forward gap. It is emitted immediately before the library throws the corresponding `InvalidOperationException`.

The following table shows the task, keyword, level, and opcode.

| Task | Keyword | Level | Opcode |
| --- | --- | --- | --- |
| DayTimeScheduling (0x0001) | Scheduling (0x0001) | Warning (3) | SchedulingFault (11) |

The following table shows the event information.

| Event | Event ID | Raised when |
| --- | --- | --- |
| DayTimeSchedulingResolutionFault | 1 | A day-time resolution fault is detected (missing invalid window, RunAfter, or RunBefore resolution failure). |

The following table shows the event data.

| Name | Type | Description |
| --- | --- | --- |
| zoneId | UnicodeString | The `TimeZoneInfo.Id` for the schedule zone. |
| faultCode | Int32 | Diagnostic code: `1` = expected invalid local wall-time window missing on the calendar date; `2` = RunAfter resolution failed after a spring-forward gap; `3` = RunBefore resolution failed before a spring-forward gap. See `PrimeTimeEventSource.FaultCode_*` constants in source. |
| detail | UnicodeString | Additional context (for example, calendar date or resolution suffix text). May be empty. |

---

### DayTimeSchedulingFireInstantNotFound event

This event is raised when no qualifying local day-time fire instant exists within the bounded forward search window (pathological zone/options combinations). It is emitted immediately before the library throws the corresponding `InvalidOperationException`.

The following table shows the task, keyword, level, and opcode.

| Task | Keyword | Level | Opcode |
| --- | --- | --- | --- |
| DayTimeScheduling (0x0001) | Scheduling (0x0001) | Error (2) | SchedulingSearchExhausted (12) |

The following table shows the event information.

| Event | Event ID | Raised when |
| --- | --- | --- |
| DayTimeSchedulingFireInstantNotFound | 2 | The bounded day search completes without finding a fire instant strictly after the current schedule instant. |

The following table shows the event data.

| Name | Type | Description |
| --- | --- | --- |
| zoneId | UnicodeString | The `TimeZoneInfo.Id` for the schedule zone. |
| targetTimeOfDay | UnicodeString | The target local time of day (`TimeOnly` round-trip `O` format). |
| searchWindowDays | Int32 | The maximum number of successive local calendar days scanned. |

---

### ClockTimerUseAfterDispose event

This event is raised when a timer registration is used after it has been disposed (for example, setting `Enabled` or calling `Start` on a disposed interval timer).

The following table shows the task, keyword, level, and opcode.

| Task | Keyword | Level | Opcode |
| --- | --- | --- | --- |
| ClockTimer (0x0004) | Timer (0x0002) | Warning (3) | UseAfterDispose (13) |

The following table shows the event information.

| Event | Event ID | Raised when |
| --- | --- | --- |
| ClockTimerUseAfterDispose | 3 | A member is invoked on a disposed timer registration that requires an active object. |

The following table shows the event data.

| Name | Type | Description |
| --- | --- | --- |
| operation | UnicodeString | Logical operation name, for example `Enabled`, `Start`, or `ChangeNextAndRepeat`. |

---

### IntervalTimerInvalidRepeatTransition event

This event is raised when a one-shot interval timer registration is changed to use a finite positive repeat interval (invalid transition).

The following table shows the task, keyword, level, and opcode.

| Task | Keyword | Level | Opcode |
| --- | --- | --- | --- |
| IntervalTimer (0x0002) | Timer (0x0002) | Warning (3) | InvalidTimerTransition (14) |

The following table shows the event information.

| Event | Event ID | Raised when |
| --- | --- | --- |
| IntervalTimerInvalidRepeatTransition | 4 | `Change` is called with a positive repeat interval on a registration that was created as one-shot. |

The following table shows the event data.

| Name | Type | Description |
| --- | --- | --- |
| *(none)* | — | This event has no payload fields. |

---

### ClockTimerUnsupportedCallbackKind event

This event is raised when timer callback dispatch reaches a branch that does not support the registered internal callback kind (unexpected internal state). It is emitted immediately before the library throws `InvalidOperationException`.

The following table shows the task, keyword, level, and opcode.

| Task | Keyword | Level | Opcode |
| --- | --- | --- | --- |
| ClockTimer (0x0004) | Invariant (0x0004) | Error (2) | UnsupportedCallbackKind (15) |

The following table shows the event information.

| Event | Event ID | Raised when |
| --- | --- | --- |
| ClockTimerUnsupportedCallbackKind | 5 | Callback dispatch does not support the current `TimerCallbackKind` value. |

The following table shows the event data.

| Name | Type | Description |
| --- | --- | --- |
| callbackKind | Int32 | Numeric value of the internal `TimerCallbackKind` enum (`0` = SimpleAction through `4` = ContextAsync). |
| timerCategory | UnicodeString | One of `['Interval', 'DayTime']` indicating which registration type hit the unsupported branch. |

---

### PerfView example

To capture all events from the **KZDev.PrimeTime** assembly in [PerfView](https://github.com/microsoft/perfview), use the string `*KZDev.PrimeTime` as an argument to the `-providers` option of the `perfview` command-line tool. To also capture stack traces for those events, use `*KZDev.PrimeTime:@StacksEnabled=true`. For processes that reference **KZDev.SystemClock.PrimeTime** instead, use `*KZDev.SystemClock.PrimeTime` (and the same `@StacksEnabled=true` variant). See the PerfView documentation for more information on provider strings and stack collection.
