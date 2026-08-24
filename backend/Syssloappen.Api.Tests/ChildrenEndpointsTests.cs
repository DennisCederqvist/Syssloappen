using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Syssloappen.Api.Authentication;
using Syssloappen.Api.Data;
using Syssloappen.Api.Dtos.Auth;
using Syssloappen.Api.Dtos.Children;
using Xunit;

namespace Syssloappen.Api.Tests;

public sealed class ChildrenEndpointsTests : IDisposable
{
    private const string Password = "Password1";
    private readonly AuthApiFactory factory = new();

    [Fact]
    public async Task Unauthenticated_user_cannot_create_a_child()
    {
        using var client = CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/children",
            new { Name = "Anna" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Child_role_cannot_create_a_child()
    {
        using var client = CreateClient();
        var registration = await RegisterAdult(client, "Familjen Andersson", "adult@example.test");
        await CreateUser(registration.HouseholdId, "child@example.test", RoleNames.Child);
        await Login(client, "child@example.test");

        var response = await client.PostAsJsonAsync(
            "/api/children",
            new { Name = "Anna" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Client_cannot_choose_household_and_adults_only_see_their_own_children()
    {
        using var firstClient = CreateClient();
        using var secondClient = CreateClient();
        var firstRegistration = await RegisterAdult(
            firstClient,
            "Familjen Andersson",
            "adult.one@example.test");
        var secondRegistration = await RegisterAdult(
            secondClient,
            "Familjen Berg",
            "adult.two@example.test");
        await Login(firstClient, "adult.one@example.test");
        await Login(secondClient, "adult.two@example.test");

        // Even if a caller adds an unexpected HouseholdId property, the request DTO ignores it.
        var createFirstResponse = await firstClient.PostAsJsonAsync(
            "/api/children",
            new { Name = "Anna", HouseholdId = secondRegistration.HouseholdId });
        var createSecondResponse = await secondClient.PostAsJsonAsync(
            "/api/children",
            new { Name = "Erik" });

        Assert.Equal(HttpStatusCode.Created, createFirstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Created, createSecondResponse.StatusCode);

        var firstChild = (await createFirstResponse.Content.ReadFromJsonAsync<ChildResponse>())!;
        var firstHouseholdChildren = await firstClient.GetFromJsonAsync<List<ChildResponse>>("/api/children");
        var secondHouseholdChildren = await secondClient.GetFromJsonAsync<List<ChildResponse>>("/api/children");

        Assert.Collection(
            firstHouseholdChildren!,
            child => Assert.Equal("Anna", child.Name));
        Assert.Collection(
            secondHouseholdChildren!,
            child => Assert.Equal("Erik", child.Name));

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var storedChild = await dbContext.ChildProfiles
            .AsNoTracking()
            .SingleAsync(child => child.Id == firstChild.Id);

        Assert.Equal(firstRegistration.HouseholdId, storedChild.HouseholdId);
        Assert.NotEqual(secondRegistration.HouseholdId, storedChild.HouseholdId);
    }

    [Fact]
    public async Task Adults_in_the_same_household_can_see_the_created_child()
    {
        using var creatorClient = CreateClient();
        using var otherAdultClient = CreateClient();
        var registration = await RegisterAdult(
            creatorClient,
            "Familjen Carlsson",
            "creator@example.test");
        await CreateUser(registration.HouseholdId, "other.adult@example.test", RoleNames.Adult);
        await Login(creatorClient, "creator@example.test");
        await Login(otherAdultClient, "other.adult@example.test");

        var createResponse = await creatorClient.PostAsJsonAsync(
            "/api/children",
            new { Name = "Maja" });
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var visibleChildren = await otherAdultClient.GetFromJsonAsync<List<ChildResponse>>("/api/children");

        Assert.Collection(
            visibleChildren!,
            child => Assert.Equal("Maja", child.Name));
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

    private async Task CreateUser(int householdId, string email, string role)
    {
        using var scope = factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            HouseholdId = householdId
        };

        var createResult = await userManager.CreateAsync(user, Password);
        Assert.True(createResult.Succeeded, string.Join(", ", createResult.Errors.Select(error => error.Description)));

        var roleResult = await userManager.AddToRoleAsync(user, role);
        Assert.True(roleResult.Succeeded, string.Join(", ", roleResult.Errors.Select(error => error.Description)));
    }

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

    private static async Task Login(HttpClient client, string email)
    {
        var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest
            {
                Email = email,
                Password = Password
            });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
