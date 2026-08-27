namespace Syssloappen.Api.Dtos.Rewards;
public sealed record RewardRedemptionResponse(
    int Id,
    int RewardId,
    string RewardName,
    int PointsCost,
    string Status,
    DateTime RequestedAt,
    DateTime? ReviewedAt,
    DateTime? DeliveredAt,
    string? Comment,
    int AvailablePoints);
