using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Syssloappen.Api.Data;
using Syssloappen.Api.Dtos.Auth;
using Syssloappen.Api.Dtos.Children;
using Syssloappen.Api.Dtos.ChoreAssignments;
using Syssloappen.Api.Dtos.Chores;
using Syssloappen.Api.Dtos.PushSubscriptions;
using Syssloappen.Api.Services;
using Xunit;

namespace Syssloappen.Api.Tests;

/// <summary>Exercises the real <see cref="SignalRNotificationDispatcher"/> (rather than the
/// <see cref="FakeNotificationDispatcher"/> every other test uses) against a
/// <see cref="FakeWebPushClient"/>, to prove push fan-out and stale-subscription cleanup
/// actually happen.</summary>
public sealed class PushNotificationDispatchTests : IDisposable
{
    private const string Password = "Password1";
    private readonly AuthApiFactory baseFactory = new();
    private readonly WebApplicationFactory<Program> factory;

    public PushNotificationDispatchTests()
    {
        factory = baseFactory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<INotificationDispatcher>();
            services.AddScoped<INotificationDispatcher, SignalRNotificationDispatcher>();
        }));
    }

    [Fact]
    public async Task Assigning_a_chore_pushes_a_notification_to_the_childs_subscribed_device()
    {
        using var adultClient = CreateClient();
        using var childClient = CreateClient();
        await RegisterAndLoginAdult(adultClient, "Familjen Push Assign", "push.assign@example.test");
        var child = await CreateChild(adultClient, "Elin");
        var chore = await CreateChore(adultClient, "Diska", 8);
        await PairChild(adultClient, childClient, child.Id);

        await Subscribe(childClient, "https://push.example/child-device");

        var assignment = await AssignChore(adultClient, chore.Id, child.Id);

        var sent = Assert.Single(baseFactory.WebPushClient.SentNotifications);
        Assert.Equal("https://push.example/child-device", sent.Endpoint);
        Assert.Contains("New chore", sent.PayloadJson);
        Assert.Contains("Diska", sent.PayloadJson);
        Assert.Contains(assignment.Id.ToString(), sent.PayloadJson);
    }

    [Fact]
    public async Task No_push_is_attempted_when_the_target_has_no_subscription()
    {
        using var adultClient = CreateClient();
        using var childClient = CreateClient();
        await RegisterAndLoginAdult(adultClient, "Familjen Push None", "push.none@example.test");
        var child = await CreateChild(adultClient, "Leo");
        var chore = await CreateChore(adultClient, "Städa", 5);
        await PairChild(adultClient, childClient, child.Id);

        await AssignChore(adultClient, chore.Id, child.Id);

        Assert.Empty(baseFactory.WebPushClient.SentNotifications);
    }

    [Fact]
    public async Task A_gone_subscription_is_removed_without_failing_the_request()
    {
        using var adultClient = CreateClient();
        using var childClient = CreateClient();
        await RegisterAndLoginAdult(adultClient, "Familjen Push Gone", "push.gone@example.test");
        var child = await CreateChild(adultClient, "Nora");
        var chore = await CreateChore(adultClient, "Bädda", 5);
        await PairChild(adultClient, childClient, child.Id);
        await Subscribe(childClient, "https://push.example/gone-device");
        baseFactory.WebPushClient.GoneEndpoints.Add("https://push.example/gone-device");

        var response = await AssignChoreRaw(adultClient, chore.Id, child.Id);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        Assert.Empty(baseFactory.WebPushClient.SentNotifications);
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Empty(await dbContext.PushSubscriptions.AsNoTracking().ToListAsync());
    }

    public void Dispose()
    {
        factory.Dispose();
        baseFactory.Dispose();
    }

    private HttpClient CreateClient() => factory.CreateClient(new WebApplicationFactoryClientOptions
    {
        BaseAddress = new Uri("https://localhost"),
        AllowAutoRedirect = false,
        HandleCookies = true
    });

    private static async Task Subscribe(HttpClient client, string endpoint)
    {
        var response = await client.PostAsJsonAsync(
            "/api/push-subscriptions",
            new SubscribeToPushRequest { Endpoint = endpoint, P256dh = "key", Auth = "auth" });
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    private static async Task<HttpResponseMessage> AssignChoreRaw(HttpClient client, int choreId, int childId) =>
        await client.PostAsJsonAsync(
            "/api/chore-assignments",
            new CreateChoreAssignmentRequest { ChoreId = choreId, ChildId = childId });

    private static async Task<ChoreAssignmentResponse> AssignChore(HttpClient client, int choreId, int childId)
    {
        var response = await AssignChoreRaw(client, choreId, childId);
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

    private static async Task PairChild(HttpClient adultClient, HttpClient childClient, int childId)
    {
        var issueResponse = await adultClient.PostAsync($"/api/children/{childId}/pairing-codes", null);
        Assert.Equal(HttpStatusCode.Created, issueResponse.StatusCode);
        var code = (await issueResponse.Content.ReadFromJsonAsync<ChildPairingCodeResponse>())!;
        var pairResponse = await childClient.PostAsJsonAsync(
            "/api/auth/child/pair", new PairChildDeviceRequest { Code = code.Code });
        Assert.Equal(HttpStatusCode.OK, pairResponse.StatusCode);
    }

    private async Task RegisterAndLoginAdult(HttpClient client, string householdName, string email)
    {
        var registerResponse = await client.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterAdultRequest { HouseholdName = householdName, Email = email, Password = Password });
        Assert.Equal(HttpStatusCode.Created, registerResponse.StatusCode);
        await TestEmailConfirmation.ConfirmLatestAsync(client, baseFactory.EmailSender, email);
        var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login", new LoginRequest { Email = email, Password = Password });
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
    }
}
