namespace Syssloappen.Api.Dtos.ChoreAssignments;

public sealed record SubmitChoreAssignmentResponse(
    int AssignmentId,
    string Status,
    DateTime SubmittedAt);
