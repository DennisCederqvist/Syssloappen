using System.Collections.Concurrent;

namespace Syssloappen.Api.Services;

/// <summary>
/// Records the most recent "email" LoggingEmailSender logged for each address, so
/// e2e tests — which have no real inbox to check — can fetch a confirmation or
/// password-reset link directly instead of scraping the server console. Registered
/// unconditionally (a small in-memory store is harmless), but only ever readable
/// through the /dev/last-email endpoint, which Program.cs maps solely when
/// ASPNETCORE_ENVIRONMENT is Development.
/// </summary>
public sealed class DevEmailStore
{
    private readonly ConcurrentDictionary<string, DevEmailRecord> byEmail =
        new(StringComparer.OrdinalIgnoreCase);

    public void Record(string toEmail, string subject, string htmlBody) =>
        byEmail[toEmail] = new DevEmailRecord(subject, htmlBody);

    public bool TryGet(string toEmail, out DevEmailRecord record) =>
        byEmail.TryGetValue(toEmail, out record!);
}

public sealed record DevEmailRecord(string Subject, string HtmlBody);
