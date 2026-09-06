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
    UserManager<ApplicationUser> userManager) : ControllerBase
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
        // joined explicitly rather than inferred from HouseholdId alone.
        var adults = await (
            from user in dbContext.Users.AsNoTracking()
            join userRole in dbContext.UserRoles.AsNoTracking() on user.Id equals userRole.UserId
            join role in dbContext.Roles.AsNoTracking() on userRole.RoleId equals role.Id
            where user.HouseholdId == currentUser.HouseholdId && role.Name == RoleNames.Adult
            orderby user.NormalizedEmail
            select new HouseholdAdultResponse(user.Id, user.Email!, user.Id == household.OwnerUserId))
            .ToListAsync();

        return Ok(adults);
    }
}
