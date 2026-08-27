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

public sealed class ChoreAssignmentCancellationTests : IDisposable
{
    private const string Password = "Password1";
    private readonly AuthApiFactory factory = new();

    [Fact]
    public async Task Only_adult_can_cancel_an_assignment()
    {
        using var anonymousClient = CreateClient();
        using var adultClient = CreateClient();
        using var childClient = CreateClient();
        await RegisterAndLoginAdult(
            adultClient,
            "Familjen Cancel Role",
            "cancel.role@example.test");
        var child = await CreateChild(adultClient, "Maja");
        var chore = await CreateChore(adultClient, "Mata katten");
        var assignment = await AssignChore(adultClient, chore.Id, child.Id);
        await PairChild(adultClient, childClient, child.Id);

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await anonymousClient.DeleteAsync(
                $"/api/chore-assignments/{assignment.Id}")).StatusCode);
        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await childClient.DeleteAsync(
                $"/api/chore-assignments/{assignment.Id}")).StatusCode);
        Assert.Equal(
            HttpStatusCode.NoContent,
            (await adultClient.DeleteAsync(
                $"/api/chore-assignments/{assignment.Id}")).StatusCode);
    }

    [Fact]
    public async Task Adult_cancels_with_backend_derived_audit_and_history_is_not_deleted()
    {
        using var adultClient = CreateClient();
        var registration = await RegisterAndLoginAdult(
            adultClient,
            "Familjen Cancel Audit",
            "cancel.audit@example.test");
        var child = await CreateChild(adultClient, "Leo");
        var chore = await CreateChore(adultClient, "Töm diskmaskinen");
        var assignment = await AssignChore(adultClient, chore.Id, child.Id);
        var beforeCancel = DateTime.UtcNow;
        using var request = new HttpRequestMessage(
            HttpMethod.Delete,
            $"/api/chore-assignments/{assignment.Id}")
        {
            Content = JsonContent.Create(new
            {
                ChildId = int.MaxValue,
                HouseholdId = registration.HouseholdId + 1,
                Status = nameof(ChoreAssignmentStatus.Approved),
                CancelledByUserId = "forged-user",
                CancelledAt = DateTime.UtcNow.AddYears(-10)
            })
        };

        var response = await adultClient.SendAsync(request);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var stored = await dbContext.ChoreAssignments.AsNoTracking().SingleAsync();
        var adult = await dbContext.Users.AsNoTracking().SingleAsync(user =>
            user.Email == "cancel.audit@example.test");
        Assert.Equal(ChoreAssignmentStatus.Cancelled, stored.Status);
        Assert.Equal(adult.Id, stored.CancelledByUserId);
        Assert.NotEqual("forged-user", stored.CancelledByUserId);
        Assert.True(stored.CancelledAt >= beforeCancel);
        Assert.Equal(registration.HouseholdId, stored.HouseholdId);
        Assert.Equal(child.Id, stored.ChildId);
        Assert.Equal(chore.Id, stored.ChoreId);
        Assert.Empty(await dbContext.ChoreCompletions.AsNoTracking().ToListAsync());

        Assert.Equal(
            HttpStatusCode.Conflict,
            (await adultClient.DeleteAsync(
                $"/api/chore-assignments/{assignment.Id}")).StatusCode);
        Assert.Equal(
            HttpStatusCode.BadRequest,
            (await adultClient.DeleteAsync("/api/chore-assignments/0")).StatusCode);
        Assert.Equal(
            HttpStatusCode.NotFound,
            (await adultClient.DeleteAsync(
                $"/api/chore-assignments/{int.MaxValue}")).StatusCode);
    }

    [Fact]
    public async Task Manipulated_assignment_id_cannot_cancel_another_households_assignment()
    {
        using var firstClient = CreateClient();
        using var secondClient = CreateClient();
        await RegisterAndLoginAdult(
            firstClient,
            "Familjen Cancel One",
            "cancel.one@example.test");
        await RegisterAndLoginAdult(
            secondClient,
            "Familjen Cancel Two",
            "cancel.two@example.test");
        var child = await CreateChild(secondClient, "Nora");
        var chore = await CreateChore(secondClient, "Bädda sängen");
        var assignment = await AssignChore(secondClient, chore.Id, child.Id);

        Assert.Equal(
            HttpStatusCode.NotFound,
            (await firstClient.DeleteAsync(
                $"/api/chore-assignments/{assignment.Id}")).StatusCode);

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var stored = await dbContext.ChoreAssignments.AsNoTracking().SingleAsync();
        Assert.Equal(ChoreAssignmentStatus.Assigned, stored.Status);
        Assert.Null(stored.CancelledByUserId);
        Assert.Null(stored.CancelledAt);
    }

    [Fact]
    public async Task Cancelled_assignment_is_hidden_from_current_lists_and_child_submit()
    {
        using var adultClient = CreateClient();
        using var childClient = CreateClient();
        await RegisterAndLoginAdult(
            adultClient,
            "Familjen Cancel Visibility",
            "cancel.visibility@example.test");
        var child = await CreateChild(adultClient, "Ella");
        var chore = await CreateChore(adultClient, "Duka bordet");
        var assignment = await AssignChore(adultClient, chore.Id, child.Id);
        await PairChild(adultClient, childClient, child.Id);

        Assert.Single(await GetChildAssignments(childClient));
        Assert.Equal(
            HttpStatusCode.NoContent,
            (await adultClient.DeleteAsync(
                $"/api/chore-assignments/{assignment.Id}")).StatusCode);

        Assert.Empty((await adultClient.GetFromJsonAsync<List<AdultChoreAssignmentResponse>>(
            "/api/chore-assignments"))!);
        Assert.Empty(await GetChildAssignments(childClient));
        Assert.Equal(
            HttpStatusCode.NotFound,
            (await childClient.PostAsync(
                $"/api/child/chore-assignments/{assignment.Id}/submit",
                null)).StatusCode);
        var history = await adultClient.GetFromJsonAsync<List<AdultChoreAssignmentResponse>>(
            "/api/chore-assignments?includeCancelled=true");
        var cancelled = Assert.Single(history!);
        Assert.Equal(nameof(ChoreAssignmentStatus.Cancelled), cancelled.Status);
        Assert.NotNull(cancelled.CancelledByUserId);
        Assert.NotNull(cancelled.CancelledAt);
    }

    [Fact]
    public async Task Pending_and_needs_redo_assignments_can_be_cancelled()
    {
        using var adultClient = CreateClient();
        using var childClient = CreateClient();
        await RegisterAndLoginAdult(
            adultClient,
            "Familjen Cancel States",
            "cancel.states@example.test");
        var child = await CreateChild(adultClient, "Alva");
        var chore = await CreateChore(adultClient, "Vattna blommorna");
        var pendingAssignment = await AssignChore(adultClient, chore.Id, child.Id);
        var needsRedoAssignment = await AssignChore(adultClient, chore.Id, child.Id);
        await PairChild(adultClient, childClient, child.Id);
        await SubmitAssignment(childClient, pendingAssignment.Id);
        await SubmitAssignment(childClient, needsRedoAssignment.Id);
        Assert.Equal(
            HttpStatusCode.OK,
            (await adultClient.PostAsJsonAsync(
                $"/api/chore-assignments/{needsRedoAssignment.Id}/reject",
                new ReviewChoreAssignmentRequest { Comment = "Försök igen" })).StatusCode);

        Assert.Equal(
            HttpStatusCode.NoContent,
            (await adultClient.DeleteAsync(
                $"/api/chore-assignments/{pendingAssignment.Id}")).StatusCode);
        Assert.Equal(
            HttpStatusCode.NoContent,
            (await adultClient.DeleteAsync(
                $"/api/chore-assignments/{needsRedoAssignment.Id}")).StatusCode);

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var stored = await dbContext.ChoreAssignments.AsNoTracking().ToListAsync();
        Assert.All(stored, item => Assert.Equal(ChoreAssignmentStatus.Cancelled, item.Status));
        Assert.Contains(stored, item => item.ReviewComment == "Försök igen");
    }

    [Fact]
    public async Task Approved_assignment_cannot_be_cancelled_and_points_are_preserved()
    {
        using var adultClient = CreateClient();
        using var childClient = CreateClient();
        await RegisterAndLoginAdult(
            adultClient,
            "Familjen Cancel Approved",
            "cancel.approved@example.test");
        var child = await CreateChild(adultClient, "Iris");
        var chore = await CreateChore(adultClient, "Packa skolväskan", 20);
        var assignment = await AssignChore(adultClient, chore.Id, child.Id);
        await PairChild(adultClient, childClient, child.Id);
        await SubmitAssignment(childClient, assignment.Id);
        Assert.Equal(
            HttpStatusCode.OK,
            (await adultClient.PostAsJsonAsync(
                $"/api/chore-assignments/{assignment.Id}/approve",
                new ReviewChoreAssignmentRequest())).StatusCode);

        Assert.Equal(
            HttpStatusCode.Conflict,
            (await adultClient.DeleteAsync(
                $"/api/chore-assignments/{assignment.Id}")).StatusCode);
        var points = await childClient.GetFromJsonAsync<ChildPointsResponse>("/api/child/points");
        Assert.Equal(20, points!.TotalPoints);

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var stored = await dbContext.ChoreAssignments.AsNoTracking().SingleAsync();
        var completion = await dbContext.ChoreCompletions.AsNoTracking().SingleAsync();
        Assert.Equal(ChoreAssignmentStatus.Approved, stored.Status);
        Assert.Null(stored.CancelledByUserId);
        Assert.Null(stored.CancelledAt);
        Assert.Equal(20, completion.PointsAwarded);
    }

    [Fact]
    public async Task Adult_can_archive_and_restore_only_final_assignment_history()
    {
        using var adultClient = CreateClient();
        using var childClient = CreateClient();
        await RegisterAndLoginAdult(
            adultClient,
            "Familjen Archive Chore",
            "archive.chore@example.test");
        var child = await CreateChild(adultClient, "Albin");
        var chore = await CreateChore(adultClient, "Sortera tvätt");
        var assignment = await AssignChore(adultClient, chore.Id, child.Id);

        Assert.Equal(
            HttpStatusCode.Conflict,
            (await adultClient.PostAsync($"/api/chore-assignments/{assignment.Id}/archive", null)).StatusCode);

        await PairChild(adultClient, childClient, child.Id);
        await SubmitAssignment(childClient, assignment.Id);
        Assert.Equal(
            HttpStatusCode.OK,
            (await adultClient.PostAsJsonAsync(
                $"/api/chore-assignments/{assignment.Id}/approve",
                new ReviewChoreAssignmentRequest())).StatusCode);

        Assert.Equal(
            HttpStatusCode.NoContent,
            (await adultClient.PostAsync($"/api/chore-assignments/{assignment.Id}/archive", null)).StatusCode);
        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.NotNull((await dbContext.ChoreAssignments.SingleAsync()).AdultArchivedAt);
        }

        Assert.Equal(
            HttpStatusCode.NoContent,
            (await adultClient.PostAsync($"/api/chore-assignments/{assignment.Id}/restore", null)).StatusCode);
        using var restoredScope = factory.Services.CreateScope();
        var restoredDbContext = restoredScope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Null((await restoredDbContext.ChoreAssignments.SingleAsync()).AdultArchivedAt);
    }

    public void Dispose() => factory.Dispose();

    private HttpClient CreateClient() => factory.CreateClient(
        new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false,
            HandleCookies = true
        });

    private static async Task<List<ChildChoreAssignmentResponse>> GetChildAssignments(
        HttpClient childClient) =>
        (await childClient.GetFromJsonAsync<List<ChildChoreAssignmentResponse>>(
            "/api/child/chore-assignments"))!;

    private static async Task SubmitAssignment(HttpClient childClient, int assignmentId)
    {
        var response = await childClient.PostAsync(
            $"/api/child/chore-assignments/{assignmentId}/submit",
            null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
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

    private static async Task<ChoreResponse> CreateChore(
        HttpClient client,
        string title,
        int points = 5)
    {
        var response = await client.PostAsJsonAsync(
            "/api/chores",
            new CreateChoreRequest { Title = title, Points = points });
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
