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
            CreatedAt = timeProvider.GetUtcNow().UtcDateTime
        };

        dbContext.Chores.Add(chore);
        await dbContext.SaveChangesAsync();

        return CreatedAtAction(nameof(GetAll), ToResponse(chore));
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
            .Where(chore => chore.HouseholdId == currentUser.HouseholdId)
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
