using System.ComponentModel.DataAnnotations;

namespace Syssloappen.Api.Dtos.Auth;

public sealed class RegisterInvitedAdultRequest
{
    [Required]
    [StringLength(9, MinimumLength = 9)]
    public string InvitationCode { get; init; } = string.Empty;

    [Required]
    [EmailAddress]
    public string Email { get; init; } = string.Empty;

    [Required]
    [StringLength(100, MinimumLength = 8)]
    public string Password { get; init; } = string.Empty;
}