using System.ComponentModel.DataAnnotations;

namespace Syssloappen.Api.Dtos.ChoreAssignments;

public sealed class CreateChoreAssignmentRequest
{
    [Range(1, int.MaxValue)]
    public int ChoreId { get; init; }

    [Range(1, int.MaxValue)]
    public int ChildId { get; init; }

    public DateOnly? DueDate { get; init; }
}
