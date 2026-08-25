namespace Syssloappen.Api.Dtos.ChoreAssignments;

public sealed record ReviewChoreAssignmentResponse(
    int AssignmentId,
    string Status,
    DateTime ReviewedAt,
    string? ReviewComment,
    int? PointsAwarded);
