using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Syssloappen.Api.Authentication;
using Syssloappen.Api.Data;
using Syssloappen.Api.Dtos.Households;

namespace Syssloappen.Api.Controllers;

[ApiController]
[Route("api/household/family-code")]
[Authorize(Roles = RoleNames.Adult)]
public sealed class HouseholdFamilyCodeController(
    AppDbContext dbContext,
    UserManager<ApplicationUser> userManager,
    TimeProvider timeProvider) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<FamilyCodeStatusResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<FamilyCodeStatusResponse>> GetStatus()
    {
        var currentUser = await userManager.GetUserAsync(User);

        if (currentUser is null)
        {
            return Unauthorized();
        }

        var household = await dbContext.Households
            .AsNoTracking()
            .SingleAsync(candidate => candidate.Id == currentUser.HouseholdId);
        var isConfigured = household.FamilyCodeLastFour is not null;
        var maskedCode = isConfigured ? $"****-****-{household.FamilyCodeLastFour}" : null;

        return Ok(new FamilyCodeStatusResponse(
            isConfigured,
            maskedCode,
            household.FamilyCodeUpdatedAt));
    }

    [HttpPost("rotate")]
    [ProducesResponseType<RotateFamilyCodeResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<RotateFamilyCodeResponse>> Rotate()
    {
        var currentUser = await userManager.GetUserAsync(User);

        if (currentUser is null)
        {
            return Unauthorized();
        }

        var household = await dbContext.Households.SingleAsync(
            candidate => candidate.Id == currentUser.HouseholdId);
        var familyCode = await FamilyCodeService.GenerateUniqueAsync(dbContext);
        var now = timeProvider.GetUtcNow().UtcDateTime;

        household.FamilyCodeHash = familyCode.Hash;
        household.FamilyCodeLastFour = familyCode.LastFour;
        household.FamilyCodeUpdatedAt = now;
        await dbContext.SaveChangesAsync();

        // Rotation is the only later opportunity to reveal cleartext. The previous
        // hash is overwritten, which immediately invalidates the old family code.
        return Ok(new RotateFamilyCodeResponse(familyCode.Code, now));
    }
}
