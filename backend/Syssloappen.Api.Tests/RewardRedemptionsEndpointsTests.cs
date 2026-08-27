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
using Syssloappen.Api.Dtos.Rewards;
using Xunit;

namespace Syssloappen.Api.Tests;

public sealed class RewardRedemptionsEndpointsTests : IDisposable
{
    private const string Password = "Password1";
    private readonly AuthApiFactory factory = new();

    [Fact]
    public async Task Only_active_child_can_list_rewards_and_request_redemption()
    {
        using var anonymous = Client(); using var adult = Client(); using var child = Client();
        await RegisterLogin(adult, "Redemption A", "redemption.access@example.test");
        var profile = await CreateChild(adult, "Maja"); await Pair(adult, child, profile.Id);
        var reward = await CreateReward(adult, "Godis", 10);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/child/rewards")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await adult.GetAsync("/api/child/rewards")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await child.PostAsJsonAsync("/api/child/reward-redemptions", new { rewardId = reward.Id })).StatusCode);
    }

    [Fact]
    public async Task Child_only_sees_active_rewards_in_own_household()
    {
        using var firstAdult = Client(); using var firstChild = Client(); using var secondAdult = Client();
        await RegisterLogin(firstAdult, "Redemption B", "redemption.first@example.test");
        var child = await CreateChild(firstAdult, "Vera"); await Pair(firstAdult, firstChild, child.Id);
        await RegisterLogin(secondAdult, "Redemption C", "redemption.second@example.test");
        await CreateReward(firstAdult, "Film", 20); var hidden = await CreateReward(firstAdult, "Gammal", 5); await firstAdult.DeleteAsync($"/api/rewards/{hidden.Id}");
        await CreateReward(secondAdult, "Främmande", 1);
        var response = await firstChild.GetFromJsonAsync<ChildRewardsResponse>("/api/child/rewards");
        Assert.Equal(0, response!.AvailablePoints);
        Assert.Equal("Film", Assert.Single(response.Rewards).Name);
    }

    [Fact]
    public async Task Request_reserves_snapshot_points_and_same_key_is_idempotent()
    {
        using var adult = Client(); using var childClient = Client();
        await RegisterLogin(adult, "Redemption D", "redemption.idempotent@example.test");
        var child = await CreateChild(adult, "Liam"); await AwardPoints(adult, childClient, child.Id, 20);
        var reward = await CreateReward(adult, "Bio", 15); var key = Guid.NewGuid().ToString();
        var first = await Redeem(childClient, reward.Id, key);
        var replay = await Redeem(childClient, reward.Id, key);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode); Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        var created = (await first.Content.ReadFromJsonAsync<RewardRedemptionResponse>())!;
        var repeated = (await replay.Content.ReadFromJsonAsync<RewardRedemptionResponse>())!;
        Assert.Equal(created.Id, repeated.Id); Assert.Equal(15, created.PointsCost); Assert.Equal(5, created.AvailablePoints);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Single(await db.RewardRedemptions.ToListAsync()); Assert.Equal(15, (await db.ChildPointReservations.SingleAsync()).ReservedPoints);
    }

    [Fact]
    public async Task Insufficient_points_and_foreign_reward_are_neutral_and_do_not_reserve()
    {
        using var adult = Client(); using var childClient = Client(); using var otherAdult = Client();
        await RegisterLogin(adult, "Redemption E", "redemption.balance@example.test");
        var child = await CreateChild(adult, "Nils"); await AwardPoints(adult, childClient, child.Id, 10);
        var expensive = await CreateReward(adult, "Dyr", 15);
        await RegisterLogin(otherAdult, "Redemption F", "redemption.other@example.test"); var foreign = await CreateReward(otherAdult, "Kapad", 1);
        Assert.Equal(HttpStatusCode.Conflict, (await Redeem(childClient, expensive.Id, Guid.NewGuid().ToString())).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Redeem(childClient, foreign.Id, Guid.NewGuid().ToString())).StatusCode);
        using var scope = factory.Services.CreateScope();
        Assert.Empty(await scope.ServiceProvider.GetRequiredService<AppDbContext>().RewardRedemptions.ToListAsync());
    }

    [Fact]
    public async Task Adult_can_approve_then_deliver_but_cannot_change_a_delivered_redemption()
    {
        using var adult = Client(); using var childClient = Client();
        await RegisterLogin(adult, "Redemption G", "redemption.delivery@example.test");
        var child = await CreateChild(adult, "Noah"); await AwardPoints(adult, childClient, child.Id, 20);
        var reward = await CreateReward(adult, "Spel", 10);
        var requested = await Redeem(childClient, reward.Id, Guid.NewGuid().ToString());
        var redemption = (await requested.Content.ReadFromJsonAsync<RewardRedemptionResponse>())!;

        var approved = await adult.PostAsJsonAsync($"/api/reward-redemptions/{redemption.Id}/approve", new UpdateRewardRedemptionRequest { Comment = "Bra jobbat" });
        Assert.Equal(HttpStatusCode.OK, approved.StatusCode);
        Assert.Equal("Approved", (await approved.Content.ReadFromJsonAsync<AdultRewardRedemptionResponse>())!.Status);
        var delivered = await adult.PostAsJsonAsync($"/api/reward-redemptions/{redemption.Id}/deliver", new UpdateRewardRedemptionRequest());
        Assert.Equal(HttpStatusCode.OK, delivered.StatusCode);
        Assert.NotNull((await delivered.Content.ReadFromJsonAsync<AdultRewardRedemptionResponse>())!.DeliveredAt);
        Assert.Equal(HttpStatusCode.Conflict, (await adult.PostAsJsonAsync($"/api/reward-redemptions/{redemption.Id}/cancel", new UpdateRewardRedemptionRequest())).StatusCode);
    }

    [Fact]
    public async Task Cancellation_releases_points_once_and_makes_reward_selectable_again()
    {
        using var adult = Client(); using var childClient = Client(); using var otherAdult = Client();
        await RegisterLogin(adult, "Redemption H", "redemption.cancel@example.test");
        var child = await CreateChild(adult, "Ella"); await AwardPoints(adult, childClient, child.Id, 20);
        var reward = await CreateReward(adult, "Bok", 15);
        var requested = await Redeem(childClient, reward.Id, Guid.NewGuid().ToString());
        var redemption = (await requested.Content.ReadFromJsonAsync<RewardRedemptionResponse>())!;
        await RegisterLogin(otherAdult, "Redemption I", "redemption.cancel-other@example.test");
        Assert.Equal(HttpStatusCode.NotFound, (await otherAdult.PostAsJsonAsync($"/api/reward-redemptions/{redemption.Id}/cancel", new UpdateRewardRedemptionRequest())).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await adult.PostAsJsonAsync($"/api/reward-redemptions/{redemption.Id}/cancel", new UpdateRewardRedemptionRequest())).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await adult.PostAsJsonAsync($"/api/reward-redemptions/{redemption.Id}/cancel", new UpdateRewardRedemptionRequest())).StatusCode);
        var rewards = await childClient.GetFromJsonAsync<ChildRewardsResponse>("/api/child/rewards");
        Assert.Equal(20, rewards!.AvailablePoints); Assert.Equal(reward.Id, Assert.Single(rewards.Rewards).Id);
        using var scope = factory.Services.CreateScope();
        Assert.Equal(0, (await scope.ServiceProvider.GetRequiredService<AppDbContext>().ChildPointReservations.SingleAsync()).ReservedPoints);
    }

    public void Dispose() => factory.Dispose();
    private HttpClient Client() => factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), HandleCookies = true, AllowAutoRedirect = false });
    private static async Task<HttpResponseMessage> Redeem(HttpClient client, int rewardId, string key) { var request = new HttpRequestMessage(HttpMethod.Post, "/api/child/reward-redemptions") { Content = JsonContent.Create(new CreateRewardRedemptionRequest { RewardId = rewardId }) }; request.Headers.Add("Idempotency-Key", key); return await client.SendAsync(request); }
    private static async Task<RewardResponse> CreateReward(HttpClient client, string name, int points) { var response = await client.PostAsJsonAsync("/api/rewards", new CreateRewardRequest { Name = name, PointsCost = points }); Assert.Equal(HttpStatusCode.Created, response.StatusCode); return (await response.Content.ReadFromJsonAsync<RewardResponse>())!; }
    private static async Task<CreateChildResponse> CreateChild(HttpClient client, string name) { var response = await client.PostAsJsonAsync("/api/children", new CreateChildRequest { Name = name, UserName = Guid.NewGuid().ToString("N"), Password = Password }); Assert.Equal(HttpStatusCode.Created, response.StatusCode); return (await response.Content.ReadFromJsonAsync<CreateChildResponse>())!; }
    private static async Task Pair(HttpClient adult, HttpClient child, int childId) { var issue = await adult.PostAsync($"/api/children/{childId}/pairing-codes", null); var code = (await issue.Content.ReadFromJsonAsync<ChildPairingCodeResponse>())!; Assert.Equal(HttpStatusCode.OK, (await child.PostAsJsonAsync("/api/auth/child/pair", new PairChildDeviceRequest { Code = code.Code })).StatusCode); }
    private static async Task AwardPoints(HttpClient adult, HttpClient child, int childId, int points) { var chore = await adult.PostAsJsonAsync("/api/chores", new CreateChoreRequest { Title = "Test", Points = points }); var c = (await chore.Content.ReadFromJsonAsync<ChoreResponse>())!; var assigned = await adult.PostAsJsonAsync("/api/chore-assignments", new CreateChoreAssignmentRequest { ChoreId = c.Id, ChildId = childId }); var a = (await assigned.Content.ReadFromJsonAsync<ChoreAssignmentResponse>())!; await Pair(adult, child, childId); Assert.Equal(HttpStatusCode.OK, (await child.PostAsync($"/api/child/chore-assignments/{a.Id}/submit", null)).StatusCode); Assert.Equal(HttpStatusCode.OK, (await adult.PostAsJsonAsync($"/api/chore-assignments/{a.Id}/approve", new ReviewChoreAssignmentRequest())).StatusCode); }
    private static async Task RegisterLogin(HttpClient client, string name, string email) { Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync("/api/auth/register", new RegisterAdultRequest { HouseholdName = name, Email = email, Password = Password })).StatusCode); Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/auth/login", new LoginRequest { Email = email, Password = Password })).StatusCode); }
}
