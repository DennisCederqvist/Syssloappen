using System.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Syssloappen.Api.Authentication;
using Syssloappen.Api.Data;
using Syssloappen.Api.Dtos.Households;
using Syssloappen.Api.Services;

namespace Syssloappen.Api.Controllers;

[ApiController]
[Route("api/household/adults")]
[Authorize(Roles = RoleNames.Adult)]
public sealed class HouseholdAdultsController(
    AppDbContext dbContext,
    UserManager<ApplicationUser> userManager,
    TimeProvider timeProvider,
    IEmailSender emailSender,
    IConfiguration configuration) : ControllerBase
{
    private const int GracePeriodDays = 30;

    [HttpGet]
    [ProducesResponseType<List<HouseholdAdultResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<List<HouseholdAdultResponse>>> List()
    {
        var currentUser = await userManager.GetUserAsync(User);

        if (currentUser is null)
        {
            return Unauthorized();
        }

        var household = await dbContext.Households
            .AsNoTracking()
            .SingleAsync(candidate => candidate.Id == currentUser.HouseholdId);

        // Household.Users also includes Child accounts, so the Adult role must be
        // joined explicitly rather than inferred from HouseholdId alone. Disconnected
        // adults are excluded — they no longer belong to the active household, even
        // though their historical rows and references are preserved.
        var adults = await (
            from user in dbContext.Users.AsNoTracking()
            join userRole in dbContext.UserRoles.AsNoTracking() on user.Id equals userRole.UserId
            join role in dbContext.Roles.AsNoTracking() on userRole.RoleId equals role.Id
            where user.HouseholdId == currentUser.HouseholdId
                && role.Name == RoleNames.Adult
                && user.DisconnectedAt == null
            orderby user.Id == household.OwnerUserId descending, user.NormalizedEmail
            select new HouseholdAdultResponse(
                user.Id,
                user.Email!,
                user.Id == household.OwnerUserId,
                user.Nickname ?? user.FirstName))
            .ToListAsync();

        return Ok(adults);
    }

    [HttpDelete("{userId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Disconnect(string userId)
    {
        var currentUser = await userManager.GetUserAsync(User);

        if (currentUser is null)
        {
            return Unauthorized();
        }

        var household = await dbContext.Households
            .SingleAsync(candidate => candidate.Id == currentUser.HouseholdId);

        // The target must be an active Adult in the caller's own Household. A foreign,
        // Child, nonexistent or already-disconnected ID is treated identically as 404.
        var target = await (
            from user in dbContext.Users
            join userRole in dbContext.UserRoles on user.Id equals userRole.UserId
            join role in dbContext.Roles on userRole.RoleId equals role.Id
            where user.Id == userId
                && user.HouseholdId == currentUser.HouseholdId
                && role.Name == RoleNames.Adult
                && user.DisconnectedAt == null
            select user)
            .SingleOrDefaultAsync();

        if (target is null)
        {
            return NotFound();
        }

        // The owner can never be disconnected, by anyone, including themselves — only
        // permanent, explicitly-confirmed family deletion (a separate, later step) can
        // remove them.
        if (target.Id == household.OwnerUserId)
        {
            return Conflict(new ProblemDetails
            {
                Title = "Huvudägaren kan inte kopplas bort.",
                Status = StatusCodes.Status409Conflict
            });
        }

        var activeAdultCount = await (
            from user in dbContext.Users
            join userRole in dbContext.UserRoles on user.Id equals userRole.UserId
            join role in dbContext.Roles on userRole.RoleId equals role.Id
            where user.HouseholdId == currentUser.HouseholdId
                && role.Name == RoleNames.Adult
                && user.DisconnectedAt == null
            select user.Id)
            .CountAsync();

        // Defense in depth alongside the owner check above: a Household must never be
        // left with zero active Adults, regardless of which rule would otherwise allow it.
        if (activeAdultCount <= 1)
        {
            return Conflict(new ProblemDetails
            {
                Title = "Sista vuxna kan inte kopplas bort.",
                Status = StatusCodes.Status409Conflict
            });
        }

        // A normal disconnection preserves the row (and every historical reference to
        // it) but revokes login and frees the email for a future, unrelated registration.
        target.DisconnectedAt = timeProvider.GetUtcNow().UtcDateTime;
        target.Email = null;
        target.NormalizedEmail = null;
        target.UserName = null;
        target.NormalizedUserName = null;
        await dbContext.SaveChangesAsync();

        return NoContent();
    }

    [HttpGet("~/api/household/deletion-status")]
    [ProducesResponseType<AccountDeletionStatusResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AccountDeletionStatusResponse>> DeletionStatus()
    {
        var currentUser = await userManager.GetUserAsync(User);

        if (currentUser is null)
        {
            return Unauthorized();
        }

        var household = await dbContext.Households
            .AsNoTracking()
            .SingleAsync(candidate => candidate.Id == currentUser.HouseholdId);

        return Ok(new AccountDeletionStatusResponse(household.DeletionScheduledAt));
    }

    [HttpPost("~/api/household/delete-account")]
    [ProducesResponseType<AccountDeletionStatusResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AccountDeletionStatusResponse>> ScheduleAccountDeletion(
        ScheduleAccountDeletionRequest request)
    {
        var currentUser = await userManager.GetUserAsync(User);

        if (currentUser is null)
        {
            return Unauthorized();
        }

        var household = await dbContext.Households
            .SingleAsync(candidate => candidate.Id == currentUser.HouseholdId);

        // Only the owner can trigger deletion of the whole Household — the same
        // permanence the owner already has protection from in Disconnect above.
        if (currentUser.Id != household.OwnerUserId)
        {
            return Conflict(new ProblemDetails
            {
                Title = "Endast huvudägaren kan radera kontot.",
                Status = StatusCodes.Status409Conflict
            });
        }

        if (household.DeletionScheduledAt is not null)
        {
            return Conflict(new ProblemDetails
            {
                Title = "Radering är redan schemalagd.",
                Status = StatusCodes.Status409Conflict
            });
        }

        var passwordCorrect = await userManager.CheckPasswordAsync(currentUser, request.Password);
        if (!passwordCorrect)
        {
            return ValidationProblem(new ValidationProblemDetails(
                new Dictionary<string, string[]> { ["Password"] = ["Fel lösenord."] })
            {
                Status = StatusCodes.Status400BadRequest
            });
        }

        var scheduledFor = timeProvider.GetUtcNow().UtcDateTime.AddDays(GracePeriodDays);
        household.DeletionScheduledAt = scheduledFor;
        await dbContext.SaveChangesAsync();

        await SendDeletionScheduledEmailAsync(currentUser, scheduledFor);

        return Ok(new AccountDeletionStatusResponse(scheduledFor));
    }

    [HttpPost("~/api/household/cancel-account-deletion")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CancelAccountDeletion()
    {
        var currentUser = await userManager.GetUserAsync(User);

        if (currentUser is null)
        {
            return Unauthorized();
        }

        var household = await dbContext.Households
            .SingleAsync(candidate => candidate.Id == currentUser.HouseholdId);

        if (currentUser.Id != household.OwnerUserId)
        {
            return Conflict(new ProblemDetails
            {
                Title = "Endast huvudägaren kan avbryta raderingen.",
                Status = StatusCodes.Status409Conflict
            });
        }

        if (household.DeletionScheduledAt is null)
        {
            return Conflict(new ProblemDetails
            {
                Title = "Ingen radering är schemalagd.",
                Status = StatusCodes.Status409Conflict
            });
        }

        household.DeletionScheduledAt = null;
        await dbContext.SaveChangesAsync();

        await SendDeletionCancelledEmailAsync(currentUser);

        return NoContent();
    }

    [HttpPut("me")]
    [ProducesResponseType<HouseholdAdultResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<HouseholdAdultResponse>> UpdateOwnProfile(UpdateAdultProfileRequest request)
    {
        var currentUser = await userManager.GetUserAsync(User);

        if (currentUser is null)
        {
            return Unauthorized();
        }

        var household = await dbContext.Households
            .AsNoTracking()
            .SingleAsync(candidate => candidate.Id == currentUser.HouseholdId);

        // A blank field clears the previous value rather than being ignored — the Adult
        // is always editing their complete profile, not applying a partial patch.
        currentUser.FirstName = NormalizeOptional(request.FirstName);
        currentUser.LastName = NormalizeOptional(request.LastName);
        currentUser.Nickname = NormalizeOptional(request.Nickname);
        await userManager.UpdateAsync(currentUser);

        return Ok(new HouseholdAdultResponse(
            currentUser.Id,
            currentUser.Email!,
            currentUser.Id == household.OwnerUserId,
            currentUser.Nickname ?? currentUser.FirstName));
    }

    [HttpPost("me/change-password")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ChangeOwnPassword(ChangeAdultPasswordRequest request)
    {
        var currentUser = await userManager.GetUserAsync(User);

        if (currentUser is null)
        {
            return Unauthorized();
        }

        var result = await userManager.ChangePasswordAsync(
            currentUser,
            request.CurrentPassword,
            request.NewPassword);

        if (!result.Succeeded)
        {
            var errors = result.Errors
                .GroupBy(error => error.Code)
                .ToDictionary(group => group.Key, group => group.Select(error => error.Description).ToArray());
            return ValidationProblem(new ValidationProblemDetails(errors)
            {
                Status = StatusCodes.Status400BadRequest
            });
        }

        return NoContent();
    }

    private static string? NormalizeOptional(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }

    // See AuthController.PublicBaseUrl for why this must always be explicitly configured
    // and never fall back to Request.Host.
    private string PublicBaseUrl => configuration["PublicBaseUrl"]
        ?? throw new InvalidOperationException("PublicBaseUrl is not configured.");

    private async Task SendDeletionScheduledEmailAsync(ApplicationUser owner, DateTime scheduledFor)
    {
        var name = owner.Nickname ?? owner.FirstName;
        var greeting = string.IsNullOrEmpty(name) ? "Hi!" : $"Hi {WebUtility.HtmlEncode(name)}!";
        var settingsUrl = $"{PublicBaseUrl}/vuxen/installningar/vuxna";
        var html = $"""
            <p>{greeting}</p>
            <p>Your Sysslo account and the entire family's data are now scheduled for permanent
            deletion on {scheduledFor:yyyy-MM-dd}. Until then, everything keeps working as usual.</p>
            <p>If you change your mind, you can cancel the deletion at any time before then, under
            Settings → Adults: <a href="{settingsUrl}">{settingsUrl}</a></p>
            <p>If this wasn't you, you should change your password right away.</p>
            """;

        await emailSender.SendAsync(owner.Email!, name, "Your Sysslo account is scheduled for deletion", html);
    }

    private async Task SendDeletionCancelledEmailAsync(ApplicationUser owner)
    {
        var name = owner.Nickname ?? owner.FirstName;
        var greeting = string.IsNullOrEmpty(name) ? "Hi!" : $"Hi {WebUtility.HtmlEncode(name)}!";
        var html = $"""
            <p>{greeting}</p>
            <p>The deletion of your Sysslo account has been cancelled. The family's data is intact
            and everything keeps working as usual.</p>
            <p>If this wasn't you, you should change your password right away.</p>
            """;

        await emailSender.SendAsync(owner.Email!, name, "Deletion of your Sysslo account cancelled", html);
    }
}
