using Microsoft.EntityFrameworkCore;
using Syssloappen.Api.Data;

namespace Syssloappen.Api.Services;

/// <summary>
/// A child's available points: what they have earned from approved chores minus what is currently
/// reserved by open reward requests. It is the same number the child sees in their own view, so an
/// adult looking at it sees exactly what the child sees.
/// </summary>
public static class ChildPointBalances
{
    /// <summary>Returns the available points for each requested child of <paramref name="householdId"/>.</summary>
    public static async Task<Dictionary<int, int>> AvailableAsync(
        AppDbContext dbContext,
        int householdId,
        IReadOnlyCollection<int> childIds,
        CancellationToken cancellationToken = default)
    {
        // Every condition is repeated here, matching the child's own points queries, so a child's
        // balance can never include another household's data.
        var earned = await dbContext.ChoreCompletions
            .AsNoTracking()
            .Where(completion =>
                childIds.Contains(completion.ChildId)
                && completion.HouseholdId == householdId
                && completion.Child.HouseholdId == householdId
                && completion.Assignment.HouseholdId == householdId
                && completion.Assignment.ChildId == completion.ChildId
                && completion.Chore.HouseholdId == householdId)
            .GroupBy(completion => completion.ChildId)
            .Select(group => new { ChildId = group.Key, Points = group.Sum(completion => completion.PointsAwarded) })
            .ToDictionaryAsync(row => row.ChildId, row => row.Points, cancellationToken);

        var reserved = await dbContext.ChildPointReservations
            .AsNoTracking()
            .Where(reservation => reservation.HouseholdId == householdId && childIds.Contains(reservation.ChildId))
            .ToDictionaryAsync(reservation => reservation.ChildId, reservation => reservation.ReservedPoints, cancellationToken);

        return childIds.ToDictionary(
            childId => childId,
            childId => earned.GetValueOrDefault(childId) - reserved.GetValueOrDefault(childId));
    }
}
