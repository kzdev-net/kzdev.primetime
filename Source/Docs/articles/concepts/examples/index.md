# Cross-track examples

These pages cover **testing patterns** that look the same on both production stacks. Each page presents the **System Clock** and **PrimeTime (NodaTime)** variants side-by-side so readers do not have to switch between tracks.

## Pages

- [DI replacement with `AddPrimeTestClock`](di-replacement-testing.md) — substitute virtual time in a Microsoft DI container so `IPrimeClock` consumers resolve a shared `PrimeTestClock`.
- [Test-clock control APIs](test-clock-control-testing.md) — `Set*`, `Advance`, `RunFor`, `Start` / `Stop` reference patterns.

For stack-specific examples (interval timers, time-of-day, DST, sleep / delay / cancellation), see each track's examples folder:

- [System Clock examples](../../system-clock/examples/index.md)
- [PrimeTime (NodaTime) examples](../../primetime/examples/index.md)

## Related

- [Choosing a package](../choosing-a-package.md)
- [Timers, daylight saving, and testing](../concepts-timers-and-testing.md)
- [PrimeTime testing packages](../../testing-packages.md)
