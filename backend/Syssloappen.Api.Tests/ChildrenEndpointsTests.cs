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
            new
            {
                Name = "Anna",
                UserName = "Anna",
                Password,
                HouseholdId = secondRegistration.HouseholdId
            });
        var createSecondResponse = await secondClient.PostAsJsonAsync(
            "/api/children",
            new CreateChildRequest { Name = "Erik", UserName = "Erik", Password = Password });

        Assert.Equal(HttpStatusCode.Created, createFirstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Created, createSecondResponse.StatusCode);

        var createdFirstChild = (await createFirstResponse.Content.ReadFromJsonAsync<CreateChildResponse>())!;
        var firstChild = new ChildResponse(createdFirstChild.Id, createdFirstChild.Name);
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
            new CreateChildRequest { Name = "Maja", UserName = "Maja", Password = Password });
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var visibleChildren = await otherAdultClient.GetFromJsonAsync<List<ChildResponse>>("/api/children");

        Assert.Collection(
            visibleChildren!,
            child => Assert.Equal("Maja", child.Name));
    }

    [Fact]
    public async Task Unauthenticated_user_cannot_update_a_child()
    {
        using var client = CreateClient();

        var response = await client.PutAsJsonAsync(
            "/api/children/1",
            new { Name = "Nytt namn" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Child_role_cannot_update_a_child()
    {
        using var client = CreateClient();
        var registration = await RegisterAdult(client, "Familjen Dahl", "adult.update@example.test");
        await Login(client, "adult.update@example.test");
        var child = await CreateChild(client, "Anna");
        await CreateUser(registration.HouseholdId, "child.update@example.test", RoleNames.Child);
        await Login(client, "child.update@example.test");

        var response = await client.PutAsJsonAsync(
            $"/api/children/{child.Id}",
            new { Name = "Nytt namn" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Adult_can_update_a_child_and_another_adult_in_the_same_household_sees_it()
    {
        using var editorClient = CreateClient();
        using var otherAdultClient = CreateClient();
        var registration = await RegisterAdult(
            editorClient,
            "Familjen Ek",
            "editor@example.test");
        await CreateUser(registration.HouseholdId, "viewer@example.test", RoleNames.Adult);
        await Login(editorClient, "editor@example.test");
        await Login(otherAdultClient, "viewer@example.test");
        var child = await CreateChild(editorClient, "Felstavat namn");

        var updateResponse = await editorClient.PutAsJsonAsync(
            $"/api/children/{child.Id}",
            new { Name = "  Rätt namn  " });

        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        var updatedChild = await updateResponse.Content.ReadFromJsonAsync<ChildResponse>();
        Assert.Equal(new ChildResponse(child.Id, "Rätt namn"), updatedChild);

        var visibleChildren = await otherAdultClient.GetFromJsonAsync<List<ChildResponse>>("/api/children");
        Assert.Collection(
            visibleChildren!,
            visibleChild => Assert.Equal(new ChildResponse(child.Id, "Rätt namn"), visibleChild));
    }

    [Fact]
    public async Task Adult_cannot_update_a_child_in_another_household()
    {
        using var firstClient = CreateClient();
        using var secondClient = CreateClient();
        await RegisterAdult(firstClient, "Familjen Fors", "adult.fors@example.test");
        await RegisterAdult(secondClient, "Familjen Gran", "adult.gran@example.test");
        await Login(firstClient, "adult.fors@example.test");
        await Login(secondClient, "adult.gran@example.test");
        var secondHouseholdChild = await CreateChild(secondClient, "Oförändrad");

        var updateResponse = await firstClient.PutAsJsonAsync(
            $"/api/children/{secondHouseholdChild.Id}",
            new { Name = "Otillåten ändring" });

        Assert.Equal(HttpStatusCode.NotFound, updateResponse.StatusCode);

        var secondHouseholdChildren = await secondClient.GetFromJsonAsync<List<ChildResponse>>("/api/children");
        Assert.Collection(
            secondHouseholdChildren!,
            child => Assert.Equal(new ChildResponse(secondHouseholdChild.Id, "Oförändrad"), child));
    }

    [Fact]
    public async Task Client_cannot_change_a_childs_household_when_updating()
    {
        using var firstClient = CreateClient();
        using var secondClient = CreateClient();
        var firstRegistration = await RegisterAdult(
            firstClient,
            "Familjen Holm",
            "adult.holm@example.test");
        var secondRegistration = await RegisterAdult(
            secondClient,
            "Familjen Isaksson",
            "adult.isaksson@example.test");
        await Login(firstClient, "adult.holm@example.test");
        var child = await CreateChild(firstClient, "Före ändring");

        var updateResponse = await firstClient.PutAsJsonAsync(
            $"/api/children/{child.Id}",
            new { Name = "Efter ändring", HouseholdId = secondRegistration.HouseholdId });

        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var storedChild = await dbContext.ChildProfiles
            .AsNoTracking()
            .SingleAsync(storedChild => storedChild.Id == child.Id);

        Assert.Equal("Efter ändring", storedChild.Name);
        Assert.Equal(firstRegistration.HouseholdId, storedChild.HouseholdId);
        Assert.NotEqual(secondRegistration.HouseholdId, storedChild.HouseholdId);
    }

    [Fact]
    public async Task Empty_name_cannot_be_saved_when_updating_a_child()
    {
        using var client = CreateClient();
        await RegisterAdult(client, "Familjen Jansson", "adult.jansson@example.test");
        await Login(client, "adult.jansson@example.test");
        var child = await CreateChild(client, "Oförändrad");

        var updateResponse = await client.PutAsJsonAsync(
            $"/api/children/{child.Id}",
            new { Name = "   " });

        Assert.Equal(HttpStatusCode.BadRequest, updateResponse.StatusCode);

        var visibleChildren = await client.GetFromJsonAsync<List<ChildResponse>>("/api/children");
        Assert.Collection(
            visibleChildren!,
            visibleChild => Assert.Equal(new ChildResponse(child.Id, "Oförändrad"), visibleChild));
    }

    [Fact]
    public async Task Too_long_name_cannot_be_saved_when_updating_a_child()
    {
        using var client = CreateClient();
        await RegisterAdult(client, "Familjen Karlsson", "adult.karlsson@example.test");
        await Login(client, "adult.karlsson@example.test");
        var child = await CreateChild(client, "Oförändrad");

        var updateResponse = await client.PutAsJsonAsync(
            $"/api/children/{child.Id}",
            new { Name = new string('a', 101) });

        Assert.Equal(HttpStatusCode.BadRequest, updateResponse.StatusCode);

        var visibleChildren = await client.GetFromJsonAsync<List<ChildResponse>>("/api/children");
        Assert.Collection(
            visibleChildren!,
            visibleChild => Assert.Equal(new ChildResponse(child.Id, "Oförändrad"), visibleChild));
    }

    [Fact]
    public async Task Unauthenticated_user_cannot_deactivate_a_child()
    {
        using var client = CreateClient();

        var response = await client.DeleteAsync("/api/children/1");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Child_role_cannot_deactivate_a_child()
    {
        using var adultClient = CreateClient();
        using var childClient = CreateClient();
        var registration = await RegisterAdult(
            adultClient,
            "Familjen Lind",
            "adult.deactivate@example.test");
        await Login(adultClient, "adult.deactivate@example.test");
        var child = await CreateChild(adultClient, "Anna");
        await CreateUser(registration.HouseholdId, "child.deactivate@example.test", RoleNames.Child);
        await Login(childClient, "child.deactivate@example.test");

        var response = await childClient.DeleteAsync($"/api/children/{child.Id}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        var visibleChildren = await adultClient.GetFromJsonAsync<List<ChildResponse>>("/api/children");
        Assert.Contains(new ChildResponse(child.Id, "Anna"), visibleChildren!);
    }

    [Fact]
    public async Task Adult_can_deactivate_a_child_without_deleting_its_database_row()
    {
        using var client = CreateClient();
        await RegisterAdult(client, "Familjen Moss", "adult.moss@example.test");
        await Login(client, "adult.moss@example.test");
        var child = await CreateChild(client, "Maja");

        var response = await client.DeleteAsync($"/api/children/{child.Id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var visibleChildren = await client.GetFromJsonAsync<List<ChildResponse>>("/api/children");
        Assert.Empty(visibleChildren!);

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var storedChild = await dbContext.ChildProfiles
            .AsNoTracking()
            .SingleAsync(storedChild => storedChild.Id == child.Id);

        Assert.False(storedChild.IsActive);
    }

    [Fact]
    public async Task Adult_cannot_deactivate_a_child_in_another_household()
    {
        using var firstClient = CreateClient();
        using var secondClient = CreateClient();
        await RegisterAdult(firstClient, "Familjen Nord", "adult.nord@example.test");
        await RegisterAdult(secondClient, "Familjen Ohlsson", "adult.ohlsson@example.test");
        await Login(firstClient, "adult.nord@example.test");
        await Login(secondClient, "adult.ohlsson@example.test");
        var secondHouseholdChild = await CreateChild(secondClient, "Erik");

        var response = await firstClient.DeleteAsync($"/api/children/{secondHouseholdChild.Id}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var storedChild = await dbContext.ChildProfiles
            .AsNoTracking()
            .SingleAsync(child => child.Id == secondHouseholdChild.Id);

        Assert.True(storedChild.IsActive);
        var secondHouseholdChildren = await secondClient.GetFromJsonAsync<List<ChildResponse>>("/api/children");
        Assert.Contains(secondHouseholdChild, secondHouseholdChildren!);
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

    private static async Task<ChildResponse> CreateChild(HttpClient client, string name)
    {
        var response = await client.PostAsJsonAsync(
            "/api/children",
            new CreateChildRequest
            {
                Name = name,
                UserName = $"child-{Guid.NewGuid():N}",
                Password = Password
            });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = (await response.Content.ReadFromJsonAsync<CreateChildResponse>())!;
        return new ChildResponse(created.Id, created.Name);
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
