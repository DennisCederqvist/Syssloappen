using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Syssloappen.Api.Dtos.Auth;
using Syssloappen.Api.Dtos.Children;
using Syssloappen.Api.Dtos.ChoreAssignments;
using Syssloappen.Api.Dtos.Chores;
using Syssloappen.Api.Dtos.Rewards;
using Xunit;

namespace Syssloappen.Api.Tests;

// Adults see each child's available points in the child list, and it is the same number the
// child sees in their own view.
public sealed class ChildPointBalanceTests : IDisposable
{
    private const string Password = "Password1";
    private readonly AuthApiFactory factory = new();

    [Fact]
    public async Task A_new_child_has_no_points()
    {
        using var adult = CreateClient();
        await RegisterAndLoginAdult(adult, "Familjen Poang Noll", "points.zero@example.test");
        await CreateChild(adult, "Ella");

        var child = Assert.Single(await GetChildren(adult));

        Assert.Equal(0, child.AvailablePoints);
    }

    [Fact]
    public async Task Approved_chores_add_to_the_balance_the_child_also_sees()
    {
        using var adult = CreateClient();
        using var childClient = CreateClient();
        await RegisterAndLoginAdult(adult, "Familjen Poang Intjanat", "points.earned@example.test");
        var child = await CreateChild(adult, "Alva");
        await PairChild(adult, childClient, child.Id);

        await EarnPoints(adult, childClient, child.Id, 10);
        await EarnPoints(adult, childClient, child.Id, 5);

        var listed = Assert.Single(await GetChildren(adult));
        var ownView = await childClient.GetFromJsonAsync<ChildRewardsResponse>("/api/child/rewards");
        Assert.Equal(15, listed.AvailablePoints);
        Assert.Equal(ownView!.AvailablePoints, listed.AvailablePoints);
    }

    [Fact]
    public async Task Points_reserved_by_a_reward_request_are_deducted()
    {
        using var adult = CreateClient();
        using var childClient = CreateClient();
        await RegisterAndLoginAdult(adult, "Familjen Poang Reserverat", "points.reserved@example.test");
        var child = await CreateChild(adult, "Noa");
        await PairChild(adult, childClient, child.Id);
        await EarnPoints(adult, childClient, child.Id, 10);
        var reward = await CreateReward(adult, "Glass", 4);

        var content = JsonContent.Create(new CreateRewardRedemptionRequest { RewardId = reward.Id });
        content.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        Assert.Equal(HttpStatusCode.Created, (await childClient.PostAsync("/api/child/reward-redemptions", content)).StatusCode);

        var listed = Assert.Single(await GetChildren(adult));
        var ownView = await childClient.GetFromJsonAsync<ChildRewardsResponse>("/api/child/rewards");
        Assert.Equal(6, listed.AvailablePoints);
        Assert.Equal(ownView!.AvailablePoints, listed.AvailablePoints);
    }

    [Fact]
    public async Task Each_child_has_their_own_balance_and_other_households_are_not_included()
    {
        using var adult = CreateClient();
        using var otherAdult = CreateClient();
        using var childClient = CreateClient();
        await RegisterAndLoginAdult(adult, "Familjen Poang Egen", "points.own@example.test");
        await RegisterAndLoginAdult(otherAdult, "Familjen Poang Annan", "points.other@example.test");
        var earner = await CreateChild(adult, "Sam");
        await CreateChild(adult, "Vera");
        var stranger = await CreateChild(otherAdult, "Utomstaende");
        await PairChild(adult, childClient, earner.Id);
        await EarnPoints(adult, childClient, earner.Id, 7);

        var children = await GetChildren(adult);

        Assert.Equal(2, children.Count);
        Assert.Equal(7, children.Single(child => child.Id == earner.Id).AvailablePoints);
        Assert.Equal(0, children.Single(child => child.Name == "Vera").AvailablePoints);
        Assert.DoesNotContain(children, child => child.Id == stranger.Id);
        Assert.Equal(0, Assert.Single(await GetChildren(otherAdult)).AvailablePoints);
    }

    public void Dispose() => factory.Dispose();

    private HttpClient CreateClient() => factory.CreateClient(new WebApplicationFactoryClientOptions
    {
        BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false, HandleCookies = true
    });

    private static async Task<List<ChildWithPointsResponse>> GetChildren(HttpClient adult) =>
        (await adult.GetFromJsonAsync<List<ChildWithPointsResponse>>("/api/children"))!;

    private static async Task EarnPoints(HttpClient adult, HttpClient childClient, int childId, int points)
    {
        var chore = await CreateChore(adult, $"Tjäna poäng {Guid.NewGuid():N}", points);
        var assignResponse = await adult.PostAsJsonAsync(
            "/api/chore-assignments", new CreateChoreAssignmentRequest { ChoreId = chore.Id, ChildId = childId });
        Assert.Equal(HttpStatusCode.Created, assignResponse.StatusCode);
        var assignment = (await assignResponse.Content.ReadFromJsonAsync<ChoreAssignmentResponse>())!;

        Assert.Equal(HttpStatusCode.OK,
            (await childClient.PostAsync($"/api/child/chore-assignments/{assignment.Id}/submit", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK,
            (await adult.PostAsJsonAsync($"/api/chore-assignments/{assignment.Id}/approve", new ReviewChoreAssignmentRequest())).StatusCode);
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
            "/api/children", new CreateChildRequest { Name = name, UserName = $"child-{Guid.NewGuid():N}", Password = Password });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<CreateChildResponse>())!;
    }

    private static async Task<RewardResponse> CreateReward(HttpClient client, string name, int pointsCost)
    {
        var response = await client.PostAsJsonAsync(
            "/api/rewards", new CreateRewardRequest { Name = name, PointsCost = pointsCost, StockQuantity = 1 });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<RewardResponse>())!;
    }

    private static async Task PairChild(HttpClient adultClient, HttpClient childClient, int childId)
    {
        var issueResponse = await adultClient.PostAsync($"/api/children/{childId}/pairing-codes", null);
        Assert.Equal(HttpStatusCode.Created, issueResponse.StatusCode);
        var code = (await issueResponse.Content.ReadFromJsonAsync<ChildPairingCodeResponse>())!;
        var pairResponse = await childClient.PostAsJsonAsync("/api/auth/child/pair", new PairChildDeviceRequest { Code = code.Code });
        Assert.Equal(HttpStatusCode.OK, pairResponse.StatusCode);
    }

    private async Task RegisterAndLoginAdult(HttpClient client, string householdName, string email)
    {
        var registerResponse = await client.PostAsJsonAsync("/api/auth/register", new RegisterAdultRequest
        { HouseholdName = householdName, Email = email, Password = Password });
        Assert.Equal(HttpStatusCode.Created, registerResponse.StatusCode);
        await TestEmailConfirmation.ConfirmLatestAsync(client, factory.EmailSender, email);

        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest { Email = email, Password = Password });
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
    }
}
