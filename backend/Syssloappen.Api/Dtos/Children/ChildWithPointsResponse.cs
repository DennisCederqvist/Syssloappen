namespace Syssloappen.Api.Dtos.Children;

/// <summary>A child in the household list, with their available points (earned minus reserved by open reward requests).</summary>
public sealed record ChildWithPointsResponse(int Id, string Name, string? PhotoUrl, int AvailablePoints);
