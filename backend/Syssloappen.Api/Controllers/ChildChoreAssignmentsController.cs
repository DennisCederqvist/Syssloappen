using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Syssloappen.Api.Authentication;
using Syssloappen.Api.Data;
using Syssloappen.Api.Dtos.ChoreAssignments;

namespace Syssloappen.Api.Controllers;

[ApiController]
[Route("api/child/chore-assignments")]
[Authorize(Roles = RoleNames.Child)]
public sealed class ChildChoreAssignmentsController(
    AppDbContext dbContext,
    UserManager<ApplicationUser> userManager) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<ChildChoreAssignmentResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<ChildChoreAssignmentResponse>>> GetMine()
    {
        var currentUser = await userManager.GetUserAsync(User);

        if (currentUser is null)
        {
            return Unauthorized();
        }

        // The authenticated account selects the ChildProfile. There is no client-provided
        // ChildId that could be changed to inspect a sibling or another household.
        var child = await dbContext.ChildProfiles
            .AsNoTracking()
            .SingleOrDefaultAsync(childProfile =>
                childProfile.UserId == currentUser.Id
                && childProfile.HouseholdId == currentUser.HouseholdId
                && childProfile.IsActive);

        if (child is null)
        {
            return Unauthorized();
        }

        // Repeating every ownership condition in the SQL query protects the private
        // Child view even if inconsistent assignment data were ever introduced.
        var assignments = await dbContext.ChoreAssignments
            .AsNoTracking()
            .Where(assignment =>
                assignment.ChildId == child.Id
                && assignment.HouseholdId == currentUser.HouseholdId
                && assignment.Child.HouseholdId == currentUser.HouseholdId
                && assignment.Child.UserId == currentUser.Id
                && assignment.Child.IsActive
                && assignment.Chore.HouseholdId == currentUser.HouseholdId)
            .OrderByDescending(assignment => assignment.AssignedAt)
            .ThenByDescending(assignment => assignment.Id)
            .Select(assignment => new ChildChoreAssignmentResponse(
                assignment.Id,
                assignment.ChoreId,
                assignment.Chore.Title,
                assignment.Chore.Description,
                assignment.AssignedAt))
            .ToListAsync();

        return Ok(assignments);
    }
}
