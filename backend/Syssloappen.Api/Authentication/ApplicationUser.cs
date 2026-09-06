using Microsoft.AspNetCore.Identity;
using Syssloappen.Api.Models;

namespace Syssloappen.Api.Authentication;

public sealed class ApplicationUser : IdentityUser
{
    public int HouseholdId { get; set; }

    public Household Household { get; set; } = null!;

    public string? ChildUserName { get; set; }

    public string? NormalizedChildUserName { get; set; }

    // Set when an Adult is disconnected from their Household (normal, non-destructive
    // removal — never for the household owner). The row, its ID and every historical
    // reference to it are preserved for audit; only login credentials are cleared.
    public DateTime? DisconnectedAt { get; set; }
}
