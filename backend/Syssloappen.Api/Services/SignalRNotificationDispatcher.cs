using Microsoft.AspNetCore.SignalR;
using Syssloappen.Api.Hubs;

namespace Syssloappen.Api.Services;

public sealed class SignalRNotificationDispatcher(IHubContext<NotificationsHub> hubContext) : INotificationDispatcher
{
    private const string ClientMethodName = "notification";

    public Task NotifyChildAsync(int childProfileId, NotificationEvent evt, CancellationToken cancellationToken = default) =>
        hubContext.Clients
            .Group(NotificationGroups.Child(childProfileId))
            .SendAsync(ClientMethodName, evt, cancellationToken);

    public Task NotifyHouseholdAdultsAsync(int householdId, NotificationEvent evt, CancellationToken cancellationToken = default) =>
        hubContext.Clients
            .Group(NotificationGroups.HouseholdAdults(householdId))
            .SendAsync(ClientMethodName, evt, cancellationToken);
}
