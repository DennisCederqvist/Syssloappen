namespace Syssloappen.Api.Dtos.ChoreAssignments;

public sealed record ChoreAssignmentResponse(
    int Id,
    int ChoreId,
    int ChildId,
    int Points,
    DateTime AssignedAt,
    DateOnly DueDate);
