using Syssloappen.Api.Authentication;

namespace Syssloappen.Api.Models;

public sealed class ChoreCompletion
{
    public int Id { get; set; }

    public int HouseholdId { get; set; }

    public Household Household { get; set; } = null!;

    public int AssignmentId { get; set; }

    public ChoreAssignment Assignment { get; set; } = null!;

    public int ChildId { get; set; }

    public ChildProfile Child { get; set; } = null!;

    public int ChoreId { get; set; }

    public Chore Chore { get; set; } = null!;

    public string ApprovedByUserId { get; set; } = string.Empty;

    public ApplicationUser ApprovedByUser { get; set; } = null!;

    public DateTime ApprovedAt { get; set; }

    public int PointsAwarded { get; set; }
}
