namespace Syssloappen.Api.Dtos.Children;

public sealed record ChildDeviceSessionResponse(
    Guid SessionId,
    DateTime CreatedAt,
    DateTime LastSeenAt,
    DateTime ExpiresAt,
    DateTime AbsoluteExpiresAt,
    DateTime? RevokedAt);
