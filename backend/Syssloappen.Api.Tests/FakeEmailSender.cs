using Syssloappen.Api.Services;

namespace Syssloappen.Api.Tests;

/// <summary>In-memory stand-in for email sending so tests never make a real HTTP call to
/// Resend. Records what was sent so tests can assert on it (e.g. extract a confirmation link
/// out of a captured body).</summary>
public sealed class FakeEmailSender : IEmailSender
{
    public List<(string ToEmail, string? ToName, string Subject, string HtmlBody)> SentMessages { get; } = [];

    public Task SendAsync(
        string toEmail,
        string? toName,
        string subject,
        string htmlBody,
        CancellationToken cancellationToken = default)
    {
        SentMessages.Add((toEmail, toName, subject, htmlBody));
        return Task.CompletedTask;
    }
}
