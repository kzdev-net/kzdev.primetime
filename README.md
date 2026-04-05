# KZDev.PrimeTime

This repository ships two related NuGet libraries for time, clocks, and scheduling in .NET. Pick **one** package per application; they are **mutually exclusive** (do not reference both).

**KZDev.PrimeTime** is the **superset** package: it includes everything in the subset plus NodaTime-specific APIs on the same abstractions (for example, extra overloads on `IPrimeClock`). **KZDev.SystemClock.PrimeTime** is a **strict BCL subset**: shared service names and the common contract surface match the superset where they overlap, without a NodaTime dependency.

## Packages

- **[KZDev.PrimeTime](https://www.nuget.org/packages/KZDev.PrimeTime)** — Contracts and timer abstractions plus **NodaTime-backed** clocks and timers. Use `IPrimeClock`, `PrimeClock`, and related types in the **`KZDev.PrimeTime`** namespace. NodaTime support ships in this package only (there is no separate `KZDev.PrimeTime.NodaTime` package).

- **[KZDev.SystemClock.PrimeTime](https://www.nuget.org/packages/KZDev.SystemClock.PrimeTime)** — The same service names (`IPrimeClock`, `PrimeClock`, `IPrimeTestClock`, `PrimeTestClock`) with **BCL / `TimeProvider` only**, in the **`KZDev.SystemClock.PrimeTime`** namespace. Use this when you want the shared model without NodaTime.

## Using the library

Install the package that matches your stack. Register services with the DI extension methods for that package (for example, `AddPrimeClock` on `IServiceCollection`). The published API documentation describes both deliverables.

## Features

### Day-time timers and daylight saving time

Local time-of-day timers resolve the next fire using the clock’s time zone and the `SkippedTimeBehavior` and `DuplicateTimeBehavior` values on `DayTimeTimerOptions`.

## Documentation

Full documentation for the libraries is available on the [PrimeTime Documentation](https://kzdev-net.github.io/kzdev.primetime/) page.

## Future Features

The roadmap plan for this package is to add several additional helpful performance focused utilities as time allows.

## Contribution Guidelines

At this time, I am not accepting external pull requests. However, any feedback or suggestions are welcome and can be provided through the following channels:

- **Feature Requests:** Please use GitHub Discussions to discuss new features or enhancements before opening a feature request. This will help ensure that your request is in line with the project's goals and vision.
- **Bug Reports:** If you encounter any issues, feel free to open an issue so it can be addressed promptly.

I appreciate your understanding and look forward to collaborating with you through discussions and issue tracking.
