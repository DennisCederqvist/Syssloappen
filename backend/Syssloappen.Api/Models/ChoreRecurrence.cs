using Syssloappen.Api.Authentication;

namespace Syssloappen.Api.Models;

/// <summary>A repeat schedule for one Chore assigned to one Child. Scoped to that pair, not the
/// Chore template, since the same chore can recur for one child and not another.</summary>
public sealed class ChoreRecurrence
{
    public int Id { get; set; }

    public int HouseholdId { get; set; }

    public Household Household { get; set; } = null!;

    public int ChoreId { get; set; }

    public Chore Chore { get; set; } = null!;

    public int ChildId { get; set; }

    public ChildProfile Child { get; set; } = null!;

    public string CreatedByUserId { get; set; } = string.Empty;

    public ApplicationUser CreatedByUser { get; set; } = null!;

    public ChoreRecurrenceFrequency Frequency { get; set; }

    // Bit flags Mon=1, Tue=2, Wed=4, Thu=8, Fri=16, Sat=32, Sun=64.
    // Weekly: exactly one bit set. Custom: one or more. Null for Daily/Monthly.
    public int? DaysOfWeekMask { get; set; }

    // 1-31, Monthly only; clamped to the month's actual last day when it's shorter.
    public int? DayOfMonth { get; set; }

    // Generation never creates an occurrence dated before this.
    public DateOnly StartDate { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; }
}
