namespace Syssloappen.Api.Dtos.ChoreAssignments;

public sealed record ChildChoreAssignmentResponse(
    int AssignmentId,
    int ChoreId,
    string Title,
    string? Description,
    DateTime AssignedAt);
