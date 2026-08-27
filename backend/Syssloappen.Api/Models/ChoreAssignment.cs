using Syssloappen.Api.Authentication;

namespace Syssloappen.Api.Models;

public sealed class ChoreAssignment
{
    public int Id { get; set; }

    public int HouseholdId { get; set; }

    public Household Household { get; set; } = null!;

    public int ChoreId { get; set; }

    public Chore Chore { get; set; } = null!;

    public int ChildId { get; set; }

    public ChildProfile Child { get; set; } = null!;

    public string AssignedByUserId { get; set; } = string.Empty;

    public ApplicationUser AssignedByUser { get; set; } = null!;

    public DateTime AssignedAt { get; set; }

    // The calendar day the child should see and complete this one-off chore.
    // AssignedAt remains an audit timestamp and must not be used as scheduling data.
    public DateOnly DueDate { get; set; }

    public int Points { get; set; } = 5;

    public ChoreAssignmentStatus Status { get; set; } = ChoreAssignmentStatus.Assigned;

    public DateTime? SubmittedAt { get; set; }

    public string? ReviewedByUserId { get; set; }

    public ApplicationUser? ReviewedByUser { get; set; }

    public DateTime? ReviewedAt { get; set; }

    public string? ReviewComment { get; set; }

    public string? CancelledByUserId { get; set; }

    public ApplicationUser? CancelledByUser { get; set; }

    public DateTime? CancelledAt { get; set; }

    // A reversible Household-wide Adult view preference for completed history.
    // It must never remove audit data, points or the child-visible assignment.
    public DateTime? AdultArchivedAt { get; set; }
}
