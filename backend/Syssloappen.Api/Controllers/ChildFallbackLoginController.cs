using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Syssloappen.Api.Authentication;
using Syssloappen.Api.Data;
using Syssloappen.Api.Dtos.Auth;

namespace Syssloappen.Api.Controllers;

[ApiController]
[Route("api/auth/child")]
public sealed class ChildFallbackLoginController(
    AppDbContext dbContext,
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager,
    TimeProvider timeProvider) : ControllerBase
{
    [AllowAnonymous]
    [EnableRateLimiting("child-fallback-login")]
    [HttpPost("login")]
    [ProducesResponseType<PairChildDeviceResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<PairChildDeviceResponse>> Login(ChildFallbackLoginRequest request)
    {
        var familyCodeHash = FamilyCodeService.Hash(request.FamilyCode);
        var normalizedChildUserName = userManager.NormalizeName(request.UserName.Trim());

        await using var transaction = await dbContext.Database.BeginTransactionAsync();

        // The request contains no database IDs. The hash selects the Household first,
        // and the child-friendly name is then searched only inside that Household.
        var household = await dbContext.Households
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate =>
                candidate.FamilyCodeHash == familyCodeHash
                && candidate.FamilyCodeLastFour != null);

        if (household is null)
        {
            await transaction.RollbackAsync();
            return InvalidCredentials();
        }

        var childUser = await dbContext.Users.SingleOrDefaultAsync(user =>
            user.HouseholdId == household.Id
            && user.NormalizedChildUserName == normalizedChildUserName);

        if (childUser is null)
        {
            await transaction.RollbackAsync();
            return InvalidCredentials();
        }

        var child = await dbContext.ChildProfiles.SingleOrDefaultAsync(profile =>
            profile.UserId == childUser.Id
            && profile.HouseholdId == household.Id);

        if (child is null)
        {
            await transaction.RollbackAsync();
            return InvalidCredentials();
        }

        var hasChildRole = await userManager.IsInRoleAsync(childUser, RoleNames.Child);
        var passwordResult = await signInManager.CheckPasswordSignInAsync(
            childUser,
            request.Password,
            lockoutOnFailure: false);

        if (!child.IsActive
            || string.IsNullOrWhiteSpace(childUser.ChildUserName)
            || !hasChildRole
            || !passwordResult.Succeeded
            || child.UserId != childUser.Id
            || child.HouseholdId != household.Id
            || childUser.HouseholdId != household.Id)
        {
            await transaction.RollbackAsync();
            return InvalidCredentials();
        }

        var issuedSession = ChildDeviceSessionService.Create(
            child,
            childUser,
            timeProvider.GetUtcNow().UtcDateTime);

        try
        {
            dbContext.ChildDeviceSessions.Add(issuedSession.Session);
            await dbContext.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync();
            return InvalidCredentials();
        }

        await ChildDeviceSessionService.SignInAsync(
            signInManager,
            childUser,
            issuedSession,
            timeProvider);

        return Ok(new PairChildDeviceResponse(
            child.Id,
            child.Name,
            childUser.ChildUserName!,
            RoleNames.Child,
            household.Id));
    }

    private UnauthorizedObjectResult InvalidCredentials() => Unauthorized(
        new ProblemDetails
        {
            Title = "Invalid child credentials",
            Detail = "The family code, username or password is incorrect.",
            Status = StatusCodes.Status401Unauthorized
        });
}
