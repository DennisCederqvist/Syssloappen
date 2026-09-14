using System.ComponentModel.DataAnnotations;

namespace Syssloappen.Api.Dtos.PushSubscriptions;

public sealed class SubscribeToPushRequest
{
    [Required]
    [StringLength(1000)]
    public string Endpoint { get; init; } = string.Empty;

    [Required]
    [StringLength(255)]
    public string P256dh { get; init; } = string.Empty;

    [Required]
    [StringLength(255)]
    public string Auth { get; init; } = string.Empty;
}
