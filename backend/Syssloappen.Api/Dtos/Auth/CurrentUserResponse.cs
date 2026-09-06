namespace Syssloappen.Api.Dtos.Auth;

public sealed record CurrentUserResponse(
    string UserId,
    string? Email,
    string Role,
    int HouseholdId,
    string? FirstName,
    string? LastName,
    string? Nickname,
    string? DisplayName);
