using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Syssloappen.Api.Authentication;
using Syssloappen.Api.Data;
using Syssloappen.Api.Dtos.ChoreAssignments;
using Syssloappen.Api.Models;

namespace Syssloappen.Api.Controllers;

[ApiController]
[Route("api/chore-assignments")]
[Authorize(Roles = RoleNames.Adult)]
public sealed class ChoreAssignmentsController(
    AppDbContext dbContext,
    UserManager<ApplicationUser> userManager,
    TimeProvider timeProvider) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType<ChoreAssignmentResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ChoreAssignmentResponse>> Create(
        CreateChoreAssignmentRequest request)
    {
        var currentUser = await userManager.GetUserAsync(User);

        if (currentUser is null)
        {
            return Unauthorized();
        }

        // Combining every client-selected ID with the authenticated Adult's HouseholdId
        // prevents either ID from reaching another household's data.
        var choreExists = await dbContext.Chores.AnyAsync(chore =>
            chore.Id == request.ChoreId
            && chore.HouseholdId == currentUser.HouseholdId);

        if (!choreExists)
        {
            return NotFound();
        }

        var childExists = await dbContext.ChildProfiles.AnyAsync(child =>
            child.Id == request.ChildId
            && child.HouseholdId == currentUser.HouseholdId
            && child.IsActive);

        if (!childExists)
        {
            return NotFound();
        }

        var assignment = new ChoreAssignment
        {
            // Ownership, creating Adult and time are always backend-controlled.
            HouseholdId = currentUser.HouseholdId,
            ChoreId = request.ChoreId,
            ChildId = request.ChildId,
            AssignedByUserId = currentUser.Id,
            AssignedAt = timeProvider.GetUtcNow().UtcDateTime
        };

        dbContext.ChoreAssignments.Add(assignment);
        await dbContext.SaveChangesAsync();

        var response = new ChoreAssignmentResponse(
            assignment.Id,
            assignment.ChoreId,
            assignment.ChildId,
            assignment.AssignedAt);

        return Created("/api/chore-assignments", response);
    }
}
