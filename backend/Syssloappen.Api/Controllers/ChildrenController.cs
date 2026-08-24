using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Syssloappen.Api.Authentication;
using Syssloappen.Api.Data;
using Syssloappen.Api.Dtos.Children;
using Syssloappen.Api.Models;

namespace Syssloappen.Api.Controllers;

[ApiController]
[Route("api/children")]
[Authorize(Roles = RoleNames.Adult)]
public sealed class ChildrenController(
    AppDbContext dbContext,
    UserManager<ApplicationUser> userManager)
    : ControllerBase
{
    [HttpPost]
    [ProducesResponseType<ChildResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ChildResponse>> Create(CreateChildRequest request)
    {
        var name = request.Name.Trim();

        if (name.Length == 0)
        {
            ModelState.AddModelError(nameof(request.Name), "A child name is required.");
            return ValidationProblem(ModelState);
        }

        var currentUser = await userManager.GetUserAsync(User);

        if (currentUser is null)
        {
            return Unauthorized();
        }

        var child = new ChildProfile
        {
            Name = name,
            // HouseholdId comes only from the authenticated user, never from the request.
            HouseholdId = currentUser.HouseholdId
        };

        dbContext.ChildProfiles.Add(child);
        await dbContext.SaveChangesAsync();

        var response = new ChildResponse(child.Id, child.Name);
        return CreatedAtAction(nameof(GetAll), response);
    }

    [HttpGet]
    [ProducesResponseType<IReadOnlyList<ChildResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<ChildResponse>>> GetAll()
    {
        var currentUser = await userManager.GetUserAsync(User);

        if (currentUser is null)
        {
            return Unauthorized();
        }

        // Filtering in the database prevents children from other households from being loaded.
        var children = await dbContext.ChildProfiles
            .AsNoTracking()
            .Where(child => child.HouseholdId == currentUser.HouseholdId)
            .OrderBy(child => child.Name)
            .Select(child => new ChildResponse(child.Id, child.Name))
            .ToListAsync();

        return Ok(children);
    }
}
