using Syssloappen.Api.Authentication;

namespace Syssloappen.Api.Models;

// One row per browser/device a user has enabled notifications on. Adults and Children are
// both ApplicationUser rows, so one table covers both without a discriminator.
public sealed class PushSubscription
{
    public int Id { get; set; }

    public string UserId { get; set; } = string.Empty;

    public ApplicationUser User { get; set; } = null!;

    public string Endpoint { get; set; } = string.Empty;

    public string P256dh { get; set; } = string.Empty;

    public string Auth { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }
}
