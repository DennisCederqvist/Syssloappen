using System.ComponentModel.DataAnnotations;

namespace Syssloappen.Api.Dtos.Households;

public sealed class UpdateAdultProfileRequest
{
    [StringLength(100)]
    public string? FirstName { get; init; }

    [StringLength(100)]
    public string? LastName { get; init; }

    [StringLength(50)]
    public string? Nickname { get; init; }
}
