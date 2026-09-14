namespace Syssloappen.Api.Services;

/// <summary>
/// Sends a single transactional email. Implementations never decide *whether* to send — callers
/// (registration, support contact) own that decision — only *how*, so the sending mechanism can
/// be swapped per environment without touching call sites.
/// </summary>
public interface IEmailSender
{
    Task SendAsync(
        string toEmail,
        string? toName,
        string subject,
        string htmlBody,
        CancellationToken cancellationToken = default);
}
