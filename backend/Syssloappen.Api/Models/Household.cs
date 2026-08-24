namespace Syssloappen.Api.Models;

public sealed class Household
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string FamilyCodeHash { get; set; } = string.Empty;

    public string? FamilyCodeLastFour { get; set; }

    public DateTime? FamilyCodeUpdatedAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
