using Syssloappen.Api.Authentication;

namespace Syssloappen.Api.Models;

public sealed class HouseholdInvitation
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public int HouseholdId { get; set; }

    public Household Household { get; set; } = null!;

    public string CreatedByUserId { get; set; } = string.Empty;

    public ApplicationUser CreatedByUser { get; set; } = null!;

    public string CodeHash { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public DateTime ExpiresAt { get; set; }

    public DateTime? UsedAt { get; set; }
}