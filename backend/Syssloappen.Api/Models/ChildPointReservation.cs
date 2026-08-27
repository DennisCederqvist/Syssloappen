namespace Syssloappen.Api.Models;

public sealed class ChildPointReservation
{
    public int Id { get; set; }
    public int HouseholdId { get; set; }
    public Household Household { get; set; } = null!;
    public int ChildId { get; set; }
    public ChildProfile Child { get; set; } = null!;
    public int ReservedPoints { get; set; }
    public int Version { get; set; }
}
