namespace Syssloappen.Api.Dtos.Rewards;
public sealed record RewardRedemptionResponse(int Id, int RewardId, string RewardName, int PointsCost, string Status, DateTime RequestedAt, int AvailablePoints);
