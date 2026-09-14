namespace Syssloappen.Api.Services;

/// <summary>
/// Pushes a <see cref="NotificationEvent"/> to whichever live connections belong to the
/// target child or household's adults. Best-effort — callers should not fail the primary
/// request if this fails, since the underlying data change is already committed by the
/// time this runs.
/// </summary>
public interface INotificationDispatcher
{
    Task NotifyChildAsync(int childProfileId, NotificationEvent evt, CancellationToken cancellationToken = default);

    Task NotifyHouseholdAdultsAsync(int householdId, NotificationEvent evt, CancellationToken cancellationToken = default);
}
