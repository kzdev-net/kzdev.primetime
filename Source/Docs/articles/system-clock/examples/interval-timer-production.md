# Interval timer (production) — System Clock stack

This example demonstrates registering both **synchronous** and **asynchronous** interval timer callbacks on **`IPrimeClock`** using the **System Clock** package (`KZDev.SystemClock.PrimeTime`). The timer signatures use **`TimeSpan`** for due time and repeat interval — the BCL convention this stack standardizes on.

The snippet below is the body of the demo `RunAsync` method. It builds a minimal **`ServiceCollection`**, resolves an **`IPrimeClock`**, registers two timers (one synchronous, one asynchronous), waits for both to fire twice, and disposes both registrations.

[!code-csharp[](../../../../Dev/Production/KZDev.SystemClock.PrimeTime.Examples/Scenarios/IntervalTimerScenario.cs#Snippet)]

## What to notice

- **`AddPrimeClock`** registers `TimeProvider.System`, `IPrimeClock` (singleton `PrimeClock`), and `IPrimeTime` (same instance as `IPrimeClock`).
- Both **`RegisterTimer`** (sync) and **`RegisterAsyncTimer`** (async) accept the same `(dueTime, repeat, callback, cancellationToken, state, timerOptions)` shape.
- Each timer registration returns an **`IClockIntervalTimer`** that you **must dispose** to stop the timer and release resources.
- The callback receives an **`IClockIntervalTimerCallbackContext`** (used here for `Registration.Id` in logging) and a per-callback **`CancellationToken`** signalled when the timer is being disposed or the registration cancellation token fires.

## Related

- [Interval timer (testing)](interval-timer-testing.md) — the deterministic virtual-time counterpart for unit tests.
- **Concepts:** [Timers, daylight saving, and testing](../../concepts/concepts-timers-and-testing.md)
- **API:** [`IPrimeClock`](xref:KZDev.SystemClock.PrimeTime.IPrimeClock) · [`IClockIntervalTimer`](xref:KZDev.SystemClock.PrimeTime.IClockIntervalTimer)
