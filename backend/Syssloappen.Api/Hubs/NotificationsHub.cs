using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Syssloappen.Api.Authentication;
using Syssloappen.Api.Data;
using Syssloappen.Api.Services;

namespace Syssloappen.Api.Hubs;

// Authenticates via the same cookie scheme controllers use — no separate token
// handshake needed. On connect, the caller is placed into exactly the group(s) their
// role/household/child profile entitle them to; there is no client-chosen group.
[Authorize]
public sealed class NotificationsHub(
    AppDbContext dbContext,
    UserManager<ApplicationUser> userManager) : Hub
{
    public override async Task OnConnectedAsync()
    {
        var currentUser = Context.User is null ? null : await userManager.GetUserAsync(Context.User);

        if (currentUser is not null)
        {
            if (currentUser.DisconnectedAt is null && await userManager.IsInRoleAsync(currentUser, RoleNames.Adult))
            {
                await Groups.AddToGroupAsync(
                    Context.ConnectionId,
                    NotificationGroups.HouseholdAdults(currentUser.HouseholdId));
            }
            else if (await userManager.IsInRoleAsync(currentUser, RoleNames.Child))
            {
                var child = await dbContext.ChildProfiles
                    .AsNoTracking()
                    .SingleOrDefaultAsync(childProfile =>
                        childProfile.UserId == currentUser.Id
                        && childProfile.HouseholdId == currentUser.HouseholdId
                        && childProfile.IsActive);

                if (child is not null)
                {
                    await Groups.AddToGroupAsync(Context.ConnectionId, NotificationGroups.Child(child.Id));
                }
            }
        }

        await base.OnConnectedAsync();
    }
}
