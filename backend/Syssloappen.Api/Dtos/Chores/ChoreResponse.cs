namespace Syssloappen.Api.Dtos.Chores;

public sealed record ChoreResponse(
    int Id,
    string Title,
    string? Description,
    int Points,
    DateTime CreatedAt);
