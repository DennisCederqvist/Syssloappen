using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Syssloappen.Api.Authentication;
using Syssloappen.Api.Data;
using Syssloappen.Api.Dtos.Chores;
using Syssloappen.Api.Models;

namespace Syssloappen.Api.Controllers;

[ApiController]
[Route("api/chores")]
[Authorize(Roles = RoleNames.Adult)]
public sealed class ChoresController(
    AppDbContext dbContext,
    UserManager<ApplicationUser> userManager,
    TimeProvider timeProvider) : ControllerBase
{
    private static readonly int[] AllowedPointValues = [5, 10, 15, 20];

    [HttpPost]
    [ProducesResponseType<ChoreResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ChoreResponse>> Create(CreateChoreRequest request)
    {
        var title = request.Title.Trim();

        if (title.Length == 0)
        {
            ModelState.AddModelError(nameof(request.Title), "A chore title is required.");
            return ValidationProblem(ModelState);
        }

        if (!AllowedPointValues.Contains(request.Points))
        {
            ModelState.AddModelError(nameof(request.Points), "Points must be 5, 10, 15 or 20.");
            return ValidationProblem(ModelState);
        }

        var currentUser = await userManager.GetUserAsync(User);

        if (currentUser is null)
        {
            return Unauthorized();
        }

        var description = string.IsNullOrWhiteSpace(request.Description)
            ? null
            : request.Description.Trim();
        var chore = new Chore
        {
            // Both ownership values come from the authenticated Adult. Request JSON
            // cannot choose a Household or impersonate another creating account.
            HouseholdId = currentUser.HouseholdId,
            CreatedByUserId = currentUser.Id,
            Title = title,
            Description = description,
            Points = request.Points,
            IsActive = true,
            CreatedAt = timeProvider.GetUtcNow().UtcDateTime
        };

        dbContext.Chores.Add(chore);
        await dbContext.SaveChangesAsync();

        return CreatedAtAction(nameof(GetAll), ToResponse(chore));
    }

    [HttpPut("{choreId:int}")]
    [ProducesResponseType<ChoreResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ChoreResponse>> Update(
        int choreId,
        UpdateChoreRequest request)
    {
        if (choreId <= 0)
        {
            ModelState.AddModelError(nameof(choreId), "Chore ID must be positive.");
            return ValidationProblem(ModelState);
        }

        var title = request.Title.Trim();

        if (title.Length == 0)
        {
            ModelState.AddModelError(nameof(request.Title), "A chore title is required.");
            return ValidationProblem(ModelState);
        }

        if (!AllowedPointValues.Contains(request.Points))
        {
            ModelState.AddModelError(nameof(request.Points), "Points must be 5, 10, 15 or 20.");
            return ValidationProblem(ModelState);
        }

        var currentUser = await userManager.GetUserAsync(User);

        if (currentUser is null)
        {
            return Unauthorized();
        }

        // The resource ID is combined with the authenticated Adult's HouseholdId.
        // A forged ID therefore cannot select another family's chore.
        var chore = await dbContext.Chores.SingleOrDefaultAsync(item =>
            item.Id == choreId
            && item.HouseholdId == currentUser.HouseholdId
            && item.IsActive);

        if (chore is null)
        {
            return NotFound();
        }

        chore.Title = title;
        chore.Description = string.IsNullOrWhiteSpace(request.Description)
            ? null
            : request.Description.Trim();
        chore.Points = request.Points;
        await dbContext.SaveChangesAsync();

        return Ok(ToResponse(chore));
    }

    [HttpDelete("{choreId:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Deactivate(int choreId)
    {
        if (choreId <= 0)
        {
            ModelState.AddModelError(nameof(choreId), "Chore ID must be positive.");
            return ValidationProblem(ModelState);
        }

        var currentUser = await userManager.GetUserAsync(User);

        if (currentUser is null)
        {
            return Unauthorized();
        }

        var chore = await dbContext.Chores.SingleOrDefaultAsync(item =>
            item.Id == choreId
            && item.HouseholdId == currentUser.HouseholdId
            && item.IsActive);

        if (chore is null)
        {
            return NotFound();
        }

        // Keep the row and all historical relationships. Only future use through
        // the active chore bank and assignment endpoint is disabled.
        chore.IsActive = false;
        await dbContext.SaveChangesAsync();

        return NoContent();
    }

    [HttpGet]
    [ProducesResponseType<IReadOnlyList<ChoreResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<ChoreResponse>>> GetAll()
    {
        var currentUser = await userManager.GetUserAsync(User);

        if (currentUser is null)
        {
            return Unauthorized();
        }

        // The Household filter is part of the SQL query, so other families' chores
        // are never loaded into memory or considered for the response.
        var chores = await dbContext.Chores
            .AsNoTracking()
            .Where(chore =>
                chore.HouseholdId == currentUser.HouseholdId
                && chore.IsActive)
            .OrderBy(chore => chore.Title)
            .ThenBy(chore => chore.Id)
            .Select(chore => new ChoreResponse(
                chore.Id,
                chore.Title,
                chore.Description,
                chore.Points,
                chore.CreatedAt))
            .ToListAsync();

        return Ok(chores);
    }

    private static ChoreResponse ToResponse(Chore chore) => new(
        chore.Id,
        chore.Title,
        chore.Description,
        chore.Points,
        chore.CreatedAt);
}
