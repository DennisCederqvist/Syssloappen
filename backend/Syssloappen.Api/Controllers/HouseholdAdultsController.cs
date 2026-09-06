using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Syssloappen.Api.Authentication;
using Syssloappen.Api.Data;
using Syssloappen.Api.Dtos.Households;

namespace Syssloappen.Api.Controllers;

[ApiController]
[Route("api/household/adults")]
[Authorize(Roles = RoleNames.Adult)]
public sealed class HouseholdAdultsController(
    AppDbContext dbContext,
    UserManager<ApplicationUser> userManager,
    TimeProvider timeProvider) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<List<HouseholdAdultResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<List<HouseholdAdultResponse>>> List()
    {
        var currentUser = await userManager.GetUserAsync(User);

        if (currentUser is null)
        {
            return Unauthorized();
        }

        var household = await dbContext.Households
            .AsNoTracking()
            .SingleAsync(candidate => candidate.Id == currentUser.HouseholdId);

        // Household.Users also includes Child accounts, so the Adult role must be
        // joined explicitly rather than inferred from HouseholdId alone. Disconnected
        // adults are excluded — they no longer belong to the active household, even
        // though their historical rows and references are preserved.
        var adults = await (
            from user in dbContext.Users.AsNoTracking()
            join userRole in dbContext.UserRoles.AsNoTracking() on user.Id equals userRole.UserId
            join role in dbContext.Roles.AsNoTracking() on userRole.RoleId equals role.Id
            where user.HouseholdId == currentUser.HouseholdId
                && role.Name == RoleNames.Adult
                && user.DisconnectedAt == null
            orderby user.Id == household.OwnerUserId descending, user.NormalizedEmail
            select new HouseholdAdultResponse(user.Id, user.Email!, user.Id == household.OwnerUserId))
            .ToListAsync();

        return Ok(adults);
    }

    [HttpDelete("{userId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Disconnect(string userId)
    {
        var currentUser = await userManager.GetUserAsync(User);

        if (currentUser is null)
        {
            return Unauthorized();
        }

        var household = await dbContext.Households
            .SingleAsync(candidate => candidate.Id == currentUser.HouseholdId);

        // The target must be an active Adult in the caller's own Household. A foreign,
        // Child, nonexistent or already-disconnected ID is treated identically as 404.
        var target = await (
            from user in dbContext.Users
            join userRole in dbContext.UserRoles on user.Id equals userRole.UserId
            join role in dbContext.Roles on userRole.RoleId equals role.Id
            where user.Id == userId
                && user.HouseholdId == currentUser.HouseholdId
                && role.Name == RoleNames.Adult
                && user.DisconnectedAt == null
            select user)
            .SingleOrDefaultAsync();

        if (target is null)
        {
            return NotFound();
        }

        // The owner can never be disconnected, by anyone, including themselves — only
        // permanent, explicitly-confirmed family deletion (a separate, later step) can
        // remove them.
        if (target.Id == household.OwnerUserId)
        {
            return Conflict(new ProblemDetails
            {
                Title = "Huvudägaren kan inte kopplas bort.",
                Status = StatusCodes.Status409Conflict
            });
        }

        var activeAdultCount = await (
            from user in dbContext.Users
            join userRole in dbContext.UserRoles on user.Id equals userRole.UserId
            join role in dbContext.Roles on userRole.RoleId equals role.Id
            where user.HouseholdId == currentUser.HouseholdId
                && role.Name == RoleNames.Adult
                && user.DisconnectedAt == null
            select user.Id)
            .CountAsync();

        // Defense in depth alongside the owner check above: a Household must never be
        // left with zero active Adults, regardless of which rule would otherwise allow it.
        if (activeAdultCount <= 1)
        {
            return Conflict(new ProblemDetails
            {
                Title = "Sista vuxna kan inte kopplas bort.",
                Status = StatusCodes.Status409Conflict
            });
        }

        // A normal disconnection preserves the row (and every historical reference to
        // it) but revokes login and frees the email for a future, unrelated registration.
        target.DisconnectedAt = timeProvider.GetUtcNow().UtcDateTime;
        target.Email = null;
        target.NormalizedEmail = null;
        target.UserName = null;
        target.NormalizedUserName = null;
        await dbContext.SaveChangesAsync();

        return NoContent();
    }
}
