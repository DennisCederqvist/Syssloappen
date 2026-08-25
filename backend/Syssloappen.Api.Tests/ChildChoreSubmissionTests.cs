using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Syssloappen.Api.Data;
using Syssloappen.Api.Dtos.Auth;
using Syssloappen.Api.Dtos.Children;
using Syssloappen.Api.Dtos.ChoreAssignments;
using Syssloappen.Api.Dtos.Chores;
using Syssloappen.Api.Models;
using Xunit;

namespace Syssloappen.Api.Tests;

public sealed class ChildChoreSubmissionTests : IDisposable
{
    private const string Password = "Password1";
    private readonly AuthApiFactory factory = new();

    [Fact]
    public async Task Only_authenticated_child_can_submit_an_assignment()
    {
        using var anonymousClient = CreateClient();
        using var adultClient = CreateClient();
        using var childClient = CreateClient();
        await RegisterAndLoginAdult(
            adultClient,
            "Familjen Andersson Submit",
            "access.child.submit@example.test");
        var child = await CreateChild(adultClient, "Maja");
        var chore = await CreateChore(adultClient, "Mata katten");
        var assignment = await AssignChore(adultClient, chore.Id, child.Id);
        await PairChild(adultClient, childClient, child.Id);

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await SubmitAssignment(anonymousClient, assignment.Id)).StatusCode);
        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await SubmitAssignment(adultClient, assignment.Id)).StatusCode);
        Assert.Equal(
            HttpStatusCode.OK,
            (await SubmitAssignment(childClient, assignment.Id)).StatusCode);
    }

    [Fact]
    public async Task Child_submits_own_assignment_with_backend_controlled_values_and_persistence()
    {
        using var adultClient = CreateClient();
        using var childClient = CreateClient();
        var registration = await RegisterAndLoginAdult(
            adultClient,
            "Familjen Berg Submit",
            "controlled.child.submit@example.test");
        var child = await CreateChild(adultClient, "Leo");
        var sibling = await CreateChild(adultClient, "Mira");
        var chore = await CreateChore(adultClient, "Töm diskmaskinen");
        var siblingChore = await CreateChore(adultClient, "Bädda sängen");
        var assignment = await AssignChore(adultClient, chore.Id, child.Id);
        var siblingAssignment = await AssignChore(adultClient, siblingChore.Id, sibling.Id);
        await PairChild(adultClient, childClient, child.Id);
        var forgedTime = DateTime.UtcNow.AddYears(-10);
        var beforeSubmission = DateTime.UtcNow;

        var response = await childClient.PostAsJsonAsync(
            $"/api/child/chore-assignments/{assignment.Id}/submit",
            new
            {
                Id = siblingAssignment.Id,
                AssignmentId = siblingAssignment.Id,
                ChoreId = siblingChore.Id,
                ChildId = sibling.Id,
                ChildProfileId = sibling.Id,
                HouseholdId = registration.HouseholdId + 1000,
                OwnerId = "forged-owner",
                AssignedByUserId = "forged-adult",
                Status = "Approved",
                SubmittedAt = forgedTime,
                ReviewedAt = forgedTime
            });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var submitted = await response.Content.ReadFromJsonAsync<SubmitChoreAssignmentResponse>();
        Assert.NotNull(submitted);
        Assert.Equal(assignment.Id, submitted.AssignmentId);
        Assert.Equal(nameof(ChoreAssignmentStatus.PendingApproval), submitted.Status);
        Assert.True(submitted.SubmittedAt >= beforeSubmission);
        Assert.NotEqual(forgedTime, submitted.SubmittedAt);

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var stored = await dbContext.ChoreAssignments
            .AsNoTracking()
            .SingleAsync(item => item.Id == assignment.Id);
        var untouchedSibling = await dbContext.ChoreAssignments
            .AsNoTracking()
            .SingleAsync(item => item.Id == siblingAssignment.Id);

        Assert.Equal(child.Id, stored.ChildId);
        Assert.Equal(registration.HouseholdId, stored.HouseholdId);
        Assert.Equal(chore.Id, stored.ChoreId);
        Assert.Equal(ChoreAssignmentStatus.PendingApproval, stored.Status);
        Assert.Equal(submitted.SubmittedAt, stored.SubmittedAt);
        Assert.Equal(ChoreAssignmentStatus.Assigned, untouchedSibling.Status);
        Assert.Null(untouchedSibling.SubmittedAt);
    }

    [Fact]
    public async Task Child_cannot_submit_siblings_or_other_households_assignments()
    {
        using var firstAdultClient = CreateClient();
        using var secondAdultClient = CreateClient();
        using var childClient = CreateClient();
        await RegisterAndLoginAdult(
            firstAdultClient,
            "Familjen Carlsson Submit",
            "isolation.child.submit@example.test");
        await RegisterAndLoginAdult(
            secondAdultClient,
            "Familjen Dahl Submit",
            "foreign.child.submit@example.test");
        var child = await CreateChild(firstAdultClient, "Nora");
        var sibling = await CreateChild(firstAdultClient, "Sam");
        var foreignChild = await CreateChild(secondAdultClient, "Ella");
        var siblingChore = await CreateChore(firstAdultClient, "Ta ut soporna");
        var foreignChore = await CreateChore(secondAdultClient, "Vattna blommorna");
        var siblingAssignment = await AssignChore(firstAdultClient, siblingChore.Id, sibling.Id);
        var foreignAssignment = await AssignChore(secondAdultClient, foreignChore.Id, foreignChild.Id);
        await PairChild(firstAdultClient, childClient, child.Id);

        Assert.Equal(
            HttpStatusCode.NotFound,
            (await SubmitAssignment(childClient, siblingAssignment.Id)).StatusCode);
        Assert.Equal(
            HttpStatusCode.NotFound,
            (await SubmitAssignment(childClient, foreignAssignment.Id)).StatusCode);

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.All(
            await dbContext.ChoreAssignments.AsNoTracking().ToListAsync(),
            item =>
            {
                Assert.Equal(ChoreAssignmentStatus.Assigned, item.Status);
                Assert.Null(item.SubmittedAt);
            });
    }

    [Fact]
    public async Task Inconsistent_cross_household_rows_cannot_be_submitted()
    {
        using var firstAdultClient = CreateClient();
        using var secondAdultClient = CreateClient();
        using var childClient = CreateClient();
        var firstRegistration = await RegisterAndLoginAdult(
            firstAdultClient,
            "Familjen Inconsistent One",
            "consistent.child.submit@example.test");
        var secondRegistration = await RegisterAndLoginAdult(
            secondAdultClient,
            "Familjen Inconsistent Two",
            "inconsistent.child.submit@example.test");
        var child = await CreateChild(firstAdultClient, "Vera");
        var ownChore = await CreateChore(firstAdultClient, "Mata fisken");
        var foreignChore = await CreateChore(secondAdultClient, "Främmande syssla");
        int foreignChoreAssignmentId;
        int foreignHouseholdAssignmentId;

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var firstAdult = await dbContext.Users.SingleAsync(user =>
                user.Email == "consistent.child.submit@example.test");
            var secondAdult = await dbContext.Users.SingleAsync(user =>
                user.Email == "inconsistent.child.submit@example.test");
            var foreignChoreAssignment = new ChoreAssignment
            {
                HouseholdId = firstRegistration.HouseholdId,
                ChoreId = foreignChore.Id,
                ChildId = child.Id,
                AssignedByUserId = firstAdult.Id,
                AssignedAt = DateTime.UtcNow
            };
            var foreignHouseholdAssignment = new ChoreAssignment
            {
                HouseholdId = secondRegistration.HouseholdId,
                ChoreId = ownChore.Id,
                ChildId = child.Id,
                AssignedByUserId = secondAdult.Id,
                AssignedAt = DateTime.UtcNow
            };
            dbContext.ChoreAssignments.AddRange(
                foreignChoreAssignment,
                foreignHouseholdAssignment);
            await dbContext.SaveChangesAsync();
            foreignChoreAssignmentId = foreignChoreAssignment.Id;
            foreignHouseholdAssignmentId = foreignHouseholdAssignment.Id;
        }

        await PairChild(firstAdultClient, childClient, child.Id);

        Assert.Equal(
            HttpStatusCode.NotFound,
            (await SubmitAssignment(childClient, foreignChoreAssignmentId)).StatusCode);
        Assert.Equal(
            HttpStatusCode.NotFound,
            (await SubmitAssignment(childClient, foreignHouseholdAssignmentId)).StatusCode);
        using var verificationScope = factory.Services.CreateScope();
        var verificationDb = verificationScope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.All(
            await verificationDb.ChoreAssignments.AsNoTracking().ToListAsync(),
            item =>
            {
                Assert.Equal(ChoreAssignmentStatus.Assigned, item.Status);
                Assert.Null(item.SubmittedAt);
            });
    }

    [Fact]
    public async Task Deactivated_child_cannot_submit_existing_assignment()
    {
        using var adultClient = CreateClient();
        using var childClient = CreateClient();
        await RegisterAndLoginAdult(
            adultClient,
            "Familjen Ek Submit",
            "inactive.child.submit@example.test");
        var child = await CreateChild(adultClient, "Alva");
        var chore = await CreateChore(adultClient, "Sortera tvätten");
        var assignment = await AssignChore(adultClient, chore.Id, child.Id);
        await PairChild(adultClient, childClient, child.Id);
        Assert.Equal(
            HttpStatusCode.NoContent,
            (await adultClient.DeleteAsync($"/api/children/{child.Id}")).StatusCode);

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await SubmitAssignment(childClient, assignment.Id)).StatusCode);

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var stored = await dbContext.ChoreAssignments.AsNoTracking().SingleAsync();
        Assert.Equal(ChoreAssignmentStatus.Assigned, stored.Status);
        Assert.Null(stored.SubmittedAt);
    }

    [Theory]
    [InlineData(0, HttpStatusCode.BadRequest)]
    [InlineData(-1, HttpStatusCode.BadRequest)]
    [InlineData(int.MaxValue, HttpStatusCode.NotFound)]
    public async Task Invalid_or_missing_assignment_is_rejected(
        int assignmentId,
        HttpStatusCode expectedStatus)
    {
        using var adultClient = CreateClient();
        using var childClient = CreateClient();
        await RegisterAndLoginAdult(
            adultClient,
            $"Familjen Missing Submit {assignmentId}",
            $"missing-{Math.Abs((long)assignmentId)}.child.submit@example.test");
        var child = await CreateChild(adultClient, "Iris");
        await PairChild(adultClient, childClient, child.Id);

        var response = await SubmitAssignment(childClient, assignmentId);

        Assert.Equal(expectedStatus, response.StatusCode);
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Empty(await dbContext.ChoreAssignments.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task Repeated_submission_returns_conflict_without_changing_report_time()
    {
        using var adultClient = CreateClient();
        using var childClient = CreateClient();
        await RegisterAndLoginAdult(
            adultClient,
            "Familjen Fors Submit",
            "repeat.child.submit@example.test");
        var child = await CreateChild(adultClient, "Axel");
        var chore = await CreateChore(adultClient, "Duka bordet");
        var assignment = await AssignChore(adultClient, chore.Id, child.Id);
        await PairChild(adultClient, childClient, child.Id);

        var firstResponse = await SubmitAssignment(childClient, assignment.Id);
        var firstResult = await firstResponse.Content.ReadFromJsonAsync<SubmitChoreAssignmentResponse>();
        var secondResponse = await SubmitAssignment(childClient, assignment.Id);

        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, secondResponse.StatusCode);
        Assert.NotNull(firstResult);
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var stored = await dbContext.ChoreAssignments.AsNoTracking().SingleAsync();
        Assert.Equal(ChoreAssignmentStatus.PendingApproval, stored.Status);
        Assert.Equal(firstResult.SubmittedAt, stored.SubmittedAt);
    }

    [Fact]
    public async Task Child_assignment_list_shows_assigned_and_pending_approval_statuses()
    {
        using var adultClient = CreateClient();
        using var childClient = CreateClient();
        await RegisterAndLoginAdult(
            adultClient,
            "Familjen Gran Submit",
            "listing.child.submit@example.test");
        var child = await CreateChild(adultClient, "Vera");
        var firstChore = await CreateChore(adultClient, "Städa rummet");
        var secondChore = await CreateChore(adultClient, "Packa skolväskan");
        var firstAssignment = await AssignChore(adultClient, firstChore.Id, child.Id);
        var secondAssignment = await AssignChore(adultClient, secondChore.Id, child.Id);
        await PairChild(adultClient, childClient, child.Id);

        var before = await childClient.GetFromJsonAsync<List<ChildChoreAssignmentResponse>>(
            "/api/child/chore-assignments");
        Assert.All(before!, item =>
        {
            Assert.Equal(nameof(ChoreAssignmentStatus.Assigned), item.Status);
            Assert.Null(item.SubmittedAt);
        });

        var submitResponse = await SubmitAssignment(childClient, firstAssignment.Id);
        Assert.Equal(HttpStatusCode.OK, submitResponse.StatusCode);
        var after = await childClient.GetFromJsonAsync<List<ChildChoreAssignmentResponse>>(
            "/api/child/chore-assignments");

        var submitted = Assert.Single(after!, item => item.AssignmentId == firstAssignment.Id);
        Assert.Equal(nameof(ChoreAssignmentStatus.PendingApproval), submitted.Status);
        Assert.NotNull(submitted.SubmittedAt);
        var stillAssigned = Assert.Single(after!, item => item.AssignmentId == secondAssignment.Id);
        Assert.Equal(nameof(ChoreAssignmentStatus.Assigned), stillAssigned.Status);
        Assert.Null(stillAssigned.SubmittedAt);
    }

    [Fact]
    public async Task Existing_adult_child_session_chore_and_assignment_flows_still_work()
    {
        using var adultClient = CreateClient();
        using var childClient = CreateClient();
        await RegisterAndLoginAdult(
            adultClient,
            "Familjen Holm Submit",
            "regression.child.submit@example.test");
        var child = await CreateChild(adultClient, "Liam");
        var updateResponse = await adultClient.PutAsJsonAsync(
            $"/api/children/{child.Id}",
            new UpdateChildRequest { Name = "Liam Ny" });
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        await PairChild(adultClient, childClient, child.Id);
        Assert.Equal(HttpStatusCode.OK, (await childClient.GetAsync("/api/auth/me")).StatusCode);
        var chore = await CreateChore(adultClient, "Vik tvätten");
        Assert.Contains(
            (await adultClient.GetFromJsonAsync<List<ChoreResponse>>("/api/chores"))!,
            item => item.Id == chore.Id);
        var assignment = await AssignChore(adultClient, chore.Id, child.Id);

        var ownAssignments = await childClient.GetFromJsonAsync<List<ChildChoreAssignmentResponse>>(
            "/api/child/chore-assignments");

        Assert.Contains(ownAssignments!, item => item.AssignmentId == assignment.Id);
    }

    public void Dispose() => factory.Dispose();

    private HttpClient CreateClient() => factory.CreateClient(
        new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false,
            HandleCookies = true
        });

    private static Task<HttpResponseMessage> SubmitAssignment(
        HttpClient client,
        int assignmentId) => client.PostAsync(
            $"/api/child/chore-assignments/{assignmentId}/submit",
            null);

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
        var registration = (await registerResponse.Content
            .ReadFromJsonAsync<RegisterAdultResponse>())!;
        var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest { Email = email, Password = Password });
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        return registration;
    }
}
