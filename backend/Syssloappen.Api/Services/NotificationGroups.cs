namespace Syssloappen.Api.Services;

/// <summary>Shared SignalR group-name conventions so the Hub (joining) and the dispatcher
/// (sending) never drift apart.</summary>
public static class NotificationGroups
{
    public static string HouseholdAdults(int householdId) => $"household-adults:{householdId}";

    public static string Child(int childProfileId) => $"child:{childProfileId}";
}
