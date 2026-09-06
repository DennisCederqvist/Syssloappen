namespace Syssloappen.Api.Dtos.Households;

public sealed record HouseholdAdultResponse(string Id, string Email, bool IsOwner, string? DisplayName);
