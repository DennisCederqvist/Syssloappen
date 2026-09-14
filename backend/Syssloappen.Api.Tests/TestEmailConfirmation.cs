using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Syssloappen.Api.Dtos.Auth;
using Xunit;

namespace Syssloappen.Api.Tests;

/// <summary>Shared helper so every test's registration setup can confirm the account the same
/// way a real user would (following the emailed link), now that registration no longer confirms
/// immediately. Extracts the link from whatever <see cref="FakeEmailSender"/> captured rather
/// than reaching into Identity internals, so it exercises the real confirm-email endpoint.</summary>
public static class TestEmailConfirmation
{
    public static async Task ConfirmLatestAsync(HttpClient client, FakeEmailSender emailSender, string email)
    {
        var message = emailSender.SentMessages.Last(sent =>
            string.Equals(sent.ToEmail, email, StringComparison.OrdinalIgnoreCase));
        var match = Regex.Match(message.HtmlBody, "userId=([^&\"]+)&token=([^\"]+)");
        Assert.True(match.Success, "Confirmation link not found in the sent email body.");

        var response = await client.PostAsJsonAsync(
            "/api/auth/confirm-email",
            new ConfirmEmailRequest
            {
                UserId = Uri.UnescapeDataString(match.Groups[1].Value),
                Token = Uri.UnescapeDataString(match.Groups[2].Value)
            });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
