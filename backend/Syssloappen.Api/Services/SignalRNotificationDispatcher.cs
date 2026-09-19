using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Syssloappen.Api.Authentication;
using Syssloappen.Api.Data;
using Syssloappen.Api.Hubs;

namespace Syssloappen.Api.Services;

public sealed class SignalRNotificationDispatcher(
    IHubContext<NotificationsHub> hubContext,
    AppDbContext dbContext,
    IWebPushClient webPushClient,
    ILogger<SignalRNotificationDispatcher> logger) : INotificationDispatcher
{
    private const string ClientMethodName = "notification";

    public async Task NotifyChildAsync(int childProfileId, NotificationEvent evt, CancellationToken cancellationToken = default)
    {
        await hubContext.Clients
            .Group(NotificationGroups.Child(childProfileId))
            .SendAsync(ClientMethodName, evt, cancellationToken);

        var childUserId = await dbContext.ChildProfiles
            .AsNoTracking()
            .Where(child => child.Id == childProfileId)
            .Select(child => child.UserId)
            .SingleOrDefaultAsync(cancellationToken);

        if (childUserId is not null)
        {
            await PushToUserAsync(childUserId, evt, cancellationToken);
        }
    }

    public async Task NotifyHouseholdChildrenAsync(int householdId, NotificationEvent evt, CancellationToken cancellationToken = default)
    {
        await hubContext.Clients
            .Group(NotificationGroups.HouseholdChildren(householdId))
            .SendAsync(ClientMethodName, evt, cancellationToken);
    }

    public async Task NotifyHouseholdAdultsAsync(int householdId, NotificationEvent evt, CancellationToken cancellationToken = default)
    {
        await hubContext.Clients
            .Group(NotificationGroups.HouseholdAdults(householdId))
            .SendAsync(ClientMethodName, evt, cancellationToken);

        // Same "active Adult in this household" filter used throughout the app
        // (HouseholdAdultsController.List, NotificationsHub).
        var adultUserIds = await (
            from user in dbContext.Users.AsNoTracking()
            join userRole in dbContext.UserRoles.AsNoTracking() on user.Id equals userRole.UserId
            join role in dbContext.Roles.AsNoTracking() on userRole.RoleId equals role.Id
            where user.HouseholdId == householdId
                && role.Name == RoleNames.Adult
                && user.DisconnectedAt == null
            select user.Id)
            .ToListAsync(cancellationToken);

        foreach (var userId in adultUserIds)
        {
            await PushToUserAsync(userId, evt, cancellationToken);
        }
    }

    private async Task PushToUserAsync(string userId, NotificationEvent evt, CancellationToken cancellationToken)
    {
        if (PushMessageFormatter.IsSilent(evt.Type)) return;

        var subscriptions = await dbContext.PushSubscriptions
            .Where(subscription => subscription.UserId == userId)
            .ToListAsync(cancellationToken);

        if (subscriptions.Count == 0)
        {
            return;
        }

        var (title, body) = PushMessageFormatter.Format(evt);
        var url = PushMessageFormatter.DestinationPath(evt.Type);
        var payload = JsonSerializer.Serialize(new
        {
            notification = new
            {
                title,
                body,
                data = new
                {
                    payload = evt.Data,
                    // Angular's service worker reads this to decide what clicking the
                    // notification does — focus an already-open tab if there is one,
                    // otherwise open a new one at this path.
                    onActionClick = new
                    {
                        @default = new { operation = "focusLastFocusedOrOpen", url }
                    }
                }
            }
        });

        foreach (var subscription in subscriptions)
        {
            try
            {
                await webPushClient.SendNotificationAsync(
                    subscription.Endpoint, subscription.P256dh, subscription.Auth, payload, cancellationToken);
            }
            catch (WebPushSubscriptionGoneException)
            {
                dbContext.PushSubscriptions.Remove(subscription);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to send a push notification to subscription {SubscriptionId}.", subscription.Id);
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
