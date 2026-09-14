namespace Syssloappen.Api.Services;

/// <summary>
/// Permanently deletes every Household whose 30-day GDPR deletion grace period
/// (<c>Household.DeletionScheduledAt</c>) has passed, along with every row and stored
/// file that belongs to it. Irreversible — see <see cref="HouseholdPurgeService"/>.
/// </summary>
public interface IHouseholdPurgeService
{
    Task PurgeDueHouseholdsAsync(CancellationToken cancellationToken = default);
}
