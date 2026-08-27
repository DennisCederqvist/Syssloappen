namespace Syssloappen.Api.Dtos.Rewards;

public sealed record AdultRewardRedemptionResponse(
    int Id,
    int ChildId,
    string ChildName,
    int RewardId,
    string RewardName,
    int PointsCost,
    string Status,
    DateTime RequestedAt,
    DateTime? ReviewedAt,
    DateTime? DeliveredAt,
    string? Comment,
    DateTime? AdultArchivedAt);
