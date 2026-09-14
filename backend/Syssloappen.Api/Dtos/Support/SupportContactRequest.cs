using System.ComponentModel.DataAnnotations;

namespace Syssloappen.Api.Dtos.Support;

public sealed class SupportContactRequest
{
    [Required]
    [StringLength(2000, MinimumLength = 1)]
    public string Message { get; init; } = string.Empty;
}
