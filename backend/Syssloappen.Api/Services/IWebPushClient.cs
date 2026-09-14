namespace Syssloappen.Api.Services;

/// <summary>Thin wrapper around the third-party WebPush client so callers and tests never
/// depend on its types directly.</summary>
public interface IWebPushClient
{
    /// <exception cref="WebPushSubscriptionGoneException">The push service reports this
    /// subscription no longer exists (expired or the user revoked permission) — the caller
    /// should delete the stored subscription.</exception>
    Task SendNotificationAsync(
        string endpoint,
        string p256dh,
        string auth,
        string payloadJson,
        CancellationToken cancellationToken = default);
}

public sealed class WebPushSubscriptionGoneException : Exception;
