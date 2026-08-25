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

    public int Points { get; set; } = 5;

    public ChoreAssignmentStatus Status { get; set; } = ChoreAssignmentStatus.Assigned;

    public DateTime? SubmittedAt { get; set; }

    public string? ReviewedByUserId { get; set; }

    public ApplicationUser? ReviewedByUser { get; set; }

    public DateTime? ReviewedAt { get; set; }

    public string? ReviewComment { get; set; }
}
