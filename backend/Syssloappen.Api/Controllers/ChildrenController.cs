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
    [ProducesResponseType<CreateChildResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<CreateChildResponse>> Create(CreateChildRequest request)
    {
        var name = request.Name.Trim();
        var childUserName = request.UserName.Trim();

        if (name.Length == 0)
        {
            ModelState.AddModelError(nameof(request.Name), "A child name is required.");
            return ValidationProblem(ModelState);
        }

        if (childUserName.Length == 0)
        {
            ModelState.AddModelError(nameof(request.UserName), "A child username is required.");
            return ValidationProblem(ModelState);
        }

        var currentUser = await userManager.GetUserAsync(User);

        if (currentUser is null)
        {
            return Unauthorized();
        }

        var normalizedChildUserName = userManager.NormalizeName(childUserName);
        var userNameExists = await dbContext.Users.AnyAsync(user =>
            user.HouseholdId == currentUser.HouseholdId
            && user.NormalizedChildUserName == normalizedChildUserName);

        if (userNameExists)
        {
            return ConflictProblem("The child username is already used in this household.");
        }

        // Profile, Identity user and Child role are one operation. If any step fails,
        // the transaction removes every earlier database write.
        await using var transaction = await dbContext.Database.BeginTransactionAsync();
        var childUser = new ApplicationUser
        {
            // This globally unique Identity name is internal. The child only sees the
            // separate household-scoped child-friendly username.
            UserName = $"child-{Guid.NewGuid():N}",
            Email = null,
            HouseholdId = currentUser.HouseholdId,
            ChildUserName = childUserName,
            NormalizedChildUserName = normalizedChildUserName
        };

        var child = new ChildProfile
        {
            Name = name,
            // HouseholdId comes only from the authenticated user, never from the request.
            HouseholdId = currentUser.HouseholdId,
            UserId = childUser.Id
        };

        try
        {
            var createUserResult = await userManager.CreateAsync(childUser, request.Password);

            if (!createUserResult.Succeeded)
            {
                await transaction.RollbackAsync();
                return ValidationProblem(ToValidationProblem(createUserResult));
            }

            var addRoleResult = await userManager.AddToRoleAsync(childUser, RoleNames.Child);

            if (!addRoleResult.Succeeded)
            {
                await transaction.RollbackAsync();
                return ValidationProblem(ToValidationProblem(addRoleResult));
            }

            dbContext.ChildProfiles.Add(child);
            await dbContext.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync();
            return ConflictProblem("The child could not be created because its data changed.");
        }
        catch (InvalidOperationException)
        {
            await transaction.RollbackAsync();
            return Problem(
                title: "Child could not be created",
                detail: "The required Child role is unavailable.",
                statusCode: StatusCodes.Status500InternalServerError);
        }

        var response = new CreateChildResponse(child.Id, child.Name, childUserName, RoleNames.Child);
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
            .Where(child =>
                child.HouseholdId == currentUser.HouseholdId && child.IsActive)
            .OrderBy(child => child.Name)
            .Select(child => new ChildResponse(child.Id, child.Name))
            .ToListAsync();

        return Ok(children);
    }

    [HttpPut("{id:int}")]
    [ProducesResponseType<ChildResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ChildResponse>> Update(int id, UpdateChildRequest request)
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

        // Query by both IDs so a child from another household is never loaded for editing.
        var child = await dbContext.ChildProfiles.SingleOrDefaultAsync(
            child => child.Id == id
                && child.HouseholdId == currentUser.HouseholdId
                && child.IsActive);

        if (child is null)
        {
            return NotFound();
        }

        child.Name = name;
        await dbContext.SaveChangesAsync();

        return Ok(new ChildResponse(child.Id, child.Name));
    }

    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Deactivate(int id)
    {
        var currentUser = await userManager.GetUserAsync(User);

        if (currentUser is null)
        {
            return Unauthorized();
        }

        // Combining Child ID and the authenticated user's HouseholdId in one query
        // prevents a manipulated route ID from loading another household's child.
        var child = await dbContext.ChildProfiles.SingleOrDefaultAsync(
            child => child.Id == id
                && child.HouseholdId == currentUser.HouseholdId
                && child.IsActive);

        if (child is null)
        {
            return NotFound();
        }

        // Keep the row so future assignments and completions can retain their history.
        child.IsActive = false;
        await dbContext.SaveChangesAsync();

        return NoContent();
    }

    private ConflictObjectResult ConflictProblem(string detail) => Conflict(
        new ProblemDetails
        {
            Title = "Child creation conflict",
            Detail = detail,
            Status = StatusCodes.Status409Conflict
        });

    private static Dictionary<string, string[]> ToErrorDictionary(IdentityResult result) => result.Errors
        .GroupBy(error => error.Code)
        .ToDictionary(
            errors => errors.Key,
            errors => errors.Select(error => error.Description).ToArray());

    private static ValidationProblemDetails ToValidationProblem(IdentityResult result) =>
        new(ToErrorDictionary(result))
        {
            Status = StatusCodes.Status400BadRequest
        };
}
