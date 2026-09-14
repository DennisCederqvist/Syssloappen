using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Syssloappen.Api.Dtos.Auth;
using Syssloappen.Api.Dtos.Children;
using Syssloappen.Api.Dtos.ChoreAssignments;
using Syssloappen.Api.Dtos.Chores;
using Syssloappen.Api.Dtos.Rewards;
using Syssloappen.Api.Services;
using Xunit;

namespace Syssloappen.Api.Tests;

public sealed class NotificationDispatchTests : IDisposable
{
    private const string Password = "Password1";
    private readonly AuthApiFactory factory = new();

    [Fact]
    public async Task Assigning_a_chore_notifies_the_child()
    {
        using var adultClient = CreateClient();
        using var childClient = CreateClient();
        await RegisterAndLoginAdult(adultClient, "Familjen Notify Assign", "notify.assign@example.test");
        var child = await CreateChild(adultClient, "Maja");
        var chore = await CreateChore(adultClient, "Mata katten", 7);
        await PairChild(adultClient, childClient, child.Id);

        var assignment = await AssignChore(adultClient, chore.Id, child.Id);

        var notification = Assert.Single(factory.NotificationDispatcher.ChildNotifications);
        Assert.Equal(child.Id, notification.ChildProfileId);
        Assert.Equal(NotificationEventType.ChoreAssigned, notification.Event.Type);
        var data = Assert.IsType<ChoreAssignedData>(notification.Event.Data);
        Assert.Equal(assignment.Id, data.AssignmentId);
        Assert.Equal("Mata katten", data.ChoreTitle);
        Assert.Equal(7, data.Points);
    }

    [Fact]
    public async Task Approving_a_chore_notifies_the_child_with_points()
    {
        using var adultClient = CreateClient();
        using var childClient = CreateClient();
        await RegisterAndLoginAdult(adultClient, "Familjen Notify Approve", "notify.approve@example.test");
        var child = await CreateChild(adultClient, "Leo");
        var chore = await CreateChore(adultClient, "Städa rummet", 12);
        await PairChild(adultClient, childClient, child.Id);
        var assignment = await AssignChore(adultClient, chore.Id, child.Id);
        await SubmitAssignment(childClient, assignment.Id);
        factory.NotificationDispatcher.ChildNotifications.Clear();

        var response = await adultClient.PostAsJsonAsync(
            $"/api/chore-assignments/{assignment.Id}/approve",
            new ReviewChoreAssignmentRequest());
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var notification = Assert.Single(factory.NotificationDispatcher.ChildNotifications);
        Assert.Equal(child.Id, notification.ChildProfileId);
        Assert.Equal(NotificationEventType.ChoreApproved, notification.Event.Type);
        var data = Assert.IsType<ChoreApprovedData>(notification.Event.Data);
        Assert.Equal(assignment.Id, data.AssignmentId);
        Assert.Equal("Städa rummet", data.ChoreTitle);
        Assert.Equal(12, data.Points);
    }

    [Fact]
    public async Task Rejecting_a_chore_notifies_the_child_that_it_needs_redo()
    {
        using var adultClient = CreateClient();
        using var childClient = CreateClient();
        await RegisterAndLoginAdult(adultClient, "Familjen Notify Redo", "notify.redo@example.test");
        var child = await CreateChild(adultClient, "Nora");
        var chore = await CreateChore(adultClient, "Bädda sängen", 5);
        await PairChild(adultClient, childClient, child.Id);
        var assignment = await AssignChore(adultClient, chore.Id, child.Id);
        await SubmitAssignment(childClient, assignment.Id);
        factory.NotificationDispatcher.ChildNotifications.Clear();

        var response = await adultClient.PostAsJsonAsync(
            $"/api/chore-assignments/{assignment.Id}/reject",
            new ReviewChoreAssignmentRequest());
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var notification = Assert.Single(factory.NotificationDispatcher.ChildNotifications);
        Assert.Equal(child.Id, notification.ChildProfileId);
        Assert.Equal(NotificationEventType.ChoreNeedsRedo, notification.Event.Type);
        var data = Assert.IsType<ChoreNeedsRedoData>(notification.Event.Data);
        Assert.Equal(assignment.Id, data.AssignmentId);
        Assert.Equal("Bädda sängen", data.ChoreTitle);
    }

    [Fact]
    public async Task Submitting_a_chore_notifies_household_adults()
    {
        using var adultClient = CreateClient();
        using var childClient = CreateClient();
        var registration = await RegisterAndLoginAdult(
            adultClient, "Familjen Notify Submit", "notify.submit@example.test");
        var child = await CreateChild(adultClient, "Iris");
        var chore = await CreateChore(adultClient, "Packa skolväskan", 5);
        await PairChild(adultClient, childClient, child.Id);
        var assignment = await AssignChore(adultClient, chore.Id, child.Id);
        factory.NotificationDispatcher.HouseholdAdultNotifications.Clear();

        await SubmitAssignment(childClient, assignment.Id);

        var notification = Assert.Single(factory.NotificationDispatcher.HouseholdAdultNotifications);
        Assert.Equal(registration.HouseholdId, notification.HouseholdId);
        Assert.Equal(NotificationEventType.ChoreSubmittedForReview, notification.Event.Type);
        var data = Assert.IsType<ChoreSubmittedForReviewData>(notification.Event.Data);
        Assert.Equal(assignment.Id, data.AssignmentId);
        Assert.Equal("Packa skolväskan", data.ChoreTitle);
        Assert.Equal("Iris", data.ChildName);
    }

    [Fact]
    public async Task Requesting_a_reward_notifies_household_adults()
    {
        using var adultClient = CreateClient();
        using var childClient = CreateClient();
        var registration = await RegisterAndLoginAdult(
            adultClient, "Familjen Notify Request", "notify.request@example.test");
        var child = await CreateChild(adultClient, "Vera");
        await PairChild(adultClient, childClient, child.Id);
        await EarnPoints(adultClient, childClient, child.Id, 10);
        var reward = await CreateReward(adultClient, "Filmkväll", 10);
        factory.NotificationDispatcher.HouseholdAdultNotifications.Clear();

        var response = await childClient.PostAsync(
            "/api/child/reward-redemptions",
            JsonContentWithIdempotencyKey(new CreateRewardRedemptionRequest { RewardId = reward.Id }));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var notification = Assert.Single(factory.NotificationDispatcher.HouseholdAdultNotifications);
        Assert.Equal(registration.HouseholdId, notification.HouseholdId);
        Assert.Equal(NotificationEventType.RewardRequested, notification.Event.Type);
        var data = Assert.IsType<RewardRequestedData>(notification.Event.Data);
        Assert.Equal("Filmkväll", data.RewardName);
        Assert.Equal("Vera", data.ChildName);
    }

    [Fact]
    public async Task Approving_a_reward_redemption_notifies_the_child()
    {
        using var adultClient = CreateClient();
        using var childClient = CreateClient();
        await RegisterAndLoginAdult(adultClient, "Familjen Notify Reward Approve", "notify.rewardapprove@example.test");
        var child = await CreateChild(adultClient, "Sam");
        await PairChild(adultClient, childClient, child.Id);
        await EarnPoints(adultClient, childClient, child.Id, 10);
        var reward = await CreateReward(adultClient, "Glass", 10);
        var createResponse = await childClient.PostAsync(
            "/api/child/reward-redemptions",
            JsonContentWithIdempotencyKey(new CreateRewardRedemptionRequest { RewardId = reward.Id }));
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var redemption = (await createResponse.Content.ReadFromJsonAsync<RewardRedemptionResponse>())!;
        factory.NotificationDispatcher.ChildNotifications.Clear();

        var approveResponse = await adultClient.PostAsJsonAsync(
            $"/api/reward-redemptions/{redemption.Id}/approve",
            new UpdateRewardRedemptionRequest());
        Assert.Equal(HttpStatusCode.OK, approveResponse.StatusCode);

        var notification = Assert.Single(factory.NotificationDispatcher.ChildNotifications);
        Assert.Equal(child.Id, notification.ChildProfileId);
        Assert.Equal(NotificationEventType.RewardApproved, notification.Event.Type);
        var data = Assert.IsType<RewardApprovedData>(notification.Event.Data);
        Assert.Equal(redemption.Id, data.RedemptionId);
        Assert.Equal("Glass", data.RewardName);
    }

    public void Dispose() => factory.Dispose();

    private HttpClient CreateClient() => factory.CreateClient(new WebApplicationFactoryClientOptions
    {
        BaseAddress = new Uri("https://localhost"),
        AllowAutoRedirect = false,
        HandleCookies = true
    });

    private static HttpContent JsonContentWithIdempotencyKey(CreateRewardRedemptionRequest request)
    {
        var content = JsonContent.Create(request);
        content.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        return content;
    }

    private static async Task SubmitAssignment(HttpClient childClient, int assignmentId)
    {
        var response = await childClient.PostAsync($"/api/child/chore-assignments/{assignmentId}/submit", null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static async Task EarnPoints(HttpClient adultClient, HttpClient childClient, int childId, int points)
    {
        var chore = await CreateChore(adultClient, $"Tjäna poäng {Guid.NewGuid():N}", points);
        var assignment = await AssignChore(adultClient, chore.Id, childId);
        await SubmitAssignment(childClient, assignment.Id);
        var approveResponse = await adultClient.PostAsJsonAsync(
            $"/api/chore-assignments/{assignment.Id}/approve",
            new ReviewChoreAssignmentRequest());
        Assert.Equal(HttpStatusCode.OK, approveResponse.StatusCode);
    }

    private static async Task<ChoreAssignmentResponse> AssignChore(HttpClient client, int choreId, int childId)
    {
        var response = await client.PostAsJsonAsync(
            "/api/chore-assignments",
            new CreateChoreAssignmentRequest { ChoreId = choreId, ChildId = childId });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ChoreAssignmentResponse>())!;
    }

    private static async Task<ChoreResponse> CreateChore(HttpClient client, string title, int points)
    {
        var response = await client.PostAsJsonAsync("/api/chores", new { Title = title, Points = points });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ChoreResponse>())!;
    }

    private static async Task<CreateChildResponse> CreateChild(HttpClient client, string name)
    {
        var response = await client.PostAsJsonAsync(
            "/api/children",
            new CreateChildRequest { Name = name, UserName = $"child-{Guid.NewGuid():N}", Password = Password });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<CreateChildResponse>())!;
    }

    private static async Task<RewardResponse> CreateReward(HttpClient client, string name, int pointsCost)
    {
        var response = await client.PostAsJsonAsync(
            "/api/rewards",
            new CreateRewardRequest { Name = name, PointsCost = Math.Max(pointsCost, 1), StockQuantity = 1 });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<RewardResponse>())!;
    }

    private static async Task PairChild(HttpClient adultClient, HttpClient childClient, int childId)
    {
        var issueResponse = await adultClient.PostAsync($"/api/children/{childId}/pairing-codes", null);
        Assert.Equal(HttpStatusCode.Created, issueResponse.StatusCode);
        var code = (await issueResponse.Content.ReadFromJsonAsync<ChildPairingCodeResponse>())!;
        var pairResponse = await childClient.PostAsJsonAsync(
            "/api/auth/child/pair",
            new PairChildDeviceRequest { Code = code.Code });
        Assert.Equal(HttpStatusCode.OK, pairResponse.StatusCode);
    }

    private async Task<RegisterAdultResponse> RegisterAndLoginAdult(
        HttpClient client, string householdName, string email)
    {
        var registerResponse = await client.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterAdultRequest { HouseholdName = householdName, Email = email, Password = Password });
        Assert.Equal(HttpStatusCode.Created, registerResponse.StatusCode);
        await TestEmailConfirmation.ConfirmLatestAsync(client, factory.EmailSender, email);
        var registration = (await registerResponse.Content.ReadFromJsonAsync<RegisterAdultResponse>())!;
        var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login", new LoginRequest { Email = email, Password = Password });
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        return registration;
    }
}
