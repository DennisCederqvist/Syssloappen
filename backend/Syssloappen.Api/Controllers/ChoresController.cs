using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Syssloappen.Api.Authentication;
using Syssloappen.Api.Data;
using Syssloappen.Api.Dtos.Chores;
using Syssloappen.Api.Models;
using Syssloappen.Api.Services;

namespace Syssloappen.Api.Controllers;

[ApiController]
[Route("api/chores")]
[Authorize(Roles = RoleNames.Adult)]
public sealed class ChoresController(
    AppDbContext dbContext,
    UserManager<ApplicationUser> userManager,
    TimeProvider timeProvider,
    IRewardImageStorage imageStorage,
    INotificationDispatcher notificationDispatcher,
    ILogger<ChoresController> logger) : ControllerBase
{
    // Same bounds and formats as reward images and child photos.
    private const long MaxUploadBytes = 10 * 1024 * 1024;
    private static readonly string[] AllowedImageContentTypes = ["image/jpeg", "image/png", "image/webp"];

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
        await NotifyChildrenAsync(currentUser.HouseholdId);

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
        // the active chore bank and assignment endpoint is disabled. The picture is no
        // longer needed, so it is released along with the chore.
        var imageUrl = chore.ImageUrl;
        chore.IsActive = false;
        chore.ImageUrl = null;
        await dbContext.SaveChangesAsync();

        if (imageUrl is not null)
        {
            await imageStorage.DeleteAsync(imageUrl, HttpContext.RequestAborted);
        }

        await NotifyChildrenAsync(currentUser.HouseholdId);
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
                chore.ImageUrl,
                chore.CreatedAt))
            .ToListAsync();

        return Ok(chores);
    }

    private static ChoreResponse ToResponse(Chore chore) => new(
        chore.Id,
        chore.Title,
        chore.Description,
        chore.Points,
        chore.ImageUrl,
        chore.CreatedAt);

    [HttpPost("{choreId:int}/image")]
    [RequestSizeLimit(MaxUploadBytes)]
    [ProducesResponseType<ChoreResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ChoreResponse>> UploadImage(int choreId, IFormFile file)
    {
        if (choreId <= 0)
        {
            ModelState.AddModelError(nameof(choreId), "Chore ID must be positive.");
            return ValidationProblem(ModelState);
        }

        if (file is null || file.Length == 0)
        {
            ModelState.AddModelError(nameof(file), "An image file is required.");
            return ValidationProblem(ModelState);
        }

        if (file.Length > MaxUploadBytes)
        {
            ModelState.AddModelError(nameof(file), "The image must be 10MB or smaller.");
            return ValidationProblem(ModelState);
        }

        if (!AllowedImageContentTypes.Contains(file.ContentType))
        {
            ModelState.AddModelError(nameof(file), "The file must be a JPEG, PNG, or WebP image.");
            return ValidationProblem(ModelState);
        }

        var currentUser = await userManager.GetUserAsync(User);
        if (currentUser is null) return Unauthorized();

        var chore = await dbContext.Chores.SingleOrDefaultAsync(item =>
            item.Id == choreId
            && item.HouseholdId == currentUser.HouseholdId
            && item.IsActive);
        if (chore is null) return NotFound();

        byte[] webpContent;
        try
        {
            await using var stream = file.OpenReadStream();
            webpContent = await RewardImageProcessor.ToCompressedWebpAsync(stream, HttpContext.RequestAborted);
        }
        catch (InvalidDataException)
        {
            ModelState.AddModelError(nameof(file), "The file could not be read as an image.");
            return ValidationProblem(ModelState);
        }

        var previousImageUrl = chore.ImageUrl;
        var fileName = $"{Guid.NewGuid()}.webp";
        chore.ImageUrl = await imageStorage.SaveAsync(webpContent, fileName, HttpContext.RequestAborted);
        await dbContext.SaveChangesAsync();

        if (previousImageUrl is not null)
        {
            await imageStorage.DeleteAsync(previousImageUrl, HttpContext.RequestAborted);
        }

        await NotifyChildrenAsync(currentUser.HouseholdId);
        return Ok(ToResponse(chore));
    }

    [HttpDelete("{choreId:int}/image")]
    [ProducesResponseType<ChoreResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ChoreResponse>> DeleteImage(int choreId)
    {
        var currentUser = await userManager.GetUserAsync(User);
        if (currentUser is null) return Unauthorized();

        var chore = await dbContext.Chores.SingleOrDefaultAsync(item =>
            item.Id == choreId
            && item.HouseholdId == currentUser.HouseholdId
            && item.IsActive);
        if (chore is null) return NotFound();

        var imageUrl = chore.ImageUrl;
        chore.ImageUrl = null;
        await dbContext.SaveChangesAsync();

        if (imageUrl is not null)
        {
            await imageStorage.DeleteAsync(imageUrl, HttpContext.RequestAborted);
        }

        await NotifyChildrenAsync(currentUser.HouseholdId);
        return Ok(ToResponse(chore));
    }

    // Best-effort: the change is already committed, so a failed refresh hint must not fail the request.
    private async Task NotifyChildrenAsync(int householdId)
    {
        try
        {
            await notificationDispatcher.NotifyHouseholdChildrenAsync(
                householdId,
                new NotificationEvent(NotificationEventType.ChoresChanged, new ContentChangedData()));
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to dispatch a real-time notification.");
        }
    }
}
