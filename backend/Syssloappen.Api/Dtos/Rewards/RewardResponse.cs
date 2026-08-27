namespace Syssloappen.Api.Dtos.Rewards;

public sealed record RewardResponse(
    int Id,
    string Name,
    string? Description,
    int PointsCost,
    int StockQuantity,
    DateTime CreatedAt);
