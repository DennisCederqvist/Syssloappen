namespace Syssloappen.Api.Dtos.Households;

public sealed record FamilyCodeStatusResponse(
    bool IsConfigured,
    string? MaskedCode,
    DateTime? UpdatedAt);
