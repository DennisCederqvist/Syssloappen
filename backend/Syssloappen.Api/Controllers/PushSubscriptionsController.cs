using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Syssloappen.Api.Authentication;
using Syssloappen.Api.Data;
using Syssloappen.Api.Dtos.PushSubscriptions;
using Syssloappen.Api.Models;
using Syssloappen.Api.Services;

namespace Syssloappen.Api.Controllers;

[ApiController]
[Route("api/push-subscriptions")]
public sealed class PushSubscriptionsController(
    AppDbContext dbContext,
    UserManager<ApplicationUser> userManager,
    TimeProvider timeProvider,
    IOptions<WebPushOptions> webPushOptions) : ControllerBase
{
    // The VAPID public key is not secret — the browser needs it to call
    // PushManager.subscribe, before the caller is necessarily authenticated.
    [AllowAnonymous]
    [HttpGet("public-key")]
    [ProducesResponseType<PushPublicKeyResponse>(StatusCodes.Status200OK)]
    public ActionResult<PushPublicKeyResponse> GetPublicKey() =>
        Ok(new PushPublicKeyResponse(webPushOptions.Value.PublicKey));

    [Authorize]
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Subscribe(SubscribeToPushRequest request)
    {
        var currentUser = await userManager.GetUserAsync(User);

        if (currentUser is null)
        {
            return Unauthorized();
        }

        var existing = await dbContext.PushSubscriptions
            .SingleOrDefaultAsync(subscription => subscription.Endpoint == request.Endpoint);

        if (existing is not null)
        {
            // The same browser endpoint re-subscribing (e.g. after clearing site data)
            // always belongs to whoever is authenticated now, not whoever it was before.
            existing.UserId = currentUser.Id;
            existing.P256dh = request.P256dh;
            existing.Auth = request.Auth;
        }
        else
        {
            dbContext.PushSubscriptions.Add(new PushSubscription
            {
                UserId = currentUser.Id,
                Endpoint = request.Endpoint,
                P256dh = request.P256dh,
                Auth = request.Auth,
                CreatedAt = timeProvider.GetUtcNow().UtcDateTime
            });
        }

        await dbContext.SaveChangesAsync();
        return NoContent();
    }

    [Authorize]
    [HttpDelete]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Unsubscribe(UnsubscribeFromPushRequest request)
    {
        var currentUser = await userManager.GetUserAsync(User);

        if (currentUser is null)
        {
            return Unauthorized();
        }

        await dbContext.PushSubscriptions
            .Where(subscription =>
                subscription.Endpoint == request.Endpoint && subscription.UserId == currentUser.Id)
            .ExecuteDeleteAsync();

        return NoContent();
    }
}
