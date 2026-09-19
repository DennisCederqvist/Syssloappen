using Microsoft.EntityFrameworkCore;
using Syssloappen.Api.Data;

namespace Syssloappen.Api.Services;

public sealed class HouseholdPurgeService(
    AppDbContext dbContext,
    IRewardImageStorage imageStorage,
    TimeProvider timeProvider,
    ILogger<HouseholdPurgeService> logger) : IHouseholdPurgeService
{
    public async Task PurgeDueHouseholdsAsync(CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;

        var dueHouseholdIds = await dbContext.Households
            .Where(household => household.DeletionScheduledAt != null && household.DeletionScheduledAt <= now)
            .Select(household => household.Id)
            .ToListAsync(cancellationToken);

        foreach (var householdId in dueHouseholdIds)
        {
            await PurgeHouseholdAsync(householdId, cancellationToken);
        }
    }

    private async Task PurgeHouseholdAsync(int householdId, CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        // Storage files are only removed once the DB transaction that proves they're no
        // longer referenced has committed — collect their URLs before the owning rows go.
        var imageUrls = new List<string>();
        imageUrls.AddRange(await dbContext.Rewards
            .Where(reward => reward.HouseholdId == householdId && reward.ImageUrl != null)
            .Select(reward => reward.ImageUrl!)
            .ToListAsync(cancellationToken));
        imageUrls.AddRange(await dbContext.Chores
            .Where(chore => chore.HouseholdId == householdId && chore.ImageUrl != null)
            .Select(chore => chore.ImageUrl!)
            .ToListAsync(cancellationToken));
        imageUrls.AddRange(await dbContext.ChildProfiles
            .Where(child => child.HouseholdId == householdId && child.PhotoUrl != null)
            .Select(child => child.PhotoUrl!)
            .ToListAsync(cancellationToken));

        // Every step below is a bulk ExecuteDelete/ExecuteUpdate rather than a tracked
        // Remove+SaveChanges — this avoids ever mixing change-tracked entities with bulk
        // operations against the same tables in one unit of work (which otherwise causes
        // spurious optimistic-concurrency failures on SQLite once a table is touched by
        // both). Deletion order mirrors every DeleteBehavior.Restrict foreign key
        // configured in AppDbContext.OnModelCreating: each step removes rows that would
        // otherwise still reference what the next step deletes. HouseholdId itself
        // cascades at the DB level, but the cross-references between these entities
        // (ChoreAssignment→Chore, ChoreCompletion→Assignment, etc.) do not.
        await dbContext.ChoreCompletions.Where(x => x.HouseholdId == householdId)
            .ExecuteDeleteAsync(cancellationToken);
        await dbContext.RewardRedemptions.Where(x => x.HouseholdId == householdId)
            .ExecuteDeleteAsync(cancellationToken);
        await dbContext.ChildPointReservations.Where(x => x.HouseholdId == householdId)
            .ExecuteDeleteAsync(cancellationToken);
        await dbContext.ChildPairingCodes.Where(x => x.HouseholdId == householdId)
            .ExecuteDeleteAsync(cancellationToken);
        await dbContext.ChildDeviceSessions.Where(x => x.HouseholdId == householdId)
            .ExecuteDeleteAsync(cancellationToken);
        await dbContext.ChoreAssignments.Where(x => x.HouseholdId == householdId)
            .ExecuteDeleteAsync(cancellationToken);
        await dbContext.ChoreRecurrences.Where(x => x.HouseholdId == householdId)
            .ExecuteDeleteAsync(cancellationToken);
        await dbContext.Chores.Where(x => x.HouseholdId == householdId)
            .ExecuteDeleteAsync(cancellationToken);
        await dbContext.Rewards.Where(x => x.HouseholdId == householdId)
            .ExecuteDeleteAsync(cancellationToken);
        await dbContext.ChildProfiles.Where(x => x.HouseholdId == householdId)
            .ExecuteDeleteAsync(cancellationToken);
        await dbContext.HouseholdInvitations.Where(x => x.HouseholdId == householdId)
            .ExecuteDeleteAsync(cancellationToken);

        // Household.OwnerUserId and ApplicationUser.HouseholdId reference each other, both
        // Restrict — breaking the cycle here (same trick RegisterAdult uses to create it)
        // is what makes deleting the Users below, then the Household itself, possible.
        await dbContext.Households.Where(x => x.Id == householdId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(h => h.OwnerUserId, (string?)null), cancellationToken);

        var userIds = await dbContext.Users
            .Where(user => user.HouseholdId == householdId)
            .Select(user => user.Id)
            .ToListAsync(cancellationToken);

        // Identity's own per-user tables (and PushSubscriptions, also FK-Restrict to
        // ApplicationUser) have no HouseholdId to filter by, so they're scoped through
        // the household's user IDs instead.
        await dbContext.UserRoles.Where(x => userIds.Contains(x.UserId)).ExecuteDeleteAsync(cancellationToken);
        await dbContext.UserClaims.Where(x => userIds.Contains(x.UserId)).ExecuteDeleteAsync(cancellationToken);
        await dbContext.UserLogins.Where(x => userIds.Contains(x.UserId)).ExecuteDeleteAsync(cancellationToken);
        await dbContext.UserTokens.Where(x => userIds.Contains(x.UserId)).ExecuteDeleteAsync(cancellationToken);
        await dbContext.PushSubscriptions.Where(x => userIds.Contains(x.UserId)).ExecuteDeleteAsync(cancellationToken);
        await dbContext.Users.Where(x => userIds.Contains(x.Id)).ExecuteDeleteAsync(cancellationToken);

        await dbContext.Households.Where(x => x.Id == householdId).ExecuteDeleteAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        logger.LogInformation(
            "Purged household {HouseholdId} after its deletion grace period expired.",
            householdId);

        foreach (var url in imageUrls)
        {
            try
            {
                await imageStorage.DeleteAsync(url, cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogWarning(
                    ex,
                    "Failed to delete a stored image while purging household {HouseholdId}.",
                    householdId);
            }
        }
    }
}
