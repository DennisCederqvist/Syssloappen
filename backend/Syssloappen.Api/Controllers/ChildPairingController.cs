using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Syssloappen.Api.Authentication;
using Syssloappen.Api.Data;
using Syssloappen.Api.Dtos.Auth;
using Syssloappen.Api.Models;

namespace Syssloappen.Api.Controllers;

[ApiController]
[Route("api/auth/child")]
public sealed class ChildPairingController(
    AppDbContext dbContext,
    SignInManager<ApplicationUser> signInManager,
    TimeProvider timeProvider)
    : ControllerBase
{
    [AllowAnonymous]
    [EnableRateLimiting("child-pairing")]
    [HttpPost("pair")]
    [ProducesResponseType<PairChildDeviceResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<PairChildDeviceResponse>> Pair(PairChildDeviceRequest request)
    {
        var codeHash = ChildPairingCodeService.Hash(request.Code);
        var now = timeProvider.GetUtcNow().UtcDateTime;

        await using var transaction = await dbContext.Database.BeginTransactionAsync();
        var pairingCode = await dbContext.ChildPairingCodes
            .Include(code => code.ChildProfile)
            .ThenInclude(child => child.User)
            .SingleOrDefaultAsync(code =>
                code.CodeHash == codeHash
                && code.UsedAt == null
                && code.ExpiresAt > now);

        var child = pairingCode?.ChildProfile;
        var childUser = child?.User;
        var hasChildRole = childUser is not null
            && await signInManager.UserManager.IsInRoleAsync(childUser, RoleNames.Child);

        if (pairingCode is null
            || child is null
            || childUser is null
            || string.IsNullOrWhiteSpace(childUser.ChildUserName)
            || !hasChildRole
            || !child.IsActive
            || child.HouseholdId != pairingCode.HouseholdId
            || childUser.HouseholdId != pairingCode.HouseholdId)
        {
            await transaction.RollbackAsync();
            return InvalidPairingCode();
        }

        var issuedSession = ChildDeviceSessionService.Create(child, childUser, now);

        try
        {
            pairingCode.UsedAt = now;
            dbContext.ChildDeviceSessions.Add(issuedSession.Session);
            await dbContext.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync();
            return InvalidPairingCode();
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
            child.HouseholdId));
    }

    private UnauthorizedObjectResult InvalidPairingCode() => Unauthorized(
        new ProblemDetails
        {
            Title = "Invalid pairing code",
            Detail = "The pairing code is invalid or has expired.",
            Status = StatusCodes.Status401Unauthorized
        });
}
