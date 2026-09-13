using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Syssloappen.Api.Models;

namespace Syssloappen.Api.Dtos.ChoreRecurrences;

public sealed class CreateChoreRecurrenceRequest
{
    [Range(1, int.MaxValue)]
    public int ChoreId { get; init; }

    [Range(1, int.MaxValue)]
    public int ChildId { get; init; }

    [JsonConverter(typeof(JsonStringEnumConverter<ChoreRecurrenceFrequency>))]
    public ChoreRecurrenceFrequency Frequency { get; init; }

    /// <summary>Bit flags Mon=1..Sun=64. Required (with exactly one bit) for Weekly, required
    /// (with one or more bits) for Custom, ignored otherwise.</summary>
    public int? DaysOfWeekMask { get; init; }

    /// <summary>1-31. Required for Monthly, ignored otherwise.</summary>
    public int? DayOfMonth { get; init; }

    public DateOnly? StartDate { get; init; }
}
