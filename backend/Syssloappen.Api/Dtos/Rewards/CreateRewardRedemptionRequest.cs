using System.ComponentModel.DataAnnotations;
namespace Syssloappen.Api.Dtos.Rewards;
public sealed class CreateRewardRedemptionRequest { [Range(1, int.MaxValue)] public int RewardId { get; init; } }
