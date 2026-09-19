namespace Syssloappen.Api.Services;

public enum NotificationEventType
{
    ChoreAssigned,
    ChoreApproved,
    ChoreNeedsRedo,
    ChoreSubmittedForReview,
    RewardRequested,
    RewardApproved,
    // Silent refresh hints: no push notification, no toast — a child's open pages just reload.
    ChoresChanged,
    RewardsChanged,
}

// Carries only IDs/names, never pre-built prose — the frontend renders the
// user-facing text from its own i18n strings keyed by Type, the same lesson
// learned from hardcoding Swedish into outbound emails.
public sealed record NotificationEvent(NotificationEventType Type, object Data);

public sealed record ChoreAssignedData(int AssignmentId, string ChoreTitle, int Points);

public sealed record ChoreApprovedData(int AssignmentId, string ChoreTitle, int Points);

public sealed record ChoreNeedsRedoData(int AssignmentId, string ChoreTitle);

public sealed record ChoreSubmittedForReviewData(int AssignmentId, string ChoreTitle, string ChildName);

public sealed record RewardRequestedData(int RedemptionId, string RewardName, string ChildName);

public sealed record RewardApprovedData(int RedemptionId, string RewardName);

public sealed record ContentChangedData();
