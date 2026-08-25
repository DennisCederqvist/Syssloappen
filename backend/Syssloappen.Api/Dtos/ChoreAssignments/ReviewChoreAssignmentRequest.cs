using System.ComponentModel.DataAnnotations;

namespace Syssloappen.Api.Dtos.ChoreAssignments;

public sealed class ReviewChoreAssignmentRequest
{
    [StringLength(500)]
    public string? Comment { get; init; }
}
