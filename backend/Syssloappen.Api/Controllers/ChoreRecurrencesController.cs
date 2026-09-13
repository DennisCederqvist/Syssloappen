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
    ChoreRecurrenceGenerator generator) : ControllerBase
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

        int? daysOfWeekMask;
        int? dayOfMonth;
        switch (request.Frequency)
        {
            case ChoreRecurrenceFrequency.Weekly:
                if (request.DaysOfWeekMask is null || !IsSingleBitSet(request.DaysOfWeekMask.Value))
                {
                    ModelState.AddModelError(
                        nameof(request.DaysOfWeekMask), "Weekly recurrence requires exactly one weekday.");
                    return ValidationProblem(ModelState);
                }
                daysOfWeekMask = request.DaysOfWeekMask;
                dayOfMonth = null;
                break;
            case ChoreRecurrenceFrequency.Custom:
                if (request.DaysOfWeekMask is null or 0 or > 127)
                {
                    ModelState.AddModelError(
                        nameof(request.DaysOfWeekMask), "Custom recurrence requires at least one weekday.");
                    return ValidationProblem(ModelState);
                }
                daysOfWeekMask = request.DaysOfWeekMask;
                dayOfMonth = null;
                break;
            case ChoreRecurrenceFrequency.Monthly:
                if (request.DayOfMonth is null or < 1 or > 31)
                {
                    ModelState.AddModelError(
                        nameof(request.DayOfMonth), "Monthly recurrence requires a day of month between 1 and 31.");
                    return ValidationProblem(ModelState);
                }
                daysOfWeekMask = null;
                dayOfMonth = request.DayOfMonth;
                break;
            default:
                daysOfWeekMask = null;
                dayOfMonth = null;
                break;
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

        return CreatedAtAction(nameof(GetAll), ToResponse(recurrence, chore.Title, child.Name));
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

    private static bool IsSingleBitSet(int value) => value > 0 && (value & (value - 1)) == 0;

    private static ChoreRecurrenceResponse ToResponse(ChoreRecurrence recurrence, string choreTitle, string childName) =>
        new(recurrence.Id, recurrence.ChoreId, choreTitle, recurrence.ChildId, childName,
            recurrence.Frequency.ToString(), recurrence.DaysOfWeekMask, recurrence.DayOfMonth, recurrence.StartDate);
}
