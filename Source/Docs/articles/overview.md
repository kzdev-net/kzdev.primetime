# Overview

KZDev.PrimeTime libraries offer a shared model for time, clocks, and scheduling on .NET.

There are two published packages:

- **KZDev.PrimeTime** — Superset: full feature set with NodaTime-backed clocks and NodaTime overloads on shared abstractions (`IPrimeClock`, `PrimeClock`, and related APIs in the namespace `KZDev.PrimeTime`).
- **KZDev.SystemClock.PrimeTime** — Strict BCL subset: the same core service type names and signature-aligned common APIs, in the namespace `KZDev.SystemClock.PrimeTime`, without NodaTime.

Reference exactly one package in a given project. See the [API Reference](xref:PrimeTime) for generated documentation covering both assemblies included in this site.
