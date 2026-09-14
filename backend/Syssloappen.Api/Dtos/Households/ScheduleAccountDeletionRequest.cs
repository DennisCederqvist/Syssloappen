using System.ComponentModel.DataAnnotations;

namespace Syssloappen.Api.Dtos.Households;

public sealed class ScheduleAccountDeletionRequest
{
    [Required]
    public string Password { get; init; } = string.Empty;
}
