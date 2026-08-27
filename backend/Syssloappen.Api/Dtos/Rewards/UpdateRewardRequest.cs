using System.ComponentModel.DataAnnotations;

namespace Syssloappen.Api.Dtos.Rewards;

public sealed class UpdateRewardRequest
{
    [Required]
    [StringLength(100)]
    public string Name { get; init; } = string.Empty;

    [StringLength(500)]
    public string? Description { get; init; }

    [Range(1, int.MaxValue)]
    public int PointsCost { get; init; }

    [Range(0, int.MaxValue)]
    public int StockQuantity { get; init; }
}
