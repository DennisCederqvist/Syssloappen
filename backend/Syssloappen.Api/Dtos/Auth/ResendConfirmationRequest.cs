using System.ComponentModel.DataAnnotations;

namespace Syssloappen.Api.Dtos.Auth;

public sealed class ResendConfirmationRequest
{
    [Required]
    [EmailAddress]
    public string Email { get; init; } = string.Empty;
}
