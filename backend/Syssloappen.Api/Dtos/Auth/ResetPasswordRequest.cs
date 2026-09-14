using System.ComponentModel.DataAnnotations;

namespace Syssloappen.Api.Dtos.Auth;

public sealed class ResetPasswordRequest
{
    [Required]
    public string UserId { get; init; } = string.Empty;

    [Required]
    public string Token { get; init; } = string.Empty;

    [Required]
    [StringLength(100, MinimumLength = 8)]
    public string NewPassword { get; init; } = string.Empty;
}
