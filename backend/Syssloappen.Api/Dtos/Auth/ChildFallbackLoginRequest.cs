using System.ComponentModel.DataAnnotations;

namespace Syssloappen.Api.Dtos.Auth;

public sealed class ChildFallbackLoginRequest
{
    [Required]
    [StringLength(32)]
    public string FamilyCode { get; init; } = string.Empty;

    [Required]
    [StringLength(50)]
    public string UserName { get; init; } = string.Empty;

    [Required]
    [StringLength(200)]
    public string Password { get; init; } = string.Empty;
}
