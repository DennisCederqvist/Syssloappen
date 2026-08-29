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
[Route("api/child/chore-assignments")]
[Authorize(Roles = RoleNames.Child)]
public sealed class ChildChoreAssignmentsController(
    AppDbContext dbContext,
    UserManager<ApplicationUser> userManager,
    TimeProvider timeProvider) : ControllerBase
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

        await MoveUnfinishedAssignmentsToToday(child.Id, currentUser.HouseholdId);

        // Repeating every ownership condition in the SQL query protects the private
        // Child view even if inconsistent assignment data were ever introduced.
        var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
        var assignments = await dbContext.ChoreAssignments
            .AsNoTracking()
            .Where(assignment =>
                assignment.ChildId == child.Id
                && assignment.HouseholdId == currentUser.HouseholdId
                && assignment.Child.HouseholdId == currentUser.HouseholdId
                && assignment.Child.UserId == currentUser.Id
                && assignment.Child.IsActive
                && assignment.Chore.HouseholdId == currentUser.HouseholdId
                && assignment.Status != ChoreAssignmentStatus.Cancelled
                && assignment.DueDate <= today)
            .OrderByDescending(assignment => assignment.DueDate)
            .ThenByDescending(assignment => assignment.AssignedAt)
            .ThenByDescending(assignment => assignment.Id)
            .Select(assignment => new ChildChoreAssignmentResponse(
                assignment.Id,
                assignment.ChoreId,
                assignment.Chore.Title,
                assignment.Chore.Description,
                assignment.Points,
                assignment.AssignedAt,
                assignment.DueDate,
                assignment.Status.ToString(),
                assignment.SubmittedAt,
                assignment.ReviewComment))
            .ToListAsync();

        return Ok(assignments);
    }

    private async Task MoveUnfinishedAssignmentsToToday(int childId, int householdId)
    {
        var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);

        // Only chores the child can still perform roll into today. A submitted
        // chore is waiting for review, while approved and cancelled chores are history.
        await dbContext.ChoreAssignments
            .Where(assignment =>
                assignment.ChildId == childId
                && assignment.HouseholdId == householdId
                && assignment.DueDate < today
                && (assignment.Status == ChoreAssignmentStatus.Assigned
                    || assignment.Status == ChoreAssignmentStatus.NeedsRedo))
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(assignment => assignment.DueDate, today));
    }

    [HttpPost("{assignmentId:int}/submit")]
    [ProducesResponseType<SubmitChoreAssignmentResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<SubmitChoreAssignmentResponse>> Submit(int assignmentId)
    {
        if (assignmentId <= 0)
        {
            ModelState.AddModelError(nameof(assignmentId), "Assignment ID must be positive.");
            return ValidationProblem(ModelState);
        }

        var currentUser = await userManager.GetUserAsync(User);

        if (currentUser is null)
        {
            return Unauthorized();
        }

        // Account and household select the active ChildProfile. The request cannot
        // choose a ChildId, HouseholdId, owner, status or timestamp.
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

        // All ownership links are repeated in the update lookup. A sibling's,
        // another household's or an inconsistent assignment looks like not found.
        var assignment = await dbContext.ChoreAssignments
            .SingleOrDefaultAsync(item =>
                item.Id == assignmentId
                && item.ChildId == child.Id
                && item.HouseholdId == currentUser.HouseholdId
                && item.Child.HouseholdId == currentUser.HouseholdId
                && item.Child.UserId == currentUser.Id
                && item.Child.IsActive
                && item.Chore.HouseholdId == currentUser.HouseholdId
                && item.Status != ChoreAssignmentStatus.Cancelled);

        if (assignment is null)
        {
            return NotFound();
        }

        if (assignment.Status is not ChoreAssignmentStatus.Assigned
            and not ChoreAssignmentStatus.NeedsRedo)
        {
            return Conflict(new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Assignment cannot be submitted",
                Detail = "The assignment has already been submitted or reviewed."
            });
        }

        assignment.Status = ChoreAssignmentStatus.PendingApproval;
        assignment.SubmittedAt = timeProvider.GetUtcNow().UtcDateTime;
        assignment.ReviewedByUserId = null;
        assignment.ReviewedAt = null;
        assignment.ReviewComment = null;

        try
        {
            await dbContext.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            return Conflict(new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Assignment cannot be submitted",
                Detail = "The assignment was already changed by another request."
            });
        }

        return Ok(new SubmitChoreAssignmentResponse(
            assignment.Id,
            assignment.Status.ToString(),
            assignment.SubmittedAt.Value));
    }
}
