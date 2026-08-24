using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Syssloappen.Api.Data;

namespace Syssloappen.Api.Authentication;

public sealed class ChildSessionCookieEvents(
    AppDbContext dbContext,
    UserManager<ApplicationUser> userManager,
    TimeProvider timeProvider) : CookieAuthenticationEvents
{
    public override async Task ValidatePrincipal(CookieValidatePrincipalContext context)
    {
        var principal = context.Principal;

        if (principal?.IsInRole(RoleNames.Child) != true)
        {
            return;
        }

        var sessionIdValue = principal.FindFirstValue(ChildDeviceSessionService.SessionIdClaim);
        var sessionSecret = principal.FindFirstValue(ChildDeviceSessionService.SessionSecretClaim);
        var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!Guid.TryParse(sessionIdValue, out var sessionId)
            || string.IsNullOrWhiteSpace(sessionSecret)
            || string.IsNullOrWhiteSpace(userId))
        {
            await RejectAsync(context);
            return;
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var secretHash = ChildDeviceSessionService.HashSecret(sessionSecret);
        var session = await dbContext.ChildDeviceSessions
            .Include(deviceSession => deviceSession.ChildProfile)
            .Include(deviceSession => deviceSession.User)
            .SingleOrDefaultAsync(deviceSession =>
                deviceSession.Id == sessionId
                && deviceSession.UserId == userId
                && deviceSession.SecretHash == secretHash);

        // Every Child request rechecks the complete database binding. A protected
        // cookie alone is not enough after revocation, expiry or deactivation.
        if (session is null
            || session.RevokedAt is not null
            || session.ExpiresAt <= now
            || session.AbsoluteExpiresAt <= now
            || !session.ChildProfile.IsActive
            || session.ChildProfileId != session.ChildProfile.Id
            || session.HouseholdId != session.ChildProfile.HouseholdId
            || session.UserId != session.ChildProfile.UserId
            || session.UserId != session.User.Id
            || session.HouseholdId != session.User.HouseholdId
            || !await userManager.IsInRoleAsync(session.User, RoleNames.Child))
        {
            await RejectAsync(context);
            return;
        }

        var shouldRenew = session.ExpiresAt - now <= ChildDeviceSessionService.RenewalThreshold;
        var shouldUpdateActivity = now - session.LastSeenAt >= ChildDeviceSessionService.ActivityUpdateInterval;

        if (!shouldRenew && !shouldUpdateActivity)
        {
            return;
        }

        session.LastSeenAt = now;

        if (shouldRenew)
        {
            session.ExpiresAt = Min(
                now.Add(ChildDeviceSessionService.RenewableLifetime),
                session.AbsoluteExpiresAt);
            context.Properties.IssuedUtc = timeProvider.GetUtcNow();
            context.Properties.ExpiresUtc = new DateTimeOffset(session.ExpiresAt, TimeSpan.Zero);
            context.ShouldRenew = true;
        }

        await dbContext.SaveChangesAsync();
    }

    public override Task RedirectToLogin(RedirectContext<CookieAuthenticationOptions> context)
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        return Task.CompletedTask;
    }

    public override Task RedirectToAccessDenied(RedirectContext<CookieAuthenticationOptions> context)
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        return Task.CompletedTask;
    }

    private static async Task RejectAsync(CookieValidatePrincipalContext context)
    {
        context.RejectPrincipal();
        await context.HttpContext.SignOutAsync(IdentityConstants.ApplicationScheme);
    }

    private static DateTime Min(DateTime first, DateTime second) => first <= second ? first : second;
}
