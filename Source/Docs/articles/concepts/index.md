# Concepts

These articles apply to **either** production stack ([**KZDev.SystemClock.PrimeTime**](../system-clock/index.md) or [**KZDev.PrimeTime**](../primetime/index.md)). They focus on choosing between stacks, shared timer and DST behavior, and diagnostics.

## Articles

- [Choosing a package](choosing-a-package.md)
- [Timers, daylight saving, and testing](concepts-timers-and-testing.md)
- [Event monitoring (ETW)](event-monitoring.md)

## Cross-track examples

Patterns that look the same on both stacks are gathered together (each page presents both side-by-side):

- [Examples overview](examples/index.md)
- [DI replacement with `AddPrimeTestClock`](examples/di-replacement-testing.md)
- [Test-clock control APIs](examples/test-clock-control-testing.md)

For stack-specific examples, see [System Clock examples](../system-clock/examples/index.md) and [PrimeTime (NodaTime) examples](../primetime/examples/index.md).
