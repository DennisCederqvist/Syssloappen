using Syssloappen.Api.Services;

namespace Syssloappen.Api.Tests;

/// <summary>In-memory stand-in for <see cref="INotificationDispatcher"/> so tests never need
/// a real SignalR connection. Records what was dispatched so tests can assert on it.</summary>
public sealed class FakeNotificationDispatcher : INotificationDispatcher
{
    public List<(int ChildProfileId, NotificationEvent Event)> ChildNotifications { get; } = [];
    public List<(int HouseholdId, NotificationEvent Event)> HouseholdAdultNotifications { get; } = [];

    public Task NotifyChildAsync(int childProfileId, NotificationEvent evt, CancellationToken cancellationToken = default)
    {
        ChildNotifications.Add((childProfileId, evt));
        return Task.CompletedTask;
    }

    public List<(int HouseholdId, NotificationEvent Event)> HouseholdChildNotifications { get; } = [];

    public Task NotifyHouseholdChildrenAsync(int householdId, NotificationEvent evt, CancellationToken cancellationToken = default)
    {
        HouseholdChildNotifications.Add((householdId, evt));
        return Task.CompletedTask;
    }

    public Task NotifyHouseholdAdultsAsync(int householdId, NotificationEvent evt, CancellationToken cancellationToken = default)
    {
        HouseholdAdultNotifications.Add((householdId, evt));
        return Task.CompletedTask;
    }
}
