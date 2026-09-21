using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Syssloappen.Api.Authentication;
using Syssloappen.Api.Data;
using Syssloappen.Api.Dtos.ChoreRecurrences;
using Syssloappen.Api.Models;
using Syssloappen.Api.Services;

namespace Syssloappen.Api.Controllers;

[ApiController]
[Route("api/chore-recurrences")]
[Authorize(Roles = RoleNames.Adult)]
public sealed class ChoreRecurrencesController(
    AppDbContext dbContext,
    UserManager<ApplicationUser> userManager,
    TimeProvider timeProvider,
    ChoreRecurrenceGenerator generator,
    INotificationDispatcher notificationDispatcher,
    ILogger<ChoreRecurrencesController> logger) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<ChoreRecurrenceResponse>> Create(CreateChoreRecurrenceRequest request)
    {
        var currentUser = await userManager.GetUserAsync(User);
        if (currentUser is null) return Unauthorized();

        // Combining every client-selected ID with the authenticated Adult's HouseholdId
        // prevents either ID from reaching another household's data.
        var chore = await dbContext.Chores.SingleOrDefaultAsync(chore =>
            chore.Id == request.ChoreId && chore.HouseholdId == currentUser.HouseholdId && chore.IsActive);
        if (chore is null) return NotFound();

        var child = await dbContext.ChildProfiles.SingleOrDefaultAsync(child =>
            child.Id == request.ChildId && child.HouseholdId == currentUser.HouseholdId && child.IsActive);
        if (child is null) return NotFound();

        if (!ChoreScheduleRules.TryNormalize(
                request.Frequency, request.DaysOfWeekMask, request.DayOfMonth,
                out var daysOfWeekMask, out var dayOfMonth, out var invalidField, out var invalidMessage))
        {
            ModelState.AddModelError(invalidField, invalidMessage);
            return ValidationProblem(ModelState);
        }

        var today = DateOnly.FromDateTime(timeProvider.GetLocalNow().DateTime);
        var startDate = request.StartDate ?? today;
        if (startDate < today)
        {
            ModelState.AddModelError(nameof(request.StartDate), "Start date cannot be in the past.");
            return ValidationProblem(ModelState);
        }

        var recurrence = new ChoreRecurrence
        {
            HouseholdId = currentUser.HouseholdId,
            ChoreId = request.ChoreId,
            ChildId = request.ChildId,
            CreatedByUserId = currentUser.Id,
            Frequency = request.Frequency,
            DaysOfWeekMask = daysOfWeekMask,
            DayOfMonth = dayOfMonth,
            StartDate = startDate,
            IsActive = true,
            CreatedAt = timeProvider.GetUtcNow().UtcDateTime
        };
        dbContext.ChoreRecurrences.Add(recurrence);
        await dbContext.SaveChangesAsync();

        // Generate today's occurrence immediately if the schedule is due today, so creating a
        // recurrence feels as instant as a one-off assignment rather than waiting for the next
        // lazy pass.
        await generator.GenerateDueAssignmentsAsync(currentUser.HouseholdId);
        await NotifyChildOfGeneratedOccurrenceAsync(recurrence, chore, today);

        return CreatedAtAction(nameof(GetAll), ToResponse(recurrence, chore.Title, child.Name));
    }

    [HttpPut("{recurrenceId:int}")]
    public async Task<ActionResult<ChoreRecurrenceResponse>> Update(
        int recurrenceId, UpdateChoreRecurrenceRequest request)
    {
        if (recurrenceId <= 0)
        {
            ModelState.AddModelError(nameof(recurrenceId), "Recurrence ID must be positive.");
            return ValidationProblem(ModelState);
        }

        var currentUser = await userManager.GetUserAsync(User);
        if (currentUser is null) return Unauthorized();

        var recurrence = await dbContext.ChoreRecurrences
            .Include(item => item.Chore)
            .Include(item => item.Child)
            .SingleOrDefaultAsync(item =>
                item.Id == recurrenceId && item.HouseholdId == currentUser.HouseholdId && item.IsActive);
        if (recurrence is null) return NotFound();

        if (!ChoreScheduleRules.TryNormalize(
                request.Frequency, request.DaysOfWeekMask, request.DayOfMonth,
                out var daysOfWeekMask, out var dayOfMonth, out var invalidField, out var invalidMessage))
        {
            ModelState.AddModelError(invalidField, invalidMessage);
            return ValidationProblem(ModelState);
        }

        var today = DateOnly.FromDateTime(timeProvider.GetLocalNow().DateTime);
        var hadOccurrenceToday = await HasOccurrenceOnAsync(recurrence.Id, today);

        recurrence.Frequency = request.Frequency;
        recurrence.DaysOfWeekMask = daysOfWeekMask;
        recurrence.DayOfMonth = dayOfMonth;
        await dbContext.SaveChangesAsync();

        // The new schedule may be due today while the old one was not.
        await generator.GenerateDueAssignmentsAsync(currentUser.HouseholdId);
        if (!hadOccurrenceToday)
        {
            await NotifyChildOfGeneratedOccurrenceAsync(recurrence, recurrence.Chore, today);
        }

        return Ok(ToResponse(recurrence, recurrence.Chore.Title, recurrence.Child.Name));
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ChoreRecurrenceResponse>>> GetAll()
    {
        var currentUser = await userManager.GetUserAsync(User);
        if (currentUser is null) return Unauthorized();

        var recurrences = await dbContext.ChoreRecurrences
            .AsNoTracking()
            .Where(recurrence => recurrence.HouseholdId == currentUser.HouseholdId && recurrence.IsActive)
            .OrderBy(recurrence => recurrence.Chore.Title).ThenBy(recurrence => recurrence.Child.Name)
            .Select(recurrence => new ChoreRecurrenceResponse(
                recurrence.Id,
                recurrence.ChoreId,
                recurrence.Chore.Title,
                recurrence.ChildId,
                recurrence.Child.Name,
                recurrence.Frequency.ToString(),
                recurrence.DaysOfWeekMask,
                recurrence.DayOfMonth,
                recurrence.StartDate))
            .ToListAsync();

        return Ok(recurrences);
    }

    [HttpDelete("{recurrenceId:int}")]
    public async Task<IActionResult> Deactivate(int recurrenceId)
    {
        if (recurrenceId <= 0)
        {
            ModelState.AddModelError(nameof(recurrenceId), "Recurrence ID must be positive.");
            return ValidationProblem(ModelState);
        }

        var currentUser = await userManager.GetUserAsync(User);
        if (currentUser is null) return Unauthorized();

        var recurrence = await dbContext.ChoreRecurrences.SingleOrDefaultAsync(item =>
            item.Id == recurrenceId && item.HouseholdId == currentUser.HouseholdId && item.IsActive);
        if (recurrence is null) return NotFound();

        // Soft: stops future generation only. Already-generated assignments and their
        // completion/points history are untouched, matching every other deactivation here.
        recurrence.IsActive = false;
        await dbContext.SaveChangesAsync();
        return NoContent();
    }

    private Task<bool> HasOccurrenceOnAsync(int recurrenceId, DateOnly date) =>
        dbContext.ChoreAssignments.AnyAsync(assignment =>
            assignment.GeneratedFromRecurrenceId == recurrenceId && assignment.DueDate == date);

    // Best-effort: the recurrence is already saved, so a failed live update must not fail the request.
    private async Task NotifyChildOfGeneratedOccurrenceAsync(ChoreRecurrence recurrence, Chore chore, DateOnly today)
    {
        try
        {
            var occurrence = await dbContext.ChoreAssignments
                .AsNoTracking()
                .Where(assignment =>
                    assignment.GeneratedFromRecurrenceId == recurrence.Id && assignment.DueDate == today)
                .Select(assignment => new { assignment.Id, assignment.Points })
                .SingleOrDefaultAsync();

            if (occurrence is null) return;

            await notificationDispatcher.NotifyChildAsync(
                recurrence.ChildId,
                new NotificationEvent(
                    NotificationEventType.ChoreAssigned,
                    new ChoreAssignedData(occurrence.Id, chore.Title, occurrence.Points)));
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to dispatch a real-time notification.");
        }
    }

    private static ChoreRecurrenceResponse ToResponse(ChoreRecurrence recurrence, string choreTitle, string childName) =>
        new(recurrence.Id, recurrence.ChoreId, choreTitle, recurrence.ChildId, childName,
            recurrence.Frequency.ToString(), recurrence.DaysOfWeekMask, recurrence.DayOfMonth, recurrence.StartDate);
}
