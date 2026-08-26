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
using Syssloappen.Api.Dtos.ChoreAssignments;
using Syssloappen.Api.Dtos.Chores;
using Syssloappen.Api.Models;
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

    [Fact]
    public async Task Only_adult_can_update_and_deactivate_a_chore()
    {
        using var anonymousClient = CreateClient();
        using var adultClient = CreateClient();
        using var childClient = CreateClient();
        await RegisterAndLoginAdult(
            adultClient,
            "Familjen Gran",
            "adult.manage.chore@example.test");
        var child = await CreateChild(adultClient, "Liam");
        var chore = await CreateChoreResponse(adultClient, "Bädda sängen", 5);
        await PairChild(adultClient, childClient, child.Id);
        var update = new UpdateChoreRequest { Title = "Bädda sängen fint", Points = 10 };

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await anonymousClient.PutAsJsonAsync($"/api/chores/{chore.Id}", update)).StatusCode);
        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await childClient.PutAsJsonAsync($"/api/chores/{chore.Id}", update)).StatusCode);
        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await childClient.DeleteAsync($"/api/chores/{chore.Id}")).StatusCode);
        Assert.Equal(
            HttpStatusCode.OK,
            (await adultClient.PutAsJsonAsync($"/api/chores/{chore.Id}", update)).StatusCode);
        Assert.Equal(
            HttpStatusCode.NoContent,
            (await adultClient.DeleteAsync($"/api/chores/{chore.Id}")).StatusCode);
    }

    [Fact]
    public async Task Adult_updates_trimmed_fields_and_only_future_assignments_use_new_points()
    {
        using var adultClient = CreateClient();
        var registration = await RegisterAndLoginAdult(
            adultClient,
            "Familjen Holm",
            "update.chore@example.test");
        var child = await CreateChild(adultClient, "Vera");
        var chore = await CreateChoreResponse(adultClient, "Duka", 10);
        var firstAssignment = await AssignChore(adultClient, chore.Id, child.Id);

        var response = await adultClient.PutAsJsonAsync(
            $"/api/chores/{chore.Id}",
            new
            {
                Title = "  Duka bordet  ",
                Description = "  Lägg fram glas  ",
                Points = 20,
                HouseholdId = registration.HouseholdId + 1,
                CreatedByUserId = "forged-user",
                IsActive = false
            });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadFromJsonAsync<ChoreResponse>();
        Assert.NotNull(updated);
        Assert.Equal("Duka bordet", updated.Title);
        Assert.Equal("Lägg fram glas", updated.Description);
        Assert.Equal(20, updated.Points);
        var secondAssignment = await AssignChore(adultClient, chore.Id, child.Id);
        Assert.Equal(10, firstAssignment.Points);
        Assert.Equal(20, secondAssignment.Points);

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var stored = await dbContext.Chores.AsNoTracking().SingleAsync();
        Assert.Equal(registration.HouseholdId, stored.HouseholdId);
        Assert.True(stored.IsActive);
        Assert.NotEqual("forged-user", stored.CreatedByUserId);
    }

    [Fact]
    public async Task Invalid_update_data_and_identifiers_do_not_change_a_chore()
    {
        using var adultClient = CreateClient();
        await RegisterAndLoginAdult(
            adultClient,
            "Familjen Isaksson",
            "invalid.update.chore@example.test");
        var chore = await CreateChoreResponse(adultClient, "Vattna blommorna", 5);

        Assert.Equal(
            HttpStatusCode.BadRequest,
            (await adultClient.PutAsJsonAsync(
                "/api/chores/0",
                new UpdateChoreRequest { Title = "Giltig", Points = 5 })).StatusCode);
        Assert.Equal(
            HttpStatusCode.BadRequest,
            (await adultClient.PutAsJsonAsync(
                $"/api/chores/{chore.Id}",
                new UpdateChoreRequest { Title = "   ", Points = 5 })).StatusCode);
        Assert.Equal(
            HttpStatusCode.BadRequest,
            (await adultClient.PutAsJsonAsync(
                $"/api/chores/{chore.Id}",
                new UpdateChoreRequest { Title = "Giltig", Points = 6 })).StatusCode);
        Assert.Equal(
            HttpStatusCode.BadRequest,
            (await adultClient.PutAsJsonAsync(
                $"/api/chores/{chore.Id}",
                new UpdateChoreRequest { Title = new string('x', 101), Points = 5 })).StatusCode);
        Assert.Equal(
            HttpStatusCode.NotFound,
            (await adultClient.PutAsJsonAsync(
                $"/api/chores/{int.MaxValue}",
                new UpdateChoreRequest { Title = "Giltig", Points = 5 })).StatusCode);
        Assert.Equal(
            HttpStatusCode.BadRequest,
            (await adultClient.DeleteAsync("/api/chores/-1")).StatusCode);

        var listed = await adultClient.GetFromJsonAsync<List<ChoreResponse>>("/api/chores");
        Assert.Equal("Vattna blommorna", Assert.Single(listed!).Title);
    }

    [Fact]
    public async Task Manipulated_chore_ids_cannot_manage_another_household()
    {
        using var firstClient = CreateClient();
        using var secondClient = CreateClient();
        await RegisterAndLoginAdult(
            firstClient,
            "Familjen Jansson",
            "first.manage.chore@example.test");
        await RegisterAndLoginAdult(
            secondClient,
            "Familjen Karlsson",
            "second.manage.chore@example.test");
        var secondChore = await CreateChoreResponse(secondClient, "Ta ut soporna", 15);

        Assert.Equal(
            HttpStatusCode.NotFound,
            (await firstClient.PutAsJsonAsync(
                $"/api/chores/{secondChore.Id}",
                new UpdateChoreRequest { Title = "Kapad", Points = 5 })).StatusCode);
        Assert.Equal(
            HttpStatusCode.NotFound,
            (await firstClient.DeleteAsync($"/api/chores/{secondChore.Id}")).StatusCode);

        var secondList = await secondClient.GetFromJsonAsync<List<ChoreResponse>>("/api/chores");
        Assert.Equal("Ta ut soporna", Assert.Single(secondList!).Title);
    }

    [Fact]
    public async Task Deactivation_hides_template_blocks_new_assignments_and_preserves_history()
    {
        using var adultClient = CreateClient();
        using var childClient = CreateClient();
        await RegisterAndLoginAdult(
            adultClient,
            "Familjen Lind",
            "history.chore@example.test");
        var child = await CreateChild(adultClient, "Maja");
        var chore = await CreateChoreResponse(adultClient, "Mata katten", 15);
        var assignment = await AssignChore(adultClient, chore.Id, child.Id);
        await PairChild(adultClient, childClient, child.Id);
        Assert.Equal(
            HttpStatusCode.OK,
            (await childClient.PostAsync(
                $"/api/child/chore-assignments/{assignment.Id}/submit",
                null)).StatusCode);
        Assert.Equal(
            HttpStatusCode.OK,
            (await adultClient.PostAsJsonAsync(
                $"/api/chore-assignments/{assignment.Id}/approve",
                new ReviewChoreAssignmentRequest())).StatusCode);

        Assert.Equal(
            HttpStatusCode.NoContent,
            (await adultClient.DeleteAsync($"/api/chores/{chore.Id}")).StatusCode);
        Assert.Empty((await adultClient.GetFromJsonAsync<List<ChoreResponse>>("/api/chores"))!);
        Assert.Equal(
            HttpStatusCode.NotFound,
            (await adultClient.PostAsJsonAsync(
                "/api/chore-assignments",
                new CreateChoreAssignmentRequest { ChoreId = chore.Id, ChildId = child.Id })).StatusCode);
        var assignments = await adultClient.GetFromJsonAsync<List<AdultChoreAssignmentResponse>>(
            "/api/chore-assignments");
        Assert.Equal(nameof(ChoreAssignmentStatus.Approved), Assert.Single(assignments!).Status);

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var storedChore = await dbContext.Chores.AsNoTracking().SingleAsync();
        var storedAssignment = await dbContext.ChoreAssignments.AsNoTracking().SingleAsync();
        var completion = await dbContext.ChoreCompletions.AsNoTracking().SingleAsync();
        Assert.False(storedChore.IsActive);
        Assert.Equal(15, storedAssignment.Points);
        Assert.Equal(15, completion.PointsAwarded);
        Assert.Equal(chore.Id, completion.ChoreId);
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

    private static async Task<ChoreResponse> CreateChoreResponse(
        HttpClient client,
        string title,
        int points)
    {
        var response = await client.PostAsJsonAsync(
            "/api/chores",
            new CreateChoreRequest { Title = title, Points = points });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ChoreResponse>())!;
    }

    private static async Task<ChoreAssignmentResponse> AssignChore(
        HttpClient client,
        int choreId,
        int childId)
    {
        var response = await client.PostAsJsonAsync(
            "/api/chore-assignments",
            new CreateChoreAssignmentRequest { ChoreId = choreId, ChildId = childId });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ChoreAssignmentResponse>())!;
    }

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
