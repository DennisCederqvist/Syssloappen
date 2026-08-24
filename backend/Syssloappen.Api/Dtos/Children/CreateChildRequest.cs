using System.ComponentModel.DataAnnotations;

namespace Syssloappen.Api.Dtos.Children;

public sealed class CreateChildRequest
{
    [Required]
    [StringLength(100)]
    public string Name { get; init; } = string.Empty;

    [Required]
    [StringLength(50)]
    public string UserName { get; init; } = string.Empty;

    [Required]
    [StringLength(100, MinimumLength = 8)]
    public string Password { get; init; } = string.Empty;
}
