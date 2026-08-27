namespace Syssloappen.Api.Dtos.ChoreAssignments;

public sealed record AdultChoreAssignmentResponse(
    int AssignmentId,
    int ChoreId,
    string ChoreTitle,
    int ChildId,
    string ChildName,
    int Points,
    DateTime AssignedAt,
    DateOnly DueDate,
    string Status,
    DateTime? SubmittedAt,
    string? ReviewedByUserId,
    DateTime? ReviewedAt,
    string? ReviewComment,
    string? CancelledByUserId,
    DateTime? CancelledAt,
    DateTime? AdultArchivedAt);
