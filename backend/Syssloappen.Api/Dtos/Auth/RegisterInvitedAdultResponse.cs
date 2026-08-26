namespace Syssloappen.Api.Dtos.Auth;

public sealed record RegisterInvitedAdultResponse(string Email, string Role, int HouseholdId);