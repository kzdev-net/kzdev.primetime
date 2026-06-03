// Copyright (c) Kevin Zehrer
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

namespace KZDev.PrimeTime.Examples.Infrastructure;

/// <summary>
/// Builds timezone context from the current machine environment.
/// </summary>
public sealed class LocalTimeZoneScenarioContextFactory : ITimeZoneScenarioContextFactory
{
    /// <summary>
    /// Creates timezone context based on the local machine timezone.
    /// </summary>
    /// <returns>
    /// Local timezone identity and daylight-saving support information.
    /// </returns>
    public TimeZoneScenarioContext Create()
    {
        TimeZoneInfo localTimeZone = TimeZoneInfo.Local;
        DateTime? invalidLocalTimeExample = null;
        DateTime? ambiguousLocalTimeExample = null;

        if (localTimeZone.SupportsDaylightSavingTime)
        {
            (invalidLocalTimeExample, ambiguousLocalTimeExample) = FindTransitionExamples(localTimeZone);
        }

        return new TimeZoneScenarioContext(localTimeZone.Id, localTimeZone.SupportsDaylightSavingTime,
            invalidLocalTimeExample, ambiguousLocalTimeExample);
    }

    private static (DateTime? invalidLocalTimeExample, DateTime? ambiguousLocalTimeExample) FindTransitionExamples(TimeZoneInfo timeZone)
    {
        DateTime? invalid = null;
        DateTime? ambiguous = null;
        TimeZoneInfo.AdjustmentRule[] rules = timeZone.GetAdjustmentRules();

        for (int year = DateTime.Now.Year - 1; year <= DateTime.Now.Year + 2; year++)
        {
            if (invalid is not null && ambiguous is not null)
            {
                break;
            }

            TimeZoneInfo.AdjustmentRule? applicableRule = null;
            foreach (TimeZoneInfo.AdjustmentRule rule in rules)
            {
                if (year < rule.DateStart.Year || year > rule.DateEnd.Year)
                {
                    continue;
                }

                applicableRule = rule;
                break;
            }

            if (applicableRule is null)
            {
                continue;
            }

            if (invalid is null)
            {
                DateTime daylightStart = BuildTransitionDate(year, applicableRule.DaylightTransitionStart);
                DateTime candidateInvalid = daylightStart.AddMinutes(1);
                if (timeZone.IsInvalidTime(candidateInvalid))
                {
                    invalid = candidateInvalid;
                }
            }

            if (ambiguous is not null)
            {
                continue;
            }

            DateTime daylightEnd = BuildTransitionDate(year, applicableRule.DaylightTransitionEnd);
            DateTime candidateAmbiguous = daylightEnd.AddMinutes(-30);
            if (timeZone.IsAmbiguousTime(candidateAmbiguous))
            {
                ambiguous = candidateAmbiguous;
            }
        }

        return (invalid, ambiguous);
    }

    private static DateTime BuildTransitionDate(int year, TimeZoneInfo.TransitionTime transitionTime)
    {
        DateTime transitionBase = transitionTime.TimeOfDay;

        if (transitionTime.IsFixedDateRule)
        {
            return new DateTime(year,
                transitionTime.Month,
                transitionTime.Day,
                transitionBase.Hour,
                transitionBase.Minute,
                transitionBase.Second,
                DateTimeKind.Unspecified);
        }

        DateTime firstDayOfMonth = new(year, transitionTime.Month, 1);
        int daysToTargetDay = ((int)transitionTime.DayOfWeek - (int)firstDayOfMonth.DayOfWeek + 7) % 7;
        DateTime firstTargetDay = firstDayOfMonth.AddDays(daysToTargetDay);
        DateTime transitionDate = firstTargetDay.AddDays((transitionTime.Week - 1) * 7);

        if (transitionTime.Week != 5)
        {
            return new DateTime(transitionDate.Year,
                transitionDate.Month,
                transitionDate.Day,
                transitionBase.Hour,
                transitionBase.Minute,
                transitionBase.Second,
                DateTimeKind.Unspecified);
        }

        DateTime monthEnd = firstDayOfMonth.AddMonths(1).AddDays(-1);
        while (monthEnd.DayOfWeek != transitionTime.DayOfWeek)
        {
            monthEnd = monthEnd.AddDays(-1);
        }

        transitionDate = monthEnd;

        return new DateTime(transitionDate.Year,
            transitionDate.Month,
            transitionDate.Day,
            transitionBase.Hour,
            transitionBase.Minute,
            transitionBase.Second,
            DateTimeKind.Unspecified);
    }
}
