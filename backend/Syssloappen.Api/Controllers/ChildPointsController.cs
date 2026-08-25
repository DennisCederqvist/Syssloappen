using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Syssloappen.Api.Authentication;
using Syssloappen.Api.Data;
using Syssloappen.Api.Dtos.ChoreAssignments;

namespace Syssloappen.Api.Controllers;

[ApiController]
[Route("api/child/points")]
[Authorize(Roles = RoleNames.Child)]
public sealed class ChildPointsController(
    AppDbContext dbContext,
    UserManager<ApplicationUser> userManager) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<ChildPointsResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ChildPointsResponse>> GetTotal()
    {
        var currentUser = await userManager.GetUserAsync(User);

        if (currentUser is null)
        {
            return Unauthorized();
        }

        var child = await dbContext.ChildProfiles
            .AsNoTracking()
            .SingleOrDefaultAsync(profile =>
                profile.UserId == currentUser.Id
                && profile.HouseholdId == currentUser.HouseholdId
                && profile.IsActive);

        if (child is null)
        {
            return Unauthorized();
        }

        var totalPoints = await dbContext.ChoreCompletions
            .AsNoTracking()
            .Where(completion =>
                completion.ChildId == child.Id
                && completion.HouseholdId == currentUser.HouseholdId
                && completion.Child.HouseholdId == currentUser.HouseholdId
                && completion.Child.UserId == currentUser.Id
                && completion.Assignment.HouseholdId == currentUser.HouseholdId
                && completion.Assignment.ChildId == child.Id
                && completion.Chore.HouseholdId == currentUser.HouseholdId)
            .SumAsync(completion => (int?)completion.PointsAwarded) ?? 0;

        return Ok(new ChildPointsResponse(totalPoints));
    }
}
