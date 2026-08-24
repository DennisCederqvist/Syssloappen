using Syssloappen.Api.Authentication;

namespace Syssloappen.Api.Models;

public sealed class ChildDeviceSession
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public int HouseholdId { get; set; }

    public Household Household { get; set; } = null!;

    public int ChildProfileId { get; set; }

    public ChildProfile ChildProfile { get; set; } = null!;

    public string UserId { get; set; } = string.Empty;

    public ApplicationUser User { get; set; } = null!;

    public string SecretHash { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public DateTime LastSeenAt { get; set; }

    public DateTime ExpiresAt { get; set; }

    public DateTime AbsoluteExpiresAt { get; set; }

    public DateTime? RevokedAt { get; set; }
}
