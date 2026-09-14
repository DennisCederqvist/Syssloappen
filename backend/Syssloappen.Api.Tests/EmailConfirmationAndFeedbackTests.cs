using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Syssloappen.Api.Dtos.Auth;
using Syssloappen.Api.Dtos.Children;
using Syssloappen.Api.Dtos.Support;
using Xunit;

namespace Syssloappen.Api.Tests;

public sealed class EmailConfirmationAndFeedbackTests : IDisposable
{
    private const string Password = "Password1";
    private readonly AuthApiFactory factory = new();

    [Fact]
    public async Task Registration_sends_a_confirmation_email_and_login_is_blocked_until_confirmed()
    {
        using var client = CreateClient();
        var email = "unconfirmed@example.test";

        var registerResponse = await client.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterAdultRequest { HouseholdName = "Familjen Väntar", Email = email, Password = Password });
        Assert.Equal(HttpStatusCode.Created, registerResponse.StatusCode);

        var sent = Assert.Single(factory.EmailSender.SentMessages);
        Assert.Equal(email, sent.ToEmail, ignoreCase: true);
        Assert.Contains("bekrafta-epost", sent.HtmlBody);

        // Correct password, but the account is not confirmed yet.
        var blockedLogin = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest { Email = email, Password = Password });
        Assert.Equal(HttpStatusCode.Forbidden, blockedLogin.StatusCode);

        var me = await client.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, me.StatusCode);
    }

    [Fact]
    public async Task A_spoofed_host_header_does_not_change_the_emailed_confirmation_link()
    {
        using var client = CreateClient();
        var email = "spoofed-host@example.test";

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/register")
        {
            Content = JsonContent.Create(
                new RegisterAdultRequest { HouseholdName = "Familjen Spoof", Email = email, Password = Password }),
        };
        request.Headers.Host = "attacker-controlled.example";

        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var sent = Assert.Single(factory.EmailSender.SentMessages);
        Assert.DoesNotContain("attacker-controlled.example", sent.HtmlBody);
    }

    [Fact]
    public async Task Wrong_password_on_an_unconfirmed_account_still_gives_the_neutral_credentials_error()
    {
        using var client = CreateClient();
        var email = "unconfirmed.wrongpw@example.test";
        await Register(client, "Familjen Fel", email);

        var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest { Email = email, Password = "WrongPassword1" });

        // Never reveal confirmation status to someone who doesn't even know the password.
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Confirming_the_email_allows_login_to_succeed()
    {
        using var client = CreateClient();
        var email = "confirms@example.test";
        await Register(client, "Familjen Bekräftar", email);

        await ConfirmLatest(client, email);

        var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest { Email = email, Password = Password });
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
    }

    [Fact]
    public async Task Invalid_confirmation_token_is_rejected()
    {
        using var client = CreateClient();
        var email = "badtoken@example.test";
        await Register(client, "Familjen Fel Kod", email);

        var response = await client.PostAsJsonAsync(
            "/api/auth/confirm-email",
            new ConfirmEmailRequest { UserId = "not-a-real-id", Token = "not-a-real-token" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Resend_confirmation_is_neutral_for_unknown_already_confirmed_and_real_accounts()
    {
        using var client = CreateClient();
        var email = "resend@example.test";
        await Register(client, "Familjen Igen", email);
        Assert.Single(factory.EmailSender.SentMessages);

        var forUnknown = await client.PostAsJsonAsync(
            "/api/auth/resend-confirmation",
            new ResendConfirmationRequest { Email = "nobody@example.test" });
        Assert.Equal(HttpStatusCode.OK, forUnknown.StatusCode);
        Assert.Single(factory.EmailSender.SentMessages); // no email actually sent for an unknown address

        var forReal = await client.PostAsJsonAsync(
            "/api/auth/resend-confirmation",
            new ResendConfirmationRequest { Email = email });
        Assert.Equal(HttpStatusCode.OK, forReal.StatusCode);
        Assert.Equal(2, factory.EmailSender.SentMessages.Count); // a second email really was sent

        await ConfirmLatest(client, email);

        var forAlreadyConfirmed = await client.PostAsJsonAsync(
            "/api/auth/resend-confirmation",
            new ResendConfirmationRequest { Email = email });
        Assert.Equal(HttpStatusCode.OK, forAlreadyConfirmed.StatusCode);
        Assert.Equal(2, factory.EmailSender.SentMessages.Count); // still 2 — nothing sent once confirmed
    }

    [Fact]
    public async Task An_unconfirmed_adult_account_does_not_block_a_sibling_childs_fallback_login()
    {
        using var adultClient = CreateClient();
        using var childClient = CreateClient();

        var registration = await Register(adultClient, "Familjen Barn", "adult.unconfirmed@example.test");
        await ConfirmLatest(adultClient, "adult.unconfirmed@example.test");
        await Login(adultClient, "adult.unconfirmed@example.test");

        var childResponse = await adultClient.PostAsJsonAsync(
            "/api/children",
            new CreateChildRequest { Name = "Alex", UserName = "alex", Password = Password });
        Assert.Equal(HttpStatusCode.Created, childResponse.StatusCode);

        // The Child account has no email at all — RequireConfirmedEmail is intentionally never
        // set globally, so fallback login must still work regardless of the adult's own
        // confirmation state.
        var fallbackResponse = await childClient.PostAsJsonAsync(
            "/api/auth/child/login",
            new ChildFallbackLoginRequest
            {
                FamilyCode = registration.FamilyCode,
                UserName = "alex",
                Password = Password
            });
        Assert.Equal(HttpStatusCode.OK, fallbackResponse.StatusCode);
    }

    [Fact]
    public async Task Adult_can_submit_feedback_and_it_is_emailed_using_the_authenticated_identity()
    {
        using var client = CreateClient();
        var email = "feedback.sender@example.test";
        await Register(client, "Familjen Feedback", email);
        await ConfirmLatest(client, email);
        await Login(client, email);
        factory.EmailSender.SentMessages.Clear();

        var response = await client.PostAsJsonAsync(
            "/api/support/contact",
            new SupportContactRequest { Message = "Något är trasigt <script>alert(1)</script>" });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var sent = Assert.Single(factory.EmailSender.SentMessages);
        Assert.Contains(email, sent.HtmlBody);
        // The message is HTML-encoded rather than trusted verbatim.
        Assert.DoesNotContain("<script>", sent.HtmlBody);
        Assert.Contains("&lt;script&gt;", sent.HtmlBody);
    }

    [Fact]
    public async Task Feedback_requires_an_authenticated_adult()
    {
        using var anonymous = CreateClient();
        var response = await anonymous.PostAsJsonAsync(
            "/api/support/contact",
            new SupportContactRequest { Message = "Hej" });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    public void Dispose() => factory.Dispose();

    private HttpClient CreateClient() => factory.CreateClient(new WebApplicationFactoryClientOptions
    {
        BaseAddress = new Uri("https://localhost"),
        AllowAutoRedirect = false,
        HandleCookies = true
    });

    private async Task<RegisterAdultResponse> Register(HttpClient client, string householdName, string email)
    {
        var response = await client.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterAdultRequest { HouseholdName = householdName, Email = email, Password = Password });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<RegisterAdultResponse>())!;
    }

    private static async Task Login(HttpClient client, string email)
    {
        var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest { Email = email, Password = Password });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private async Task ConfirmLatest(HttpClient client, string email) =>
        await TestEmailConfirmation.ConfirmLatestAsync(client, factory.EmailSender, email);
}
