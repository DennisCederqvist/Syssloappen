using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Syssloappen.Api.Authentication;
using Syssloappen.Api.Data;
using Syssloappen.Api.Dtos.Households;
using Syssloappen.Api.Models;

namespace Syssloappen.Api.Controllers;

[ApiController]
[Route("api/household/invitations")]
[Authorize(Roles = RoleNames.Adult)]
public sealed class HouseholdInvitationsController(
    AppDbContext dbContext,
    UserManager<ApplicationUser> userManager,
    TimeProvider timeProvider) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType<CreateHouseholdInvitationResponse>(StatusCodes.Status201Created)]
    public async Task<ActionResult<CreateHouseholdInvitationResponse>> Create()
    {
        var currentUser = await userManager.GetUserAsync(User);
        if (currentUser is null)
        {
            return Unauthorized();
        }

        var invitation = HouseholdInvitationService.Generate();
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var expiresAt = now.AddHours(24);

        dbContext.HouseholdInvitations.Add(new HouseholdInvitation
        {
            HouseholdId = currentUser.HouseholdId,
            CreatedByUserId = currentUser.Id,
            CodeHash = invitation.Hash,
            CreatedAt = now,
            ExpiresAt = expiresAt
        });
        await dbContext.SaveChangesAsync();

        return StatusCode(
            StatusCodes.Status201Created,
            new CreateHouseholdInvitationResponse(invitation.Code, expiresAt));
    }
}