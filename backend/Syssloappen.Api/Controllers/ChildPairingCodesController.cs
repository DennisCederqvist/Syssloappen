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
[Route("api/children/{childId:int}/pairing-codes")]
[Authorize(Roles = RoleNames.Adult)]
public sealed class ChildPairingCodesController(
    AppDbContext dbContext,
    UserManager<ApplicationUser> userManager)
    : ControllerBase
{
    private static readonly TimeSpan CodeLifetime = TimeSpan.FromMinutes(10);

    [HttpPost]
    [ProducesResponseType<ChildPairingCodeResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ChildPairingCodeResponse>> Create(int childId)
    {
        var currentUser = await userManager.GetUserAsync(User);

        if (currentUser is null)
        {
            return Unauthorized();
        }

        // Combining the route ID with the authenticated Adult's HouseholdId prevents
        // pairing-code creation for another household's child.
        var child = await dbContext.ChildProfiles.SingleOrDefaultAsync(profile =>
            profile.Id == childId
            && profile.HouseholdId == currentUser.HouseholdId
            && profile.IsActive
            && profile.UserId != null);

        if (child is null)
        {
            return NotFound();
        }

        var code = ChildPairingCodeService.Generate();
        var now = DateTime.UtcNow;
        var pairingCode = new ChildPairingCode
        {
            HouseholdId = currentUser.HouseholdId,
            ChildProfileId = child.Id,
            CreatedByUserId = currentUser.Id,
            // Only the hash is persisted. The plaintext code is returned once.
            CodeHash = ChildPairingCodeService.Hash(code),
            CreatedAt = now,
            ExpiresAt = now.Add(CodeLifetime)
        };

        dbContext.ChildPairingCodes.Add(pairingCode);
        await dbContext.SaveChangesAsync();

        return StatusCode(
            StatusCodes.Status201Created,
            new ChildPairingCodeResponse(code, pairingCode.ExpiresAt));
    }
}
