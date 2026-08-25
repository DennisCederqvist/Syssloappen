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

public sealed class AdultChoreReviewAndPointsTests : IDisposable
{
    private const string Password = "Password1";
    private readonly AuthApiFactory factory = new();

    [Fact]
    public async Task Adult_selects_allowed_points_with_five_as_default()
    {
        using var adultClient = CreateClient();
        await RegisterAndLoginAdult(
            adultClient,
            "Familjen Points",
            "chore.points@example.test");

        var defaultChore = await CreateChore(adultClient, "Default points", null);
        var five = await CreateChore(adultClient, "Five points", 5);
        var ten = await CreateChore(adultClient, "Ten points", 10);
        var fifteen = await CreateChore(adultClient, "Fifteen points", 15);
        var twenty = await CreateChore(adultClient, "Twenty points", 20);

        Assert.Equal(5, defaultChore.Points);
        Assert.Equal(5, five.Points);
        Assert.Equal(10, ten.Points);
        Assert.Equal(15, fifteen.Points);
        Assert.Equal(20, twenty.Points);
        foreach (var invalidPoints in new[] { -5, 0, 6, 25 })
        {
            var response = await adultClient.PostAsJsonAsync(
                "/api/chores",
                new { Title = $"Invalid {invalidPoints}", Points = invalidPoints });
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(5, await dbContext.Chores.CountAsync());
    }

    [Fact]
    public async Task Assignment_snapshots_points_and_ignores_forged_ownership_fields()
    {
        using var adultClient = CreateClient();
        var registration = await RegisterAndLoginAdult(
            adultClient,
            "Familjen Snapshot",
            "snapshot.points@example.test");
        var child = await CreateChild(adultClient, "Maja");
        var chore = await CreateChore(adultClient, "Töm diskmaskinen", 20);

        var response = await adultClient.PostAsJsonAsync(
            "/api/chore-assignments",
            new
            {
                ChoreId = chore.Id,
                ChildId = child.Id,
                Points = 5,
                PointsAwarded = 5,
                HouseholdId = registration.HouseholdId + 1,
                Status = "Approved"
            });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var assignment = await response.Content.ReadFromJsonAsync<ChoreAssignmentResponse>();
        Assert.NotNull(assignment);
        Assert.Equal(20, assignment.Points);
        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var storedChore = await dbContext.Chores.SingleAsync(item => item.Id == chore.Id);
            storedChore.Points = 5;
            await dbContext.SaveChangesAsync();
        }

        using var verificationScope = factory.Services.CreateScope();
        var verificationDb = verificationScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var storedAssignment = await verificationDb.ChoreAssignments.AsNoTracking().SingleAsync();
        Assert.Equal(20, storedAssignment.Points);
        Assert.Equal(registration.HouseholdId, storedAssignment.HouseholdId);
        Assert.Equal(ChoreAssignmentStatus.Assigned, storedAssignment.Status);
    }

    [Fact]
    public async Task Approval_creates_one_completion_and_awards_backend_controlled_points()
    {
        using var adultClient = CreateClient();
        using var childClient = CreateClient();
        var registration = await RegisterAndLoginAdult(
            adultClient,
            "Familjen Approval",
            "approve.points@example.test");
        var child = await CreateChild(adultClient, "Leo");
        var chore = await CreateChore(adultClient, "Städa rummet", 20);
        var assignment = await AssignChore(adultClient, chore.Id, child.Id);
        await PairChild(adultClient, childClient, child.Id);
        Assert.Equal(0, (await GetPoints(childClient)).TotalPoints);
        await SubmitAssignment(childClient, assignment.Id);
        Assert.Equal(0, (await GetPoints(childClient)).TotalPoints);
        var beforeReview = DateTime.UtcNow;

        var response = await adultClient.PostAsJsonAsync(
            $"/api/chore-assignments/{assignment.Id}/approve",
            new
            {
                Comment = "Bra jobbat",
                Points = 5,
                PointsAwarded = 5,
                ChildId = int.MaxValue,
                HouseholdId = registration.HouseholdId + 1,
                ApprovedByUserId = "forged",
                ReviewedAt = DateTime.UtcNow.AddYears(-10),
                Status = "NeedsRedo"
            });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var reviewed = await response.Content.ReadFromJsonAsync<ReviewChoreAssignmentResponse>();
        Assert.NotNull(reviewed);
        Assert.Equal(nameof(ChoreAssignmentStatus.Approved), reviewed.Status);
        Assert.Equal(20, reviewed.PointsAwarded);
        Assert.Equal("Bra jobbat", reviewed.ReviewComment);
        Assert.True(reviewed.ReviewedAt >= beforeReview);
        Assert.Equal(20, (await GetPoints(childClient)).TotalPoints);

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var storedAssignment = await dbContext.ChoreAssignments.AsNoTracking().SingleAsync();
        var completion = await dbContext.ChoreCompletions.AsNoTracking().SingleAsync();
        var adult = await dbContext.Users.AsNoTracking().SingleAsync(user =>
            user.Email == "approve.points@example.test");
        Assert.Equal(ChoreAssignmentStatus.Approved, storedAssignment.Status);
        Assert.Equal(adult.Id, storedAssignment.ReviewedByUserId);
        Assert.Equal(reviewed.ReviewedAt, storedAssignment.ReviewedAt);
        Assert.Equal(registration.HouseholdId, completion.HouseholdId);
        Assert.Equal(assignment.Id, completion.AssignmentId);
        Assert.Equal(child.Id, completion.ChildId);
        Assert.Equal(chore.Id, completion.ChoreId);
        Assert.Equal(adult.Id, completion.ApprovedByUserId);
        Assert.Equal(20, completion.PointsAwarded);
        Assert.Equal(reviewed.ReviewedAt, completion.ApprovedAt);
    }

    [Fact]
    public async Task Rejection_awards_no_points_and_child_can_resubmit()
    {
        using var adultClient = CreateClient();
        using var childClient = CreateClient();
        await RegisterAndLoginAdult(
            adultClient,
            "Familjen Redo",
            "redo.points@example.test");
        var child = await CreateChild(adultClient, "Nora");
        var chore = await CreateChore(adultClient, "Bädda sängen", 15);
        var assignment = await AssignChore(adultClient, chore.Id, child.Id);
        await PairChild(adultClient, childClient, child.Id);
        await SubmitAssignment(childClient, assignment.Id);

        var rejectResponse = await adultClient.PostAsJsonAsync(
            $"/api/chore-assignments/{assignment.Id}/reject",
            new ReviewChoreAssignmentRequest { Comment = "Gör om hörnen" });

        Assert.Equal(HttpStatusCode.OK, rejectResponse.StatusCode);
        var rejected = await rejectResponse.Content.ReadFromJsonAsync<ReviewChoreAssignmentResponse>();
        Assert.NotNull(rejected);
        Assert.Equal(nameof(ChoreAssignmentStatus.NeedsRedo), rejected.Status);
        Assert.Null(rejected.PointsAwarded);
        Assert.Equal(0, (await GetPoints(childClient)).TotalPoints);
        var childList = await GetChildAssignments(childClient);
        Assert.Equal(nameof(ChoreAssignmentStatus.NeedsRedo), Assert.Single(childList).Status);
        Assert.Equal("Gör om hörnen", Assert.Single(childList).ReviewComment);

        var resubmitResponse = await childClient.PostAsync(
            $"/api/child/chore-assignments/{assignment.Id}/submit",
            null);

        Assert.Equal(HttpStatusCode.OK, resubmitResponse.StatusCode);
        childList = await GetChildAssignments(childClient);
        Assert.Equal(nameof(ChoreAssignmentStatus.PendingApproval), Assert.Single(childList).Status);
        Assert.Null(Assert.Single(childList).ReviewComment);
        var approveResponse = await adultClient.PostAsJsonAsync(
            $"/api/chore-assignments/{assignment.Id}/approve",
            new ReviewChoreAssignmentRequest());
        Assert.Equal(HttpStatusCode.OK, approveResponse.StatusCode);
        Assert.Equal(15, (await GetPoints(childClient)).TotalPoints);
    }

    [Fact]
    public async Task Adult_list_and_review_are_role_and_household_isolated()
    {
        using var anonymousClient = CreateClient();
        using var firstAdultClient = CreateClient();
        using var secondAdultClient = CreateClient();
        using var childClient = CreateClient();
        await RegisterAndLoginAdult(
            firstAdultClient,
            "Familjen Isolation One",
            "review.isolation.one@example.test");
        await RegisterAndLoginAdult(
            secondAdultClient,
            "Familjen Isolation Two",
            "review.isolation.two@example.test");
        var firstChild = await CreateChild(firstAdultClient, "Ella");
        var secondChild = await CreateChild(secondAdultClient, "Alva");
        var firstChore = await CreateChore(firstAdultClient, "Vattna blommorna", 10);
        var secondChore = await CreateChore(secondAdultClient, "Ta ut soporna", 20);
        var firstAssignment = await AssignChore(firstAdultClient, firstChore.Id, firstChild.Id);
        var secondAssignment = await AssignChore(secondAdultClient, secondChore.Id, secondChild.Id);
        await PairChild(firstAdultClient, childClient, firstChild.Id);
        await SubmitAssignment(childClient, firstAssignment.Id);

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await anonymousClient.GetAsync("/api/chore-assignments")).StatusCode);
        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await childClient.GetAsync("/api/chore-assignments")).StatusCode);
        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await childClient.PostAsJsonAsync(
                $"/api/chore-assignments/{firstAssignment.Id}/approve",
                new ReviewChoreAssignmentRequest())).StatusCode);
        var firstList = await firstAdultClient.GetFromJsonAsync<List<AdultChoreAssignmentResponse>>(
            "/api/chore-assignments");
        Assert.Collection(firstList!, item =>
        {
            Assert.Equal(firstAssignment.Id, item.AssignmentId);
            Assert.Equal(nameof(ChoreAssignmentStatus.PendingApproval), item.Status);
            Assert.Equal(10, item.Points);
        });
        Assert.Equal(
            HttpStatusCode.NotFound,
            (await firstAdultClient.PostAsJsonAsync(
                $"/api/chore-assignments/{secondAssignment.Id}/approve",
                new ReviewChoreAssignmentRequest())).StatusCode);

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Empty(await dbContext.ChoreCompletions.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task Invalid_missing_unsubmitted_and_repeated_reviews_are_safe()
    {
        using var adultClient = CreateClient();
        using var childClient = CreateClient();
        await RegisterAndLoginAdult(
            adultClient,
            "Familjen Safe Review",
            "safe.review@example.test");
        var child = await CreateChild(adultClient, "Iris");
        var chore = await CreateChore(adultClient, "Packa skolväskan", 5);
        var assignment = await AssignChore(adultClient, chore.Id, child.Id);
        await PairChild(adultClient, childClient, child.Id);

        Assert.Equal(
            HttpStatusCode.BadRequest,
            (await adultClient.PostAsJsonAsync(
                "/api/chore-assignments/0/approve",
                new ReviewChoreAssignmentRequest())).StatusCode);
        Assert.Equal(
            HttpStatusCode.NotFound,
            (await adultClient.PostAsJsonAsync(
                $"/api/chore-assignments/{int.MaxValue}/approve",
                new ReviewChoreAssignmentRequest())).StatusCode);
        Assert.Equal(
            HttpStatusCode.Conflict,
            (await adultClient.PostAsJsonAsync(
                $"/api/chore-assignments/{assignment.Id}/approve",
                new ReviewChoreAssignmentRequest())).StatusCode);
        await SubmitAssignment(childClient, assignment.Id);
        Assert.Equal(
            HttpStatusCode.BadRequest,
            (await adultClient.PostAsJsonAsync(
                $"/api/chore-assignments/{assignment.Id}/approve",
                new ReviewChoreAssignmentRequest { Comment = new string('x', 501) })).StatusCode);
        Assert.Equal(
            HttpStatusCode.OK,
            (await adultClient.PostAsJsonAsync(
                $"/api/chore-assignments/{assignment.Id}/approve",
                new ReviewChoreAssignmentRequest())).StatusCode);
        Assert.Equal(
            HttpStatusCode.Conflict,
            (await adultClient.PostAsJsonAsync(
                $"/api/chore-assignments/{assignment.Id}/approve",
                new ReviewChoreAssignmentRequest())).StatusCode);

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Single(await dbContext.ChoreCompletions.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task Child_points_are_private_and_sum_only_own_approved_completions()
    {
        using var adultClient = CreateClient();
        using var firstChildClient = CreateClient();
        using var secondChildClient = CreateClient();
        await RegisterAndLoginAdult(
            adultClient,
            "Familjen Totals",
            "totals.points@example.test");
        var firstChild = await CreateChild(adultClient, "Vera");
        var secondChild = await CreateChild(adultClient, "Sam");
        var tenPointChore = await CreateChore(adultClient, "Mata katten", 10);
        var twentyPointChore = await CreateChore(adultClient, "Töm diskmaskinen", 20);
        var firstAssignment = await AssignChore(adultClient, tenPointChore.Id, firstChild.Id);
        var secondAssignment = await AssignChore(adultClient, twentyPointChore.Id, secondChild.Id);
        await PairChild(adultClient, firstChildClient, firstChild.Id);
        await PairChild(adultClient, secondChildClient, secondChild.Id);
        await SubmitAssignment(firstChildClient, firstAssignment.Id);
        await SubmitAssignment(secondChildClient, secondAssignment.Id);
        await adultClient.PostAsJsonAsync(
            $"/api/chore-assignments/{firstAssignment.Id}/approve",
            new ReviewChoreAssignmentRequest());
        await adultClient.PostAsJsonAsync(
            $"/api/chore-assignments/{secondAssignment.Id}/approve",
            new ReviewChoreAssignmentRequest());

        Assert.Equal(10, (await GetPoints(firstChildClient)).TotalPoints);
        Assert.Equal(20, (await GetPoints(secondChildClient)).TotalPoints);
        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await adultClient.GetAsync("/api/child/points")).StatusCode);
        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await CreateClient().GetAsync("/api/child/points")).StatusCode);
    }

    public void Dispose() => factory.Dispose();

    private HttpClient CreateClient() => factory.CreateClient(
        new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false,
            HandleCookies = true
        });

    private static async Task<ChildPointsResponse> GetPoints(HttpClient childClient) =>
        (await childClient.GetFromJsonAsync<ChildPointsResponse>("/api/child/points"))!;

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
        int? points)
    {
        var response = points.HasValue
            ? await client.PostAsJsonAsync("/api/chores", new { Title = title, Points = points.Value })
            : await client.PostAsJsonAsync("/api/chores", new { Title = title });
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
