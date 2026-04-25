// Copyright (c) Kevin Zehrer
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using KZDev.SystemClock.PrimeTime.Examples.Helpers;
using KZDev.SystemClock.PrimeTime.Examples.Infrastructure;

DemoRunMode runMode = DemoRunModeParser.Parse(args);
ITimeZoneScenarioContextFactory scenarioContextFactory = new LocalTimeZoneScenarioContextFactory();
TimeZoneScenarioContext scenarioContext = scenarioContextFactory.Create();

Console.WriteLine("KZDev.SystemClock.PrimeTime examples foundation is configured.");
Console.WriteLine($"Run mode: {runMode}");
Console.WriteLine($"Local time zone: {scenarioContext.LocalTimeZoneId}");
Console.WriteLine($"Supports DST transitions: {scenarioContext.SupportsDaylightSavingTime}");
