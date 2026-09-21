using Syssloappen.Api.Models;

namespace Syssloappen.Api.Services;

/// <summary>Validation and normalisation shared by every endpoint that sets a recurring schedule.</summary>
public static class ChoreScheduleRules
{
    /// <summary>
    /// Checks the weekday mask / day of month against the frequency and returns only the fields the
    /// frequency actually uses (the others are null). Returns false with the offending field name
    /// and a message when the combination is invalid.
    /// </summary>
    public static bool TryNormalize(
        ChoreRecurrenceFrequency frequency,
        int? daysOfWeekMask,
        int? dayOfMonth,
        out int? normalizedDaysOfWeekMask,
        out int? normalizedDayOfMonth,
        out string field,
        out string error)
    {
        normalizedDaysOfWeekMask = null;
        normalizedDayOfMonth = null;
        field = string.Empty;
        error = string.Empty;

        switch (frequency)
        {
            case ChoreRecurrenceFrequency.Weekly:
                if (daysOfWeekMask is null || !IsSingleBitSet(daysOfWeekMask.Value))
                {
                    field = "DaysOfWeekMask";
                    error = "Weekly recurrence requires exactly one weekday.";
                    return false;
                }
                normalizedDaysOfWeekMask = daysOfWeekMask;
                return true;
            case ChoreRecurrenceFrequency.Custom:
                if (daysOfWeekMask is null or 0 or > 127)
                {
                    field = "DaysOfWeekMask";
                    error = "Custom recurrence requires at least one weekday.";
                    return false;
                }
                normalizedDaysOfWeekMask = daysOfWeekMask;
                return true;
            case ChoreRecurrenceFrequency.Monthly:
                if (dayOfMonth is null or < 1 or > 31)
                {
                    field = "DayOfMonth";
                    error = "Monthly recurrence requires a day of month between 1 and 31.";
                    return false;
                }
                normalizedDayOfMonth = dayOfMonth;
                return true;
            default:
                return true;
        }
    }

    private static bool IsSingleBitSet(int value) => value > 0 && (value & (value - 1)) == 0;
}
