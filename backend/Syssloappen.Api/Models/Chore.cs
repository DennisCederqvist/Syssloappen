using Syssloappen.Api.Authentication;

namespace Syssloappen.Api.Models;

public sealed class Chore
{
    public int Id { get; set; }

    public int HouseholdId { get; set; }

    public Household Household { get; set; } = null!;

    public string CreatedByUserId { get; set; } = string.Empty;

    public ApplicationUser CreatedByUser { get; set; } = null!;

    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    public int Points { get; set; } = 5;

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; }
}
