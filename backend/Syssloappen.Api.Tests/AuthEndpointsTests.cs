using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Syssloappen.Api.Authentication;
using Syssloappen.Api.Dtos.Auth;
using Xunit;

namespace Syssloappen.Api.Tests;

public sealed class AuthEndpointsTests : IDisposable
{
    private const string Password = "Password1";
    private readonly AuthApiFactory factory = new();

    [Fact]
    public async Task Login_identifies_each_adult_and_their_own_household()
    {
        using var firstHouseholdClient = CreateClient();
        using var secondHouseholdClient = CreateClient();

        var firstRegistration = await RegisterAdult(
            firstHouseholdClient,
            "Familjen Andersson",
            "adult.one@example.test");
        var secondRegistration = await RegisterAdult(
            secondHouseholdClient,
            "Familjen Berg",
            "adult.two@example.test");

        Assert.NotEqual(firstRegistration.HouseholdId, secondRegistration.HouseholdId);

        var firstLogin = await Login(firstHouseholdClient, "ADULT.ONE@EXAMPLE.TEST", Password);
        var secondLogin = await Login(secondHouseholdClient, "adult.two@example.test", Password);

        Assert.Equal(firstRegistration.HouseholdId, firstLogin.HouseholdId);
        Assert.Equal(secondRegistration.HouseholdId, secondLogin.HouseholdId);
        Assert.Equal(RoleNames.Adult, firstLogin.Role);
        Assert.Equal(RoleNames.Adult, secondLogin.Role);

        var firstCurrentUser = await firstHouseholdClient.GetFromJsonAsync<CurrentUserResponse>("/api/auth/me");
        var secondCurrentUser = await secondHouseholdClient.GetFromJsonAsync<CurrentUserResponse>("/api/auth/me");

        Assert.NotNull(firstCurrentUser);
        Assert.NotNull(secondCurrentUser);
        Assert.Equal(firstRegistration.HouseholdId, firstCurrentUser.HouseholdId);
        Assert.Equal(secondRegistration.HouseholdId, secondCurrentUser.HouseholdId);
        Assert.NotEqual(firstCurrentUser.UserId, secondCurrentUser.UserId);
    }

    [Fact]
    public async Task Wrong_password_does_not_authenticate_the_user()
    {
        using var client = CreateClient();
        await RegisterAdult(client, "Familjen Carlsson", "adult.three@example.test");

        var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest
            {
                Email = "adult.three@example.test",
                Password = "WrongPassword1"
            });

        Assert.Equal(HttpStatusCode.Unauthorized, loginResponse.StatusCode);

        var meResponse = await client.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, meResponse.StatusCode);
    }

    [Fact]
    public async Task Adult_email_remains_case_insensitively_unique()
    {
        using var firstClient = CreateClient();
        using var secondClient = CreateClient();
        await RegisterAdult(firstClient, "Familjen Email", "unique@example.test");

        var duplicateResponse = await secondClient.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterAdultRequest
            {
                HouseholdName = "Ska rullas tillbaka",
                Email = "UNIQUE@EXAMPLE.TEST",
                Password = Password
            });

        Assert.Equal(HttpStatusCode.BadRequest, duplicateResponse.StatusCode);
    }

    [Fact]
    public async Task Logout_removes_access_to_protected_endpoints()
    {
        using var client = CreateClient();

        var beforeLogin = await client.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, beforeLogin.StatusCode);

        await RegisterAdult(client, "Familjen Dahl", "adult.four@example.test");
        await Login(client, "adult.four@example.test", Password);

        var whileLoggedIn = await client.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.OK, whileLoggedIn.StatusCode);

        var logoutResponse = await client.PostAsync("/api/auth/logout", content: null);
        Assert.Equal(HttpStatusCode.NoContent, logoutResponse.StatusCode);

        var afterLogout = await client.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, afterLogout.StatusCode);
    }

    public void Dispose()
    {
        factory.Dispose();
    }

    private HttpClient CreateClient() => factory.CreateClient(
        new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false,
            HandleCookies = true
        });

    private static async Task<RegisterAdultResponse> RegisterAdult(
        HttpClient client,
        string householdName,
        string email)
    {
        var response = await client.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterAdultRequest
            {
                HouseholdName = householdName,
                Email = email,
                Password = Password
            });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<RegisterAdultResponse>())!;
    }

    private static async Task<LoginResponse> Login(HttpClient client, string email, string password)
    {
        var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest
            {
                Email = email,
                Password = password
            });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<LoginResponse>())!;
    }
}
