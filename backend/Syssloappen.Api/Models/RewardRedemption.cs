namespace Syssloappen.Api.Models;

public sealed class RewardRedemption
{
    public int Id { get; set; }
    public int HouseholdId { get; set; }
    public Household Household { get; set; } = null!;
    public int RewardId { get; set; }
    public Reward Reward { get; set; } = null!;
    public int ChildId { get; set; }
    public ChildProfile Child { get; set; } = null!;
    public int PointsCost { get; set; }
    public RewardRedemptionStatus Status { get; set; } = RewardRedemptionStatus.Requested;
    public string IdempotencyKey { get; set; } = string.Empty;
    public DateTime RequestedAt { get; set; }
    public string? ReviewedByUserId { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? DeliveredByUserId { get; set; }
    public DateTime? DeliveredAt { get; set; }
    public string? Comment { get; set; }
    public DateTime? AdultArchivedAt { get; set; }
}
