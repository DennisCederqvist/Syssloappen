namespace Syssloappen.Api.Dtos.Rewards;
public sealed record ChildRewardsResponse(int AvailablePoints, IReadOnlyList<ChildRewardResponse> Rewards);
