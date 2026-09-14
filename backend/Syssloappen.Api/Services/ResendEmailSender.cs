using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.Options;

namespace Syssloappen.Api.Services;

/// <summary>
/// Sends email via Resend's HTTP API (https://resend.com/docs/api-reference/emails/send-email).
/// No Resend SDK needed for a single send call.
/// </summary>
public sealed class ResendEmailSender(HttpClient httpClient, IOptions<ResendOptions> options) : IEmailSender
{
    public async Task SendAsync(
        string toEmail,
        string? toName,
        string subject,
        string htmlBody,
        CancellationToken cancellationToken = default)
    {
        var config = options.Value;
        var to = string.IsNullOrWhiteSpace(toName) ? toEmail : $"{toName} <{toEmail}>";

        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.resend.com/emails");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", config.ApiKey);
        request.Content = JsonContent.Create(new
        {
            from = config.FromAddress,
            to = new[] { to },
            subject,
            html = htmlBody
        });

        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException($"Resend send failed ({(int)response.StatusCode}): {body}");
        }
    }
}
