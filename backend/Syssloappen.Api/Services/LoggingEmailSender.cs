namespace Syssloappen.Api.Services;

/// <summary>
/// Development-only email "sender": logs the message instead of sending it, so a confirmation
/// link can be copied straight out of the console. Never used once <c>Email:Provider</c> is set
/// to <c>Resend</c>.
/// </summary>
public sealed class LoggingEmailSender(ILogger<LoggingEmailSender> logger, DevEmailStore devEmailStore)
    : IEmailSender
{
    public Task SendAsync(
        string toEmail,
        string? toName,
        string subject,
        string htmlBody,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation(
            "Email not sent (Email:Provider is not Resend) — to: {ToEmail}, subject: {Subject}\n{HtmlBody}",
            toEmail,
            subject,
            htmlBody);
        devEmailStore.Record(toEmail, subject, htmlBody);
        return Task.CompletedTask;
    }
}
