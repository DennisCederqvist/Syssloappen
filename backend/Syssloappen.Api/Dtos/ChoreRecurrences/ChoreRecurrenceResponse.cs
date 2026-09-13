namespace Syssloappen.Api.Dtos.ChoreRecurrences;

public sealed record ChoreRecurrenceResponse(
    int Id,
    int ChoreId,
    string ChoreTitle,
    int ChildId,
    string ChildName,
    string Frequency,
    int? DaysOfWeekMask,
    int? DayOfMonth,
    DateOnly StartDate);
