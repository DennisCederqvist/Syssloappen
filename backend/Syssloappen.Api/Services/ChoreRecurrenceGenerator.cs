using Microsoft.EntityFrameworkCore;
using Syssloappen.Api.Data;
using Syssloappen.Api.Models;

namespace Syssloappen.Api.Services;

/// <summary>
/// Lazily creates today's ChoreAssignment for every due ChoreRecurrence in a household. Called
/// at request time from the same places that already roll unfinished assignments forward
/// (ChoreAssignmentsController.GetAll, ChildChoreAssignmentsController.GetMine) — there is no
/// background job/scheduler anywhere in this codebase, and this matches that existing house
/// style rather than introducing new infrastructure for it.
/// </summary>
public sealed class ChoreRecurrenceGenerator(AppDbContext dbContext, TimeProvider timeProvider)
{
    public async Task GenerateDueAssignmentsAsync(int householdId, CancellationToken cancellationToken = default)
    {
        // Local time, matching every other "today" comparison in the chore-scheduling code.
        var today = DateOnly.FromDateTime(timeProvider.GetLocalNow().DateTime);

        var dueRecurrences = await dbContext.ChoreRecurrences
            .Where(recurrence =>
                recurrence.HouseholdId == householdId
                && recurrence.IsActive
                && recurrence.Chore.IsActive
                && recurrence.Child.IsActive
                && recurrence.StartDate <= today)
            .Select(recurrence => new
            {
                recurrence.Id,
                recurrence.ChoreId,
                recurrence.ChildId,
                recurrence.CreatedByUserId,
                recurrence.Frequency,
                recurrence.DaysOfWeekMask,
                recurrence.DayOfMonth,
                ChorePoints = recurrence.Chore.Points
            })
            .ToListAsync(cancellationToken);

        var anyGenerated = false;
        foreach (var recurrence in dueRecurrences)
        {
            if (!MatchesSchedule(recurrence.Frequency, recurrence.DaysOfWeekMask, recurrence.DayOfMonth, today))
            {
                continue;
            }

            var alreadyGenerated = await dbContext.ChoreAssignments.AnyAsync(
                assignment =>
                    assignment.GeneratedFromRecurrenceId == recurrence.Id
                    && assignment.DueDate == today,
                cancellationToken);
            if (alreadyGenerated)
            {
                continue;
            }

            dbContext.ChoreAssignments.Add(new ChoreAssignment
            {
                HouseholdId = householdId,
                ChoreId = recurrence.ChoreId,
                ChildId = recurrence.ChildId,
                AssignedByUserId = recurrence.CreatedByUserId,
                AssignedAt = timeProvider.GetUtcNow().UtcDateTime,
                DueDate = today,
                Points = recurrence.ChorePoints,
                GeneratedFromRecurrenceId = recurrence.Id
            });
            anyGenerated = true;
        }

        if (anyGenerated)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    private static bool MatchesSchedule(
        ChoreRecurrenceFrequency frequency, int? daysOfWeekMask, int? dayOfMonth, DateOnly today) =>
        frequency switch
        {
            ChoreRecurrenceFrequency.Daily => true,
            ChoreRecurrenceFrequency.Weekly or ChoreRecurrenceFrequency.Custom =>
                daysOfWeekMask is not null && (daysOfWeekMask.Value & WeekdayBit(today.DayOfWeek)) != 0,
            ChoreRecurrenceFrequency.Monthly =>
                dayOfMonth is not null
                    && today.Day == Math.Min(dayOfMonth.Value, DateTime.DaysInMonth(today.Year, today.Month)),
            _ => false
        };

    private static int WeekdayBit(DayOfWeek dayOfWeek) => dayOfWeek switch
    {
        DayOfWeek.Monday => 1,
        DayOfWeek.Tuesday => 2,
        DayOfWeek.Wednesday => 4,
        DayOfWeek.Thursday => 8,
        DayOfWeek.Friday => 16,
        DayOfWeek.Saturday => 32,
        DayOfWeek.Sunday => 64,
        _ => 0
    };
}
