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
        var chore = await dbContext.Chores.SingleOrDefaultAsync(chore =>
            chore.Id == request.ChoreId
            && chore.HouseholdId == currentUser.HouseholdId
            && chore.IsActive);

        if (chore is null)
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

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var today = DateOnly.FromDateTime(now);
        var dueDate = request.DueDate ?? today;

        if (dueDate < today)
        {
            ModelState.AddModelError(nameof(request.DueDate), "Due date cannot be in the past.");
            return ValidationProblem(ModelState);
        }

        var assignment = new ChoreAssignment
        {
            // Ownership, creating Adult and time are always backend-controlled.
            HouseholdId = currentUser.HouseholdId,
            ChoreId = request.ChoreId,
            ChildId = request.ChildId,
            AssignedByUserId = currentUser.Id,
            AssignedAt = now,
            DueDate = dueDate,
            // Snapshot the promised value so later chore edits cannot rewrite history.
            Points = chore.Points
        };

        dbContext.ChoreAssignments.Add(assignment);
        await dbContext.SaveChangesAsync();

        var response = new ChoreAssignmentResponse(
            assignment.Id,
            assignment.ChoreId,
            assignment.ChildId,
            assignment.Points,
            assignment.AssignedAt,
            assignment.DueDate);

        return Created("/api/chore-assignments", response);
    }

    [HttpDelete("{assignmentId:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Cancel(int assignmentId)
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

        // Combining the route ID with every Household relationship makes a forged
        // assignment ID indistinguishable from a missing resource.
        var assignment = await dbContext.ChoreAssignments.SingleOrDefaultAsync(item =>
            item.Id == assignmentId
            && item.HouseholdId == currentUser.HouseholdId
            && item.Child.HouseholdId == currentUser.HouseholdId
            && item.Chore.HouseholdId == currentUser.HouseholdId);

        if (assignment is null)
        {
            return NotFound();
        }

        if (assignment.Status is ChoreAssignmentStatus.Approved
            or ChoreAssignmentStatus.Cancelled)
        {
            return Conflict(new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Assignment cannot be cancelled",
                Detail = assignment.Status == ChoreAssignmentStatus.Approved
                    ? "An approved assignment and its awarded points must be preserved."
                    : "The assignment has already been cancelled."
            });
        }

        assignment.Status = ChoreAssignmentStatus.Cancelled;
        assignment.CancelledByUserId = currentUser.Id;
        assignment.CancelledAt = timeProvider.GetUtcNow().UtcDateTime;

        try
        {
            await dbContext.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            return Conflict(new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Assignment cannot be cancelled",
                Detail = "The assignment was already changed by another request."
            });
        }

        return NoContent();
    }

    [HttpGet]
    [ProducesResponseType<IReadOnlyList<AdultChoreAssignmentResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<AdultChoreAssignmentResponse>>> GetAll(
        [FromQuery] bool includeCancelled = false)
    {
        var currentUser = await userManager.GetUserAsync(User);

        if (currentUser is null)
        {
            return Unauthorized();
        }

        var assignments = await dbContext.ChoreAssignments
            .AsNoTracking()
            .Where(assignment =>
                assignment.HouseholdId == currentUser.HouseholdId
                && assignment.Child.HouseholdId == currentUser.HouseholdId
                && assignment.Chore.HouseholdId == currentUser.HouseholdId
                && (includeCancelled || assignment.Status != ChoreAssignmentStatus.Cancelled))
            .OrderByDescending(assignment => assignment.SubmittedAt)
            .ThenByDescending(assignment => assignment.AssignedAt)
            .ThenByDescending(assignment => assignment.Id)
            .Select(assignment => new AdultChoreAssignmentResponse(
                assignment.Id,
                assignment.ChoreId,
                assignment.Chore.Title,
                assignment.ChildId,
                assignment.Child.Name,
                assignment.Points,
                assignment.AssignedAt,
                assignment.DueDate,
                assignment.Status.ToString(),
                assignment.SubmittedAt,
                assignment.ReviewedByUserId,
                assignment.ReviewedAt,
                assignment.ReviewComment,
                assignment.CancelledByUserId,
                assignment.CancelledAt,
                assignment.AdultArchivedAt))
            .ToListAsync();

        return Ok(assignments);
    }

    [HttpPost("{assignmentId:int}/archive")]
    public Task<IActionResult> Archive(int assignmentId) => SetArchiveState(assignmentId, archived: true);

    [HttpPost("{assignmentId:int}/restore")]
    public Task<IActionResult> Restore(int assignmentId) => SetArchiveState(assignmentId, archived: false);

    private async Task<IActionResult> SetArchiveState(int assignmentId, bool archived)
    {
        if (assignmentId <= 0)
        {
            ModelState.AddModelError(nameof(assignmentId), "Assignment ID must be positive.");
            return ValidationProblem(ModelState);
        }

        var currentUser = await userManager.GetUserAsync(User);
        if (currentUser is null) return Unauthorized();

        var assignment = await dbContext.ChoreAssignments.SingleOrDefaultAsync(item =>
            item.Id == assignmentId
            && item.HouseholdId == currentUser.HouseholdId
            && item.Child.HouseholdId == currentUser.HouseholdId
            && item.Chore.HouseholdId == currentUser.HouseholdId);
        if (assignment is null) return NotFound();

        if (assignment.Status is not (ChoreAssignmentStatus.Approved or ChoreAssignmentStatus.Cancelled))
        {
            return Conflict(new ProblemDetails
            {
                Title = "Only completed assignments can be archived",
                Status = StatusCodes.Status409Conflict
            });
        }

        assignment.AdultArchivedAt = archived ? timeProvider.GetUtcNow().UtcDateTime : null;
        await dbContext.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("{assignmentId:int}/approve")]
    [ProducesResponseType<ReviewChoreAssignmentResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public Task<ActionResult<ReviewChoreAssignmentResponse>> Approve(
        int assignmentId,
        ReviewChoreAssignmentRequest request) => Review(assignmentId, request, approve: true);

    [HttpPost("{assignmentId:int}/reject")]
    [ProducesResponseType<ReviewChoreAssignmentResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public Task<ActionResult<ReviewChoreAssignmentResponse>> Reject(
        int assignmentId,
        ReviewChoreAssignmentRequest request) => Review(assignmentId, request, approve: false);

    private async Task<ActionResult<ReviewChoreAssignmentResponse>> Review(
        int assignmentId,
        ReviewChoreAssignmentRequest request,
        bool approve)
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

        var assignment = await dbContext.ChoreAssignments.SingleOrDefaultAsync(item =>
            item.Id == assignmentId
            && item.HouseholdId == currentUser.HouseholdId
            && item.Child.HouseholdId == currentUser.HouseholdId
            && item.Chore.HouseholdId == currentUser.HouseholdId);

        if (assignment is null)
        {
            return NotFound();
        }

        if (assignment.Status != ChoreAssignmentStatus.PendingApproval)
        {
            return Conflict(new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Assignment cannot be reviewed",
                Detail = "Only an assignment waiting for approval can be reviewed."
            });
        }

        var reviewedAt = timeProvider.GetUtcNow().UtcDateTime;
        var comment = string.IsNullOrWhiteSpace(request.Comment)
            ? null
            : request.Comment.Trim();
        assignment.Status = approve
            ? ChoreAssignmentStatus.Approved
            : ChoreAssignmentStatus.NeedsRedo;
        assignment.ReviewedByUserId = currentUser.Id;
        assignment.ReviewedAt = reviewedAt;
        assignment.ReviewComment = comment;

        if (approve)
        {
            dbContext.ChoreCompletions.Add(new ChoreCompletion
            {
                HouseholdId = currentUser.HouseholdId,
                AssignmentId = assignment.Id,
                ChildId = assignment.ChildId,
                ChoreId = assignment.ChoreId,
                ApprovedByUserId = currentUser.Id,
                ApprovedAt = reviewedAt,
                PointsAwarded = assignment.Points
            });
        }

        try
        {
            await dbContext.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            return Conflict(new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Assignment cannot be reviewed",
                Detail = "The assignment was already reviewed by another request."
            });
        }

        return Ok(new ReviewChoreAssignmentResponse(
            assignment.Id,
            assignment.Status.ToString(),
            reviewedAt,
            comment,
            approve ? assignment.Points : null));
    }
}
