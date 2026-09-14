using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Syssloappen.Api.Dtos.Auth;
using Xunit;

namespace Syssloappen.Api.Tests;

public sealed class PasswordResetTests : IDisposable
{
    private const string Password = "Password1";
    private const string NewPassword = "NewPassword1";
    private readonly AuthApiFactory factory = new();

    [Fact]
    public async Task Forgot_password_sends_a_reset_email_for_a_real_confirmed_adult()
    {
        using var client = CreateClient();
        var email = "reset.real@example.test";
        await RegisterAndConfirm(client, "Familjen Glömt", email);
        factory.EmailSender.SentMessages.Clear();

        var response = await client.PostAsJsonAsync(
            "/api/auth/forgot-password",
            new ForgotPasswordRequest { Email = email });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var sent = Assert.Single(factory.EmailSender.SentMessages);
        Assert.Equal(email, sent.ToEmail, ignoreCase: true);
        Assert.Contains("aterstall-losenord", sent.HtmlBody);
    }

    [Fact]
    public async Task Forgot_password_is_neutral_for_an_unknown_email()
    {
        using var client = CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/auth/forgot-password",
            new ForgotPasswordRequest { Email = "nobody@example.test" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty(factory.EmailSender.SentMessages);
    }

    [Fact]
    public async Task Resetting_the_password_lets_the_new_password_log_in_and_the_old_one_stops_working()
    {
        using var client = CreateClient();
        var email = "reset.works@example.test";
        await RegisterAndConfirm(client, "Familjen Byter", email);

        await RequestResetAndSubmitNewPassword(client, email, NewPassword);

        var oldLogin = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest { Email = email, Password = Password });
        Assert.Equal(HttpStatusCode.Unauthorized, oldLogin.StatusCode);

        var newLogin = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest { Email = email, Password = NewPassword });
        Assert.Equal(HttpStatusCode.OK, newLogin.StatusCode);
    }

    [Fact]
    public async Task Invalid_reset_token_is_rejected()
    {
        using var client = CreateClient();
        var email = "reset.badtoken@example.test";
        await RegisterAndConfirm(client, "Familjen Fel Länk", email);

        var response = await client.PostAsJsonAsync(
            "/api/auth/reset-password",
            new ResetPasswordRequest
            {
                UserId = "not-a-real-id",
                Token = "not-a-real-token",
                NewPassword = NewPassword
            });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task A_new_password_that_fails_the_password_policy_is_rejected()
    {
        using var client = CreateClient();
        var email = "reset.weak@example.test";
        await RegisterAndConfirm(client, "Familjen Svagt", email);

        var (userId, token) = await RequestReset(client, email);
        var response = await client.PostAsJsonAsync(
            "/api/auth/reset-password",
            new ResetPasswordRequest { UserId = userId, Token = token, NewPassword = "short" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        // The original password must still work — a rejected reset must not have partially applied.
        var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest { Email = email, Password = Password });
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
    }

    [Fact]
    public async Task A_used_reset_token_cannot_be_replayed()
    {
        using var client = CreateClient();
        var email = "reset.replay@example.test";
        await RegisterAndConfirm(client, "Familjen Återanvänd", email);

        var (userId, token) = await RequestReset(client, email);
        var firstReset = await client.PostAsJsonAsync(
            "/api/auth/reset-password",
            new ResetPasswordRequest { UserId = userId, Token = token, NewPassword = NewPassword });
        Assert.Equal(HttpStatusCode.OK, firstReset.StatusCode);

        var replay = await client.PostAsJsonAsync(
            "/api/auth/reset-password",
            new ResetPasswordRequest { UserId = userId, Token = token, NewPassword = "AnotherPassword1" });
        Assert.Equal(HttpStatusCode.BadRequest, replay.StatusCode);
    }

    public void Dispose() => factory.Dispose();

    private HttpClient CreateClient() => factory.CreateClient(new WebApplicationFactoryClientOptions
    {
        BaseAddress = new Uri("https://localhost"),
        AllowAutoRedirect = false,
        HandleCookies = true
    });

    private async Task RegisterAndConfirm(HttpClient client, string householdName, string email)
    {
        var response = await client.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterAdultRequest { HouseholdName = householdName, Email = email, Password = Password });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        await TestEmailConfirmation.ConfirmLatestAsync(client, factory.EmailSender, email);
    }

    private async Task<(string UserId, string Token)> RequestReset(HttpClient client, string email)
    {
        var response = await client.PostAsJsonAsync(
            "/api/auth/forgot-password",
            new ForgotPasswordRequest { Email = email });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var message = factory.EmailSender.SentMessages.Last(sent =>
            string.Equals(sent.ToEmail, email, StringComparison.OrdinalIgnoreCase));
        var match = Regex.Match(message.HtmlBody, "userId=([^&\"]+)&token=([^\"]+)");
        Assert.True(match.Success, "Reset link not found in the sent email body.");

        return (Uri.UnescapeDataString(match.Groups[1].Value), Uri.UnescapeDataString(match.Groups[2].Value));
    }

    private async Task RequestResetAndSubmitNewPassword(HttpClient client, string email, string newPassword)
    {
        var (userId, token) = await RequestReset(client, email);
        var response = await client.PostAsJsonAsync(
            "/api/auth/reset-password",
            new ResetPasswordRequest { UserId = userId, Token = token, NewPassword = newPassword });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
