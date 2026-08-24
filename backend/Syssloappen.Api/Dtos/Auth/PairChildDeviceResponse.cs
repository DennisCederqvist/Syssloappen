namespace Syssloappen.Api.Dtos.Auth;

public sealed record PairChildDeviceResponse(
    int ChildId,
    string Name,
    string UserName,
    string Role,
    int HouseholdId);
