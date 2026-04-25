// Copyright (c) Kevin Zehrer
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using KZDev.SystemClock.PrimeTime.Examples.Helpers;
using KZDev.SystemClock.PrimeTime.Examples.Infrastructure;
using KZDev.SystemClock.PrimeTime.Examples.Scenarios;

DemoRunMode runMode = DemoRunModeParser.Parse(args);
ITimeZoneScenarioContextFactory scenarioContextFactory = new LocalTimeZoneScenarioContextFactory();
TimeZoneScenarioContext scenarioContext = scenarioContextFactory.Create();
IReadOnlyList<IExampleScenario> scenarios = ScenarioCatalog.CreateScenarios(runMode, scenarioContext);

Console.WriteLine("KZDev.SystemClock.PrimeTime examples");
Console.WriteLine("====================================");
Console.WriteLine($"Run mode: {runMode}");
Console.WriteLine($"Local time zone: {scenarioContext.LocalTimeZoneId}");
Console.WriteLine($"Supports DST transitions: {scenarioContext.SupportsDaylightSavingTime}");
Console.WriteLine();

for (int scenarioIndex = 0; scenarioIndex < scenarios.Count; scenarioIndex++)
{
    IExampleScenario scenario = scenarios[scenarioIndex];

    Console.WriteLine($"[{scenarioIndex + 1}/{scenarios.Count}] {scenario.Name}");
    Console.WriteLine(new string('-', scenario.Name.Length + 12));

    await scenario.RunAsync(CancellationToken.None);

    Console.WriteLine();
}

Console.WriteLine("All SystemClock PrimeTime production scenarios completed.");
