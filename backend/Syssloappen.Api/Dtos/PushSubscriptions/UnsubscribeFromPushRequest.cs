using System.ComponentModel.DataAnnotations;

namespace Syssloappen.Api.Dtos.PushSubscriptions;

public sealed class UnsubscribeFromPushRequest
{
    [Required]
    [StringLength(1000)]
    public string Endpoint { get; init; } = string.Empty;
}
