using System.ComponentModel.DataAnnotations;

namespace Syssloappen.Api.Dtos.Households;

public sealed class ChangeAdultPasswordRequest
{
    [Required]
    public string CurrentPassword { get; init; } = string.Empty;

    [Required]
    [StringLength(100, MinimumLength = 8)]
    public string NewPassword { get; init; } = string.Empty;
}
