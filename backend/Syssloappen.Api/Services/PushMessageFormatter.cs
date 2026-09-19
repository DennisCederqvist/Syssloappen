namespace Syssloappen.Api.Services;

/// <summary>
/// Unlike in-app SignalR events (type + data only, rendered via the frontend's own i18n),
/// a push notification's OS-level text must already be literal — the service worker that
/// shows it has no access to Transloco. English throughout, matching the transactional
/// emails, since the language toggle is client-side only and never reaches the backend.
/// </summary>
public static class PushMessageFormatter
{
    public static (string Title, string Body) Format(NotificationEvent evt) => evt.Type switch
    {
        NotificationEventType.ChoreAssigned => FormatChoreAssigned((ChoreAssignedData)evt.Data),
        NotificationEventType.ChoreApproved => FormatChoreApproved((ChoreApprovedData)evt.Data),
        NotificationEventType.ChoreNeedsRedo => FormatChoreNeedsRedo((ChoreNeedsRedoData)evt.Data),
        NotificationEventType.ChoreSubmittedForReview =>
            FormatChoreSubmittedForReview((ChoreSubmittedForReviewData)evt.Data),
        NotificationEventType.RewardRequested => FormatRewardRequested((RewardRequestedData)evt.Data),
        NotificationEventType.RewardApproved => FormatRewardApproved((RewardApprovedData)evt.Data),
        _ => ("Sysslo", "You have a new update."),
    };

    public static bool IsSilent(NotificationEventType type) =>
        type is NotificationEventType.ChoresChanged or NotificationEventType.RewardsChanged;

    // Where clicking the notification should take the person — child-facing events open
    // the child app, adult-facing events open the adult home page.
    public static string DestinationPath(NotificationEventType type) => type switch
    {
        NotificationEventType.ChoreAssigned => "/barn",
        NotificationEventType.ChoreApproved => "/barn",
        NotificationEventType.ChoreNeedsRedo => "/barn",
        NotificationEventType.RewardApproved => "/barn/beloningar",
        NotificationEventType.ChoreSubmittedForReview => "/vuxen",
        NotificationEventType.RewardRequested => "/vuxen",
        _ => "/",
    };

    private static (string, string) FormatChoreAssigned(ChoreAssignedData data) =>
        ("New chore!", $"{data.ChoreTitle} — {data.Points} points");

    private static (string, string) FormatChoreApproved(ChoreApprovedData data) =>
        ("Chore approved!", $"{data.ChoreTitle} was approved — you earned {data.Points} points!");

    private static (string, string) FormatChoreNeedsRedo(ChoreNeedsRedoData data) =>
        ("Chore needs redo", $"{data.ChoreTitle} needs to be done again");

    private static (string, string) FormatChoreSubmittedForReview(ChoreSubmittedForReviewData data) =>
        ("Ready for review", $"{data.ChildName} finished {data.ChoreTitle}");

    private static (string, string) FormatRewardRequested(RewardRequestedData data) =>
        ("Reward requested", $"{data.ChildName} requested {data.RewardName}");

    private static (string, string) FormatRewardApproved(RewardApprovedData data) =>
        ("Reward approved!", $"Your reward {data.RewardName} was approved — go get it!");
}
