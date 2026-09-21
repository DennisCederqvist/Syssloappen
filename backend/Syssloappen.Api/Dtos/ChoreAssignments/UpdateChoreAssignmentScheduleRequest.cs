using System.Text.Json.Serialization;
using Syssloappen.Api.Models;

namespace Syssloappen.Api.Dtos.ChoreAssignments;

/// <summary>
/// Sets how an assignment repeats. A null <see cref="Frequency"/> makes it a one-off chore on
/// <see cref="DueDate"/>; any other value makes it a recurring chore that starts on
/// <see cref="DueDate"/> (or keeps its existing recurrence, updating the schedule).
/// </summary>
public sealed class UpdateChoreAssignmentScheduleRequest
{
    [JsonConverter(typeof(JsonStringEnumConverter<ChoreRecurrenceFrequency>))]
    public ChoreRecurrenceFrequency? Frequency { get; init; }

    public int? DaysOfWeekMask { get; init; }

    public int? DayOfMonth { get; init; }

    public DateOnly? DueDate { get; init; }
}
