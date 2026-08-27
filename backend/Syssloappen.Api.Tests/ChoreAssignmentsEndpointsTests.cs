using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Syssloappen.Api.Data;
using Syssloappen.Api.Dtos.Auth;
using Syssloappen.Api.Dtos.Children;
using Syssloappen.Api.Dtos.ChoreAssignments;
using Syssloappen.Api.Dtos.Chores;
using Xunit;

namespace Syssloappen.Api.Tests;

public sealed class ChoreAssignmentsEndpointsTests : IDisposable
{
    private const string Password = "Password1";
    private readonly AuthApiFactory factory = new();

    [Fact]
    public async Task Only_adult_can_assign_a_chore()
    {
        using var anonymousClient = CreateClient();
        using var adultClient = CreateClient();
        using var childClient = CreateClient();
        await RegisterAndLoginAdult(
            adultClient,
            "Familjen Andersson",
            "adult.assignment@example.test");
        var child = await CreateChild(adultClient, "Maja");
        var chore = await CreateChore(adultClient, "Mata katten");
        await PairChild(adultClient, childClient, child.Id);

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await AssignChore(anonymousClient, chore.Id, child.Id)).StatusCode);
        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await AssignChore(childClient, chore.Id, child.Id)).StatusCode);
        Assert.Equal(
            HttpStatusCode.Created,
            (await AssignChore(adultClient, chore.Id, child.Id)).StatusCode);
    }

    [Fact]
    public async Task Adult_assigns_chore_with_backend_derived_ownership_and_time()
    {
        using var adultClient = CreateClient();
        using var otherHouseholdClient = CreateClient();
        var adult = await RegisterAndLoginAdult(
            adultClient,
            "Familjen Berg",
            "owner.assignment@example.test");
        var otherAdult = await RegisterAndLoginAdult(
            otherHouseholdClient,
            "Familjen Carlsson",
            "other.owner.assignment@example.test");
        var child = await CreateChild(adultClient, "Leo");
        var chore = await CreateChore(adultClient, "Töm diskmaskinen");
        var beforeAssignment = DateTime.UtcNow;

        var response = await adultClient.PostAsJsonAsync(
            "/api/chore-assignments",
            new
            {
                Id = 123456,
                ChoreId = chore.Id,
                ChildId = child.Id,
                ChildProfileId = int.MaxValue,
                HouseholdId = otherAdult.HouseholdId,
                AssignedByUserId = "forged-user",
                CreatedByUserId = "forged-creator",
                AssignedAt = DateTime.UtcNow.AddYears(-10),
                Role = "Child"
            });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<ChoreAssignmentResponse>();
        Assert.NotNull(created);
        Assert.True(created.Id > 0);
        Assert.NotEqual(123456, created.Id);
        Assert.Equal(chore.Id, created.ChoreId);
        Assert.Equal(child.Id, created.ChildId);
        Assert.True(created.AssignedAt >= beforeAssignment);

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var stored = await dbContext.ChoreAssignments.AsNoTracking().SingleAsync();
        var adultUser = await dbContext.Users.AsNoTracking().SingleAsync(user =>
            user.Email == "owner.assignment@example.test");

        Assert.Equal(adult.HouseholdId, stored.HouseholdId);
        Assert.NotEqual(otherAdult.HouseholdId, stored.HouseholdId);
        Assert.Equal(adultUser.Id, stored.AssignedByUserId);
        Assert.NotEqual("forged-user", stored.AssignedByUserId);
        Assert.Equal(chore.Id, stored.ChoreId);
        Assert.Equal(child.Id, stored.ChildId);
        Assert.Equal(created.Id, stored.Id);
        Assert.Equal(created.AssignedAt, stored.AssignedAt);
        Assert.Equal(DateOnly.FromDateTime(created.AssignedAt), created.DueDate);
        Assert.Equal(created.DueDate, stored.DueDate);
    }

    [Fact]
    public async Task Adult_can_choose_a_future_calendar_date_for_an_assignment()
    {
        using var adultClient = CreateClient();
        await RegisterAndLoginAdult(adultClient, "Familjen Datum", "due-date@example.test");
        var child = await CreateChild(adultClient, "Sam");
        var chore = await CreateChore(adultClient, "Packa väskan");
        var dueDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(2);

        var response = await adultClient.PostAsJsonAsync(
            "/api/chore-assignments",
            new CreateChoreAssignmentRequest
            {
                ChoreId = chore.Id,
                ChildId = child.Id,
                DueDate = dueDate
            });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<ChoreAssignmentResponse>();
        Assert.NotNull(created);
        Assert.Equal(dueDate, created.DueDate);
    }

    [Fact]
    public async Task Chore_and_child_must_belong_to_the_adults_household()
    {
        using var firstClient = CreateClient();
        using var secondClient = CreateClient();
        await RegisterAndLoginAdult(
            firstClient,
            "Familjen Dahl",
            "first.isolation.assignment@example.test");
        await RegisterAndLoginAdult(
            secondClient,
            "Familjen Ek",
            "second.isolation.assignment@example.test");
        var firstChild = await CreateChild(firstClient, "Nora");
        var secondChild = await CreateChild(secondClient, "Sam");
        var firstChore = await CreateChore(firstClient, "Städa rummet");
        var secondChore = await CreateChore(secondClient, "Ta ut soporna");

        Assert.Equal(
            HttpStatusCode.NotFound,
            (await AssignChore(firstClient, secondChore.Id, firstChild.Id)).StatusCode);
        Assert.Equal(
            HttpStatusCode.NotFound,
            (await AssignChore(firstClient, firstChore.Id, secondChild.Id)).StatusCode);
        Assert.Equal(
            HttpStatusCode.NotFound,
            (await AssignChore(firstClient, secondChore.Id, secondChild.Id)).StatusCode);

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Empty(await dbContext.ChoreAssignments.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task Inactive_child_cannot_receive_a_new_assignment()
    {
        using var adultClient = CreateClient();
        await RegisterAndLoginAdult(
            adultClient,
            "Familjen Fors",
            "inactive.assignment@example.test");
        var child = await CreateChild(adultClient, "Ella");
        var chore = await CreateChore(adultClient, "Duka bordet");
        Assert.Equal(
            HttpStatusCode.NoContent,
            (await adultClient.DeleteAsync($"/api/children/{child.Id}")).StatusCode);

        var response = await AssignChore(adultClient, chore.Id, child.Id);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Empty(await dbContext.ChoreAssignments.AsNoTracking().ToListAsync());
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task Missing_chore_or_child_returns_not_found(
        bool missingChore,
        bool missingChild)
    {
        using var adultClient = CreateClient();
        await RegisterAndLoginAdult(
            adultClient,
            "Familjen Gran",
            $"missing-{missingChore}-{missingChild}@example.test");
        var child = await CreateChild(adultClient, "Liam");
        var chore = await CreateChore(adultClient, "Bädda sängen");

        var response = await AssignChore(
            adultClient,
            missingChore ? int.MaxValue : chore.Id,
            missingChild ? int.MaxValue : child.Id);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Empty(await dbContext.ChoreAssignments.AsNoTracking().ToListAsync());
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 0)]
    [InlineData(-1, 1)]
    [InlineData(1, -1)]
    public async Task Invalid_identifier_values_are_rejected(int choreId, int childId)
    {
        using var adultClient = CreateClient();
        await RegisterAndLoginAdult(
            adultClient,
            "Familjen Holm",
            $"invalid-{choreId}-{childId}@example.test");

        var response = await AssignChore(adultClient, choreId, childId);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Empty(await dbContext.ChoreAssignments.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task Existing_adult_child_pairing_and_chore_flows_still_work()
    {
        using var adultClient = CreateClient();
        using var childClient = CreateClient();
        await RegisterAndLoginAdult(
            adultClient,
            "Familjen Isaksson",
            "regression.assignment@example.test");
        var child = await CreateChild(adultClient, "Alva");

        var updateResponse = await adultClient.PutAsJsonAsync(
            $"/api/children/{child.Id}",
            new UpdateChildRequest { Name = "Alva Ny" });
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        var children = await adultClient.GetFromJsonAsync<List<ChildResponse>>("/api/children");
        Assert.Collection(children!, item => Assert.Equal("Alva Ny", item.Name));

        await PairChild(adultClient, childClient, child.Id);
        var currentChild = await childClient.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.OK, currentChild.StatusCode);

        var chore = await CreateChore(adultClient, "Vattna blommorna");
        var chores = await adultClient.GetFromJsonAsync<List<ChoreResponse>>("/api/chores");
        Assert.Collection(chores!, item => Assert.Equal(chore.Id, item.Id));

        Assert.Equal(
            HttpStatusCode.Created,
            (await AssignChore(adultClient, chore.Id, child.Id)).StatusCode);
    }

    public void Dispose() => factory.Dispose();

    private HttpClient CreateClient() => factory.CreateClient(
        new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false,
            HandleCookies = true
        });

    private static Task<HttpResponseMessage> AssignChore(
        HttpClient client,
        int choreId,
        int childId) => client.PostAsJsonAsync(
            "/api/chore-assignments",
            new CreateChoreAssignmentRequest
            {
                ChoreId = choreId,
                ChildId = childId
            });

    private static async Task<ChoreResponse> CreateChore(HttpClient client, string title)
    {
        var response = await client.PostAsJsonAsync(
            "/api/chores",
            new CreateChoreRequest { Title = title });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ChoreResponse>())!;
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

        var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest { Email = email, Password = Password });
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        return registration;
    }
}
