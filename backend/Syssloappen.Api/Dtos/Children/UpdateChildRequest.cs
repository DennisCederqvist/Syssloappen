using System.ComponentModel.DataAnnotations;

namespace Syssloappen.Api.Dtos.Children;

public sealed class UpdateChildRequest
{
    [Required]
    [StringLength(100)]
    public string Name { get; init; } = string.Empty;
}
