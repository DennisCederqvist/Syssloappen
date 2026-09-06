using Syssloappen.Api.Authentication;

namespace Syssloappen.Api.Models;

public sealed class Household
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string FamilyCodeHash { get; set; } = string.Empty;

    public string? FamilyCodeLastFour { get; set; }

    public DateTime? FamilyCodeUpdatedAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // The Adult who created this Household. Permanent — never reassigned by
    // normal disconnection; only a later, separate destructive step could
    // ever remove the household itself.
    // Nullable only to break the mutual foreign-key cycle at creation time
    // (Household needs the owner's user ID; that user's row needs the
    // Household's ID first) — RegisterAdult always sets it within the same
    // transaction before commit, so every household that is ever externally
    // visible has one.
    public string? OwnerUserId { get; set; }

    public ApplicationUser? OwnerUser { get; set; }
}
