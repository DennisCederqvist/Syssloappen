using Microsoft.AspNetCore.Identity;
using Syssloappen.Api.Models;

namespace Syssloappen.Api.Authentication;

public sealed class ApplicationUser : IdentityUser
{
    public int HouseholdId { get; set; }

    public Household Household { get; set; } = null!;
}
