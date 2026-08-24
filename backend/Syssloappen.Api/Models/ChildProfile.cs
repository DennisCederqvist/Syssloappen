namespace Syssloappen.Api.Models;

public sealed class ChildProfile
{
    public int Id { get; set; }

    public int HouseholdId { get; set; }

    public Household Household { get; set; } = null!;

    public string Name { get; set; } = string.Empty;
}
