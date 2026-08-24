using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Syssloappen.Api.Authentication;
using Syssloappen.Api.Data;
using Syssloappen.Api.Dtos.Children;

namespace Syssloappen.Api.Controllers;

[ApiController]
[Route("api/children/{childId:int}/device-sessions")]
[Authorize(Roles = RoleNames.Adult)]
public sealed class ChildDeviceSessionsController(
    AppDbContext dbContext,
    UserManager<ApplicationUser> userManager,
    TimeProvider timeProvider) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<ChildDeviceSessionResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<ChildDeviceSessionResponse>>> GetAll(int childId)
    {
        var currentUser = await userManager.GetUserAsync(User);

        if (currentUser is null)
        {
            return Unauthorized();
        }

        var childExists = await dbContext.ChildProfiles.AnyAsync(child =>
            child.Id == childId && child.HouseholdId == currentUser.HouseholdId);

        if (!childExists)
        {
            return NotFound();
        }

        var sessions = await dbContext.ChildDeviceSessions
            .AsNoTracking()
            .Where(session =>
                session.ChildProfileId == childId
                && session.HouseholdId == currentUser.HouseholdId)
            .OrderByDescending(session => session.CreatedAt)
            .Select(session => new ChildDeviceSessionResponse(
                session.Id,
                session.CreatedAt,
                session.LastSeenAt,
                session.ExpiresAt,
                session.AbsoluteExpiresAt,
                session.RevokedAt))
            .ToListAsync();

        return Ok(sessions);
    }

    [HttpDelete("{sessionId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Revoke(int childId, Guid sessionId)
    {
        var currentUser = await userManager.GetUserAsync(User);

        if (currentUser is null)
        {
            return Unauthorized();
        }

        // All caller-controlled IDs are combined with the authenticated Adult's
        // HouseholdId, so neither ID can select another household's session.
        var session = await dbContext.ChildDeviceSessions.SingleOrDefaultAsync(deviceSession =>
            deviceSession.Id == sessionId
            && deviceSession.ChildProfileId == childId
            && deviceSession.HouseholdId == currentUser.HouseholdId);

        if (session is null)
        {
            return NotFound();
        }

        session.RevokedAt ??= timeProvider.GetUtcNow().UtcDateTime;
        await dbContext.SaveChangesAsync();
        return NoContent();
    }
}
