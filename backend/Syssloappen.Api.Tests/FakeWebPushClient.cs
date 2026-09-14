using Syssloappen.Api.Services;

namespace Syssloappen.Api.Tests;

/// <summary>In-memory stand-in for <see cref="IWebPushClient"/> so tests never make a real
/// HTTP call to a push service. Records what was sent; endpoints listed in
/// <see cref="GoneEndpoints"/> simulate a push service reporting the subscription is gone.</summary>
public sealed class FakeWebPushClient : IWebPushClient
{
    public List<(string Endpoint, string PayloadJson)> SentNotifications { get; } = [];
    public HashSet<string> GoneEndpoints { get; } = [];

    public Task SendNotificationAsync(
        string endpoint,
        string p256dh,
        string auth,
        string payloadJson,
        CancellationToken cancellationToken = default)
    {
        if (GoneEndpoints.Contains(endpoint))
        {
            throw new WebPushSubscriptionGoneException();
        }

        SentNotifications.Add((endpoint, payloadJson));
        return Task.CompletedTask;
    }
}
