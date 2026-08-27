namespace Syssloappen.Api.Dtos.ChoreAssignments;

public sealed record ChildChoreAssignmentResponse(
    int AssignmentId,
    int ChoreId,
    string Title,
    string? Description,
    int Points,
    DateTime AssignedAt,
    DateOnly DueDate,
    string Status,
    DateTime? SubmittedAt,
    string? ReviewComment);
