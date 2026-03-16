# KZDev.PrimeTime

This is the repository for the ['KZDev.PrimeTime'](https://www.nuget.org/packages/KZDev.PrimeTime) nuget package that contains ...

## Using the library

- **KZDev.PrimeTime** (this package) provides shared contracts and types: `IPrimeTime`, timer interfaces, options, and enums. Clock selection is left to the host.
- For a BCL/system clock implementation, add **KZDev.PrimeTime.SystemClock** and use `IPrimeSystemClock` / `PrimeSystemClock`.
- For a NodaTime-based clock, add **KZDev.PrimeTime.NodaTime** and use `IPrimeClock` / `PrimeClock`.

Reference the main package plus the stack package(s) you need so you can use either or both clock stacks.

## Features


## Documentation

Full documentation for the package is available on the [PrimeTime Documentation](https://kzdev-net.github.io/kzdev.primetime/) page.

## Future Features

The roadmap plan for this package is to add several additional helpful performance focused utilities as time allows.

## Contribution Guidelines

At this time, I am not accepting external pull requests. However, any feedback or suggestions are welcome and can be provided through the following channels:

- **Feature Requests:** Please use GitHub Discussions to discuss new features or enhancements before opening a feature request. This will help ensure that your request is in line with the project's goals and vision.
- **Bug Reports:** If you encounter any issues, feel free to open an issue so it can be addressed promptly.

I appreciate your understanding and look forward to collaborating with you through discussions and issue tracking.