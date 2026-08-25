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

public sealed class ChildChoreAssignmentsTests : IDisposable
{
    private const string Password = "Password1";
    private readonly AuthApiFactory factory = new();

    [Fact]
    public async Task Only_authenticated_child_can_read_child_assignments()
    {
        using var anonymousClient = CreateClient();
        using var adultClient = CreateClient();
        using var childClient = CreateClient();
        await RegisterAndLoginAdult(
            adultClient,
            "Familjen Andersson",
            "access.child.assignments@example.test");
        var child = await CreateChild(adultClient, "Maja");
        await PairChild(adultClient, childClient, child.Id);

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await anonymousClient.GetAsync("/api/child/chore-assignments")).StatusCode);
        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await adultClient.GetAsync("/api/child/chore-assignments")).StatusCode);
        var childResponse = await childClient.GetAsync("/api/child/chore-assignments");
        Assert.Equal(HttpStatusCode.OK, childResponse.StatusCode);
        Assert.Empty((await childResponse.Content.ReadFromJsonAsync<List<ChildChoreAssignmentResponse>>())!);
        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await childClient.GetAsync("/api/children")).StatusCode);
    }

    [Fact]
    public async Task Child_reads_own_assignment_details_in_newest_first_order()
    {
        using var adultClient = CreateClient();
        using var childClient = CreateClient();
        await RegisterAndLoginAdult(
            adultClient,
            "Familjen Berg",
            "details.child.assignments@example.test");
        var child = await CreateChild(adultClient, "Leo");
        var firstChore = await CreateChore(adultClient, "Mata katten", "Före frukost");
        var secondChore = await CreateChore(adultClient, "Töm diskmaskinen", null);
        var firstAssignment = await AssignChore(adultClient, firstChore.Id, child.Id);
        var secondAssignment = await AssignChore(adultClient, secondChore.Id, child.Id);
        await PairChild(adultClient, childClient, child.Id);

        var assignments = await childClient.GetFromJsonAsync<List<ChildChoreAssignmentResponse>>(
            "/api/child/chore-assignments");

        Assert.Collection(
            assignments!,
            assignment =>
            {
                Assert.Equal(secondAssignment.Id, assignment.AssignmentId);
                Assert.Equal(secondChore.Id, assignment.ChoreId);
                Assert.Equal("Töm diskmaskinen", assignment.Title);
                Assert.Null(assignment.Description);
                Assert.Equal(secondAssignment.AssignedAt, assignment.AssignedAt);
            },
            assignment =>
            {
                Assert.Equal(firstAssignment.Id, assignment.AssignmentId);
                Assert.Equal(firstChore.Id, assignment.ChoreId);
                Assert.Equal("Mata katten", assignment.Title);
                Assert.Equal("Före frukost", assignment.Description);
                Assert.Equal(firstAssignment.AssignedAt, assignment.AssignedAt);
            });
    }

    [Fact]
    public async Task Child_does_not_see_siblings_assignments()
    {
        using var adultClient = CreateClient();
        using var firstChildClient = CreateClient();
        await RegisterAndLoginAdult(
            adultClient,
            "Familjen Carlsson",
            "siblings.child.assignments@example.test");
        var firstChild = await CreateChild(adultClient, "Nora");
        var sibling = await CreateChild(adultClient, "Sam");
        var ownChore = await CreateChore(adultClient, "Bädda sängen", null);
        var siblingChore = await CreateChore(adultClient, "Ta ut soporna", null);
        var ownAssignment = await AssignChore(adultClient, ownChore.Id, firstChild.Id);
        await AssignChore(adultClient, siblingChore.Id, sibling.Id);
        await PairChild(adultClient, firstChildClient, firstChild.Id);

        var assignments = await firstChildClient.GetFromJsonAsync<List<ChildChoreAssignmentResponse>>(
            "/api/child/chore-assignments");

        Assert.Collection(
            assignments!,
            assignment => Assert.Equal(ownAssignment.Id, assignment.AssignmentId));
    }

    [Fact]
    public async Task Children_only_see_assignments_from_their_own_household()
    {
        using var firstAdultClient = CreateClient();
        using var secondAdultClient = CreateClient();
        using var firstChildClient = CreateClient();
        using var secondChildClient = CreateClient();
        await RegisterAndLoginAdult(
            firstAdultClient,
            "Familjen Dahl",
            "first.household.child.assignments@example.test");
        await RegisterAndLoginAdult(
            secondAdultClient,
            "Familjen Ek",
            "second.household.child.assignments@example.test");
        var firstChild = await CreateChild(firstAdultClient, "Ella");
        var secondChild = await CreateChild(secondAdultClient, "Alva");
        var firstChore = await CreateChore(firstAdultClient, "Vattna blommorna", null);
        var secondChore = await CreateChore(secondAdultClient, "Städa rummet", null);
        var firstAssignment = await AssignChore(firstAdultClient, firstChore.Id, firstChild.Id);
        var secondAssignment = await AssignChore(secondAdultClient, secondChore.Id, secondChild.Id);
        await PairChild(firstAdultClient, firstChildClient, firstChild.Id);
        await PairChild(secondAdultClient, secondChildClient, secondChild.Id);

        var firstAssignments = await firstChildClient.GetFromJsonAsync<List<ChildChoreAssignmentResponse>>(
            "/api/child/chore-assignments");
        var secondAssignments = await secondChildClient.GetFromJsonAsync<List<ChildChoreAssignmentResponse>>(
            "/api/child/chore-assignments");

        Assert.Collection(
            firstAssignments!,
            assignment => Assert.Equal(firstAssignment.Id, assignment.AssignmentId));
        Assert.Collection(
            secondAssignments!,
            assignment => Assert.Equal(secondAssignment.Id, assignment.AssignmentId));
    }

    [Fact]
    public async Task Query_parameters_cannot_select_a_sibling_or_household()
    {
        using var adultClient = CreateClient();
        using var childClient = CreateClient();
        var registration = await RegisterAndLoginAdult(
            adultClient,
            "Familjen Fors",
            "query.child.assignments@example.test");
        var child = await CreateChild(adultClient, "Liam");
        var sibling = await CreateChild(adultClient, "Olivia");
        var ownChore = await CreateChore(adultClient, "Plocka undan", null);
        var siblingChore = await CreateChore(adultClient, "Duka bordet", null);
        var ownAssignment = await AssignChore(adultClient, ownChore.Id, child.Id);
        await AssignChore(adultClient, siblingChore.Id, sibling.Id);
        await PairChild(adultClient, childClient, child.Id);

        var assignments = await childClient.GetFromJsonAsync<List<ChildChoreAssignmentResponse>>(
            $"/api/child/chore-assignments?childId={sibling.Id}&householdId={registration.HouseholdId + 1}");

        Assert.Collection(
            assignments!,
            assignment => Assert.Equal(ownAssignment.Id, assignment.AssignmentId));
    }

    [Fact]
    public async Task Inconsistent_cross_household_rows_are_excluded_from_child_view()
    {
        using var firstAdultClient = CreateClient();
        using var secondAdultClient = CreateClient();
        using var childClient = CreateClient();
        var firstRegistration = await RegisterAndLoginAdult(
            firstAdultClient,
            "Familjen Gran",
            "consistent.child.assignments@example.test");
        var secondRegistration = await RegisterAndLoginAdult(
            secondAdultClient,
            "Familjen Holm",
            "inconsistent.child.assignments@example.test");
        var child = await CreateChild(firstAdultClient, "Vera");
        var ownChore = await CreateChore(firstAdultClient, "Mata fisken", null);
        var foreignChore = await CreateChore(secondAdultClient, "Främmande syssla", null);
        var ownAssignment = await AssignChore(firstAdultClient, ownChore.Id, child.Id);

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var firstAdult = await dbContext.Users.SingleAsync(user =>
                user.Email == "consistent.child.assignments@example.test");
            var secondAdult = await dbContext.Users.SingleAsync(user =>
                user.Email == "inconsistent.child.assignments@example.test");
            dbContext.ChoreAssignments.AddRange(
                new ChoreAssignment
                {
                    HouseholdId = firstRegistration.HouseholdId,
                    ChoreId = foreignChore.Id,
                    ChildId = child.Id,
                    AssignedByUserId = firstAdult.Id,
                    AssignedAt = DateTime.UtcNow.AddMinutes(1)
                },
                new ChoreAssignment
                {
                    HouseholdId = secondRegistration.HouseholdId,
                    ChoreId = ownChore.Id,
                    ChildId = child.Id,
                    AssignedByUserId = secondAdult.Id,
                    AssignedAt = DateTime.UtcNow.AddMinutes(2)
                });
            await dbContext.SaveChangesAsync();
        }

        await PairChild(firstAdultClient, childClient, child.Id);
        var assignments = await childClient.GetFromJsonAsync<List<ChildChoreAssignmentResponse>>(
            "/api/child/chore-assignments");

        Assert.Collection(
            assignments!,
            assignment => Assert.Equal(ownAssignment.Id, assignment.AssignmentId));
    }

    [Fact]
    public async Task Deactivated_child_is_denied_but_historical_assignment_remains()
    {
        using var adultClient = CreateClient();
        using var childClient = CreateClient();
        await RegisterAndLoginAdult(
            adultClient,
            "Familjen Isaksson",
            "inactive.child.assignments@example.test");
        var child = await CreateChild(adultClient, "Axel");
        var chore = await CreateChore(adultClient, "Sortera tvätten", null);
        var assignment = await AssignChore(adultClient, chore.Id, child.Id);
        await PairChild(adultClient, childClient, child.Id);
        Assert.Equal(
            HttpStatusCode.NoContent,
            (await adultClient.DeleteAsync($"/api/children/{child.Id}")).StatusCode);

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await childClient.GetAsync("/api/child/chore-assignments")).StatusCode);
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.True(await dbContext.ChoreAssignments.AnyAsync(item => item.Id == assignment.Id));
    }

    [Fact]
    public async Task Fallback_logged_in_child_reads_the_same_own_assignments()
    {
        using var adultClient = CreateClient();
        using var childClient = CreateClient();
        var registration = await RegisterAndLoginAdult(
            adultClient,
            "Familjen Jansson",
            "fallback.child.assignments@example.test");
        var childUserName = $"fallback-child-{Guid.NewGuid():N}";
        var child = await CreateChild(adultClient, "Iris", childUserName);
        var chore = await CreateChore(adultClient, "Packa skolväskan", null);
        var assignment = await AssignChore(adultClient, chore.Id, child.Id);

        var loginResponse = await childClient.PostAsJsonAsync(
            "/api/auth/child/login",
            new ChildFallbackLoginRequest
            {
                FamilyCode = registration.FamilyCode,
                UserName = childUserName,
                Password = Password
            });
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var assignments = await childClient.GetFromJsonAsync<List<ChildChoreAssignmentResponse>>(
            "/api/child/chore-assignments");

        Assert.Collection(
            assignments!,
            item => Assert.Equal(assignment.Id, item.AssignmentId));
    }

    public void Dispose() => factory.Dispose();

    private HttpClient CreateClient() => factory.CreateClient(
        new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false,
            HandleCookies = true
        });

    private static async Task<ChoreAssignmentResponse> AssignChore(
        HttpClient client,
        int choreId,
        int childId)
    {
        var response = await client.PostAsJsonAsync(
            "/api/chore-assignments",
            new CreateChoreAssignmentRequest
            {
                ChoreId = choreId,
                ChildId = childId
            });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ChoreAssignmentResponse>())!;
    }

    private static async Task<ChoreResponse> CreateChore(
        HttpClient client,
        string title,
        string? description)
    {
        var response = await client.PostAsJsonAsync(
            "/api/chores",
            new CreateChoreRequest { Title = title, Description = description });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ChoreResponse>())!;
    }

    private static async Task<CreateChildResponse> CreateChild(
        HttpClient client,
        string name,
        string? userName = null)
    {
        var response = await client.PostAsJsonAsync(
            "/api/children",
            new CreateChildRequest
            {
                Name = name,
                UserName = userName ?? $"child-{Guid.NewGuid():N}",
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
