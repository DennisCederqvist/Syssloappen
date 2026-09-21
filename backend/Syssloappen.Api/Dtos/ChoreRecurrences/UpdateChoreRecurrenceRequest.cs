using System.Text.Json.Serialization;
using Syssloappen.Api.Models;

namespace Syssloappen.Api.Dtos.ChoreRecurrences;

public sealed class UpdateChoreRecurrenceRequest
{
    [JsonConverter(typeof(JsonStringEnumConverter<ChoreRecurrenceFrequency>))]
    public ChoreRecurrenceFrequency Frequency { get; init; }

    /// <summary>Bit flags Mon=1..Sun=64. Same rules as when creating a recurrence.</summary>
    public int? DaysOfWeekMask { get; init; }

    /// <summary>1-31. Required for Monthly, ignored otherwise.</summary>
    public int? DayOfMonth { get; init; }
}
