using System.Net;
using Microsoft.Extensions.Options;
using WebPush;

namespace Syssloappen.Api.Services;

public sealed class WebPushClientAdapter(IOptions<WebPushOptions> options) : IWebPushClient
{
    private readonly WebPushClient client = new();

    public async Task SendNotificationAsync(
        string endpoint,
        string p256dh,
        string auth,
        string payloadJson,
        CancellationToken cancellationToken = default)
    {
        var opts = options.Value;
        var subscription = new PushSubscription(endpoint, p256dh, auth);
        var vapidDetails = new VapidDetails(opts.Subject, opts.PublicKey, opts.PrivateKey);

        try
        {
            await client.SendNotificationAsync(subscription, payloadJson, vapidDetails, cancellationToken: cancellationToken);
        }
        catch (WebPushException ex) when (
            ex.StatusCode is HttpStatusCode.Gone or HttpStatusCode.NotFound)
        {
            throw new WebPushSubscriptionGoneException();
        }
    }
}
