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
using Syssloappen.Api.Dtos.Chores;
using Xunit;

namespace Syssloappen.Api.Tests;

public sealed class ChoresEndpointsTests : IDisposable
{
    private const string Password = "Password1";
    private readonly AuthApiFactory factory = new();

    [Fact]
    public async Task Only_adult_can_create_a_chore()
    {
        using var anonymousClient = CreateClient();
        using var adultClient = CreateClient();
        using var childClient = CreateClient();

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await CreateChore(anonymousClient, "Mata katten")).StatusCode);

        await RegisterAndLoginAdult(adultClient, "Familjen Andersson", "adult.chore@example.test");
        var child = await CreateChild(adultClient, "Maja");
        await PairChild(adultClient, childClient, child.Id);

        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await CreateChore(childClient, "Städa rummet")).StatusCode);
        Assert.Equal(
            HttpStatusCode.Created,
            (await CreateChore(adultClient, "Töm diskmaskinen")).StatusCode);
    }

    [Fact]
    public async Task Adult_creates_trimmed_chore_with_backend_derived_ownership()
    {
        using var creatorClient = CreateClient();
        using var otherHouseholdClient = CreateClient();
        var creator = await RegisterAndLoginAdult(
            creatorClient,
            "Familjen Berg",
            "creator.chore@example.test");
        var other = await RegisterAndLoginAdult(
            otherHouseholdClient,
            "Familjen Carlsson",
            "other.chore@example.test");
        var beforeCreate = DateTime.UtcNow;

        var response = await creatorClient.PostAsJsonAsync(
            "/api/chores",
            new
            {
                Title = "  Mata katten  ",
                Description = "  Före frukost  ",
                HouseholdId = other.HouseholdId,
                CreatedByUserId = "forged-user",
                CreatedAt = DateTime.UtcNow.AddYears(-10)
            });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<ChoreResponse>();
        Assert.NotNull(created);
        Assert.Equal("Mata katten", created.Title);
        Assert.Equal("Före frukost", created.Description);
        Assert.True(created.CreatedAt >= beforeCreate);

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var stored = await dbContext.Chores.AsNoTracking().SingleAsync();
        var creatorUser = await dbContext.Users.AsNoTracking().SingleAsync(user =>
            user.Email == "creator.chore@example.test");

        Assert.Equal(creator.HouseholdId, stored.HouseholdId);
        Assert.NotEqual(other.HouseholdId, stored.HouseholdId);
        Assert.Equal(creatorUser.Id, stored.CreatedByUserId);
        Assert.NotEqual("forged-user", stored.CreatedByUserId);
    }

    [Fact]
    public async Task Empty_or_too_long_chore_data_is_not_saved()
    {
        using var client = CreateClient();
        await RegisterAndLoginAdult(client, "Familjen Dahl", "validation.chore@example.test");

        var emptyTitle = await CreateChore(client, "   ");
        var longTitle = await CreateChore(client, new string('a', 101));
        var longDescription = await client.PostAsJsonAsync(
            "/api/chores",
            new CreateChoreRequest
            {
                Title = "Giltig titel",
                Description = new string('a', 501)
            });

        Assert.Equal(HttpStatusCode.BadRequest, emptyTitle.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, longTitle.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, longDescription.StatusCode);
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Empty(await dbContext.Chores.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task Adults_only_list_chores_from_their_own_household()
    {
        using var creatorClient = CreateClient();
        using var sameHouseholdClient = CreateClient();
        using var otherHouseholdClient = CreateClient();
        var registration = await RegisterAndLoginAdult(
            creatorClient,
            "Familjen Ek",
            "creator.list@example.test");
        await CreateAdult(registration.HouseholdId, "viewer.list@example.test");
        await Login(sameHouseholdClient, "viewer.list@example.test");
        await RegisterAndLoginAdult(
            otherHouseholdClient,
            "Familjen Fors",
            "other.list@example.test");
        Assert.Equal(
            HttpStatusCode.Created,
            (await CreateChore(creatorClient, "Mata katten")).StatusCode);
        Assert.Equal(
            HttpStatusCode.Created,
            (await CreateChore(otherHouseholdClient, "Ta ut soporna")).StatusCode);

        var creatorChores = await creatorClient.GetFromJsonAsync<List<ChoreResponse>>("/api/chores");
        var sameHouseholdChores = await sameHouseholdClient.GetFromJsonAsync<List<ChoreResponse>>("/api/chores");
        var otherChores = await otherHouseholdClient.GetFromJsonAsync<List<ChoreResponse>>("/api/chores");

        Assert.Collection(creatorChores!, chore => Assert.Equal("Mata katten", chore.Title));
        Assert.Collection(sameHouseholdChores!, chore => Assert.Equal("Mata katten", chore.Title));
        Assert.Collection(otherChores!, chore => Assert.Equal("Ta ut soporna", chore.Title));
    }

    public void Dispose() => factory.Dispose();

    private HttpClient CreateClient() => factory.CreateClient(
        new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false,
            HandleCookies = true
        });

    private async Task CreateAdult(int householdId, string email)
    {
        using var scope = factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            HouseholdId = householdId
        };
        Assert.True((await userManager.CreateAsync(user, Password)).Succeeded);
        Assert.True((await userManager.AddToRoleAsync(user, RoleNames.Adult)).Succeeded);
    }

    private static Task<HttpResponseMessage> CreateChore(HttpClient client, string title) =>
        client.PostAsJsonAsync("/api/chores", new CreateChoreRequest { Title = title });

    private static async Task<CreateChildResponse> CreateChild(HttpClient client, string name)
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
        return (await response.Content.ReadFromJsonAsync<CreateChildResponse>())!;
    }

    private static async Task PairChild(HttpClient adultClient, HttpClient childClient, int childId)
    {
        var issueResponse = await adultClient.PostAsync(
            $"/api/children/{childId}/pairing-codes",
            null);
        Assert.Equal(HttpStatusCode.Created, issueResponse.StatusCode);
        var code = (await issueResponse.Content.ReadFromJsonAsync<ChildPairingCodeResponse>())!;
        var pairResponse = await childClient.PostAsJsonAsync(
            "/api/auth/child/pair",
            new PairChildDeviceRequest { Code = code.Code });
        Assert.Equal(HttpStatusCode.OK, pairResponse.StatusCode);
    }

    private static async Task<RegisterAdultResponse> RegisterAndLoginAdult(
        HttpClient client,
        string householdName,
        string email)
    {
        var registerResponse = await client.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterAdultRequest
            {
                HouseholdName = householdName,
                Email = email,
                Password = Password
            });
        Assert.Equal(HttpStatusCode.Created, registerResponse.StatusCode);
        var registration = (await registerResponse.Content.ReadFromJsonAsync<RegisterAdultResponse>())!;
        await Login(client, email);
        return registration;
    }

    private static async Task Login(HttpClient client, string email)
    {
        var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest { Email = email, Password = Password });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
